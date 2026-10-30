using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DesktopNMS.Core.Api;

namespace DashyNMS.Mobile.Diagnostics;

/// <summary>
/// Turns an exception into what may leave the phone (#217), as desktop's
/// payload does (DashyNMS/desktop#62): exception types, and the type, method,
/// repository-relative file and line of frames in DashyNMS's own assemblies.
/// Messages, arguments, paths outside the repository and anything from other
/// libraries are dropped - a message can hold an address, a device name or a
/// token, so none is ever read.
/// </summary>
/// <remarks>
/// <see cref="IsValid"/> checks every field against the same rules again, for
/// reports read back from disk: a tampered or older file is deleted, not sent.
/// </remarks>
public static partial class ErrorPayload
{
    /// <summary>The most one report may take, on disk and on the wire.</summary>
    public const int MaximumBytes = 32 * 1024;

    private const int MaximumExceptions = 3;
    private const int MaximumFrames = 32;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 12,
    };

    /// <summary>The assemblies whose frames are ours, and the repository folder each builds from.</summary>
    private static readonly Dictionary<string, string> AppAssemblies = new(StringComparer.Ordinal)
    {
        ["DashyNMS.Mobile"] = "src/DashyNMS.Mobile/",
        ["DashyNMS.Mobile.Shared"] = "src/DashyNMS.Mobile.Shared/",
        ["DesktopNMS.Core"] = "src/DesktopNMS.Core/",
    };

    private static readonly HashSet<string> OperatingSystems = new(StringComparer.Ordinal) { "ios", "android", "unknown" };

    private static readonly HashSet<string> Architectures = new(StringComparer.Ordinal) { "arm64", "x64", "arm", "x86", "unknown" };

    public static ErrorReport Create(Exception exception, ErrorOrigin origin, ErrorReportContext context, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var exceptions = new List<ErrorException>();
        var remainingFrames = MaximumFrames;
        for (var current = exception; current is not null && exceptions.Count < MaximumExceptions; current = Inner(current))
        {
            var type = current.GetType();
            var trusted = type.Assembly == typeof(Exception).Assembly || IsAppAssembly(type.Assembly);
            var frames = new StackTrace(current, true).GetFrames()
                .Where(frame => frame.GetMethod() is { DeclaringType: not null } method && IsAppAssembly(method.Module.Assembly))
                .Take(remainingFrames)
                .Select(CreateFrame)
                .Reverse()
                .ToArray();
            exceptions.Add(new ErrorException(trusted ? MetadataName(type) : "ExternalException", frames));
            remainingFrames -= frames.Length;
        }

        return new ErrorReport(1, Guid.NewGuid(), now, SafeContext(context), origin, exceptions);
    }

    /// <summary>
    /// An expected state rather than a bug: offline, a timeout, a certificate,
    /// the server or Graylog answering with an error. These are the user's to
    /// see, never a report - an <see cref="ErrorOrigin.Caught"/> one is dropped.
    /// </summary>
    public static bool IsExpected(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is OperationCanceledException or TimeoutException or HttpRequestException or SocketException
                or AuthenticationException or LibreNmsApiException or GraylogApiException or UnimusApiException)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The context with anything outside the fixed lists as "unknown".</summary>
    public static ErrorReportContext SafeContext(ErrorReportContext context) => new(
        context.Release.Length <= 96 && ReleasePattern().IsMatch(context.Release) ? context.Release : "unknown",
        OperatingSystems.Contains(context.OperatingSystem) ? context.OperatingSystem : "unknown",
        OsVersionPattern().IsMatch(context.OsVersion) ? context.OsVersion : "unknown",
        Architectures.Contains(context.Architecture) ? context.Architecture : "unknown");

    /// <summary>"26.1" from "26.1.2", "15" from "15": major and minor only, so it's a version band rather than a build.</summary>
    public static string OsVersionBand(Version? version) => version is null || version.Major < 0
        ? "unknown"
        : version.Minor > 0 ? $"{version.Major}.{version.Minor}" : $"{version.Major}";

    /// <summary>"arm64", "x64" and so on, from .NET's own name for the process's architecture.</summary>
    public static string ArchitectureName(System.Runtime.InteropServices.Architecture architecture) => architecture switch
    {
        System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
        System.Runtime.InteropServices.Architecture.X64 => "x64",
        System.Runtime.InteropServices.Architecture.Arm => "arm",
        System.Runtime.InteropServices.Architecture.X86 => "x86",
        _ => "unknown",
    };

    public static bool IsValid(DiagnosticReport report)
    {
        if (report.Schema != 1 || report.EventId == Guid.Empty || report.Context is not { } context
            || context.Release is null || context.Release.Length > 96
            || (context.Release != "unknown" && !ReleasePattern().IsMatch(context.Release))
            || !OperatingSystems.Contains(context.OperatingSystem ?? string.Empty)
            || (context.OsVersion != "unknown" && !OsVersionPattern().IsMatch(context.OsVersion ?? string.Empty))
            || !Architectures.Contains(context.Architecture ?? string.Empty))
        {
            return false;
        }

        return report is ErrorReport error
            && Enum.IsDefined(error.Origin)
            && error.Exceptions is { Count: >= 1 and <= MaximumExceptions }
            && error.Exceptions.All(exception => exception is not null && IsName(exception.Type)
                && exception.Frames is { Count: <= MaximumFrames }
                && exception.Frames.All(IsValidFrame));
    }

    private static bool IsValidFrame(ErrorFrame frame) =>
        frame is not null
        && AppAssemblies.ContainsKey(frame.Module ?? string.Empty)
        && IsName(frame.Type) && IsName(frame.Function)
        && (frame.FileName is null ? frame.LineNumber is null : IsSourceName(frame.Module!, frame.FileName))
        && frame.LineNumber is null or > 0 and <= 10_000_000
        && frame.IlOffset is null or >= 0;

    /// <summary>The next exception down: an aggregate's first, otherwise the inner one.</summary>
    private static Exception? Inner(Exception exception) =>
        exception is AggregateException { InnerExceptions.Count: > 0 } aggregate ? aggregate.InnerExceptions[0] : exception.InnerException;

    private static ErrorFrame CreateFrame(StackFrame frame)
    {
        var physical = frame.GetMethod()!;
        var method = LogicalMethod(physical);
        var module = physical.Module.Assembly.GetName().Name!;
        var file = SourceName(module, frame.GetFileName());
        var line = frame.GetFileLineNumber();
        var offset = frame.GetILOffset();
        return new ErrorFrame(module, MetadataName(method.DeclaringType!), SafeName(method.Name), file,
            file is not null && line is > 0 and <= 10_000_000 ? line : null,
            offset >= 0 ? offset : null);
    }

    /// <summary>An async method's own name rather than its state machine's MoveNext.</summary>
    private static MethodBase LogicalMethod(MethodBase method)
    {
        if (method.Name != "MoveNext" || method.DeclaringType?.DeclaringType is not { } owner)
        {
            return method;
        }

        try
        {
            return owner.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(candidate => candidate.GetCustomAttributes<StateMachineAttribute>()
                    .Any(attribute => attribute.StateMachineType == method.DeclaringType)) ?? method;
        }
        catch (Exception)
        {
            return method;
        }
    }

    /// <summary>The path from the repository root - "src/DashyNMS.Mobile.Shared/…" - or nothing: never the build machine's.</summary>
    private static string? SourceName(string module, string? path)
    {
        if (path is null || !AppAssemblies.TryGetValue(module, out var root))
        {
            return null;
        }

        path = path.Replace('\\', '/');
        var index = path.LastIndexOf("/" + root, StringComparison.Ordinal);
        var relative = index >= 0 ? path[(index + 1)..] : path;
        return IsSourceName(module, relative) ? relative : null;
    }

    private static bool IsSourceName(string module, string name) =>
        AppAssemblies.TryGetValue(module, out var root)
        && name.Length <= 256
        && name.StartsWith(root, StringComparison.Ordinal)
        && name.EndsWith(".cs", StringComparison.Ordinal)
        && name.Split('/').All(segment => segment.Length > 0 && segment is not ("." or "..")
            && segment.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.'));

    private static bool IsAppAssembly(Assembly assembly) => AppAssemblies.ContainsKey(assembly.GetName().Name ?? string.Empty);

    private static string MetadataName(Type type) =>
        SafeName(string.IsNullOrEmpty(type.Namespace) ? type.Name : type.Namespace + "." + type.Name);

    private static string SafeName(string name) => IsName(name) ? name : "Unknown";

    private static bool IsName(string? name) => name is { Length: > 0 and <= 256 } && MetadataPattern().IsMatch(name);

    [GeneratedRegex("^[A-Za-z0-9_.+<>`]+$", RegexOptions.CultureInvariant)]
    private static partial Regex MetadataPattern();

    [GeneratedRegex(@"^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?(?:\+[A-Za-z0-9.-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ReleasePattern();

    [GeneratedRegex(@"^[0-9]{1,3}(?:\.[0-9]{1,3})?$", RegexOptions.CultureInvariant)]
    private static partial Regex OsVersionPattern();
}
