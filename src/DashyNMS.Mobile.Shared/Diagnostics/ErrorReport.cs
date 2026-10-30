using System.Text.Json.Serialization;

namespace DashyNMS.Mobile.Diagnostics;

/// <summary>How an error reached the reporter, which decides its level in Sentry.</summary>
public enum ErrorOrigin
{
    /// <summary>Nothing caught it and the app is closing: fatal, sent at the next launch.</summary>
    Unhandled,

    /// <summary>A task failed and nobody looked; the app carries on.</summary>
    UnobservedTask,

    /// <summary>Caught and shown to the user, but not a state we expect - a bug, as a warning.</summary>
    Caught,
}

/// <summary>
/// What every report says about the app: its release, the platform, the OS
/// version and the architecture - fixed, bounded values, never the device's
/// name or model.
/// </summary>
public sealed record ErrorReportContext(string Release, string OperatingSystem, string OsVersion, string Architecture);

/// <summary>One frame from a DashyNMS assembly: no arguments, locals or paths outside the repository.</summary>
public sealed record ErrorFrame(string Module, string Type, string Function, string? FileName = null, int? LineNumber = null, int? IlOffset = null);

/// <summary>An exception's type and its DashyNMS frames - never its message.</summary>
public sealed record ErrorException(string Type, IReadOnlyList<ErrorFrame> Frames);

/// <summary>
/// Anything queued for Sentry. Error reports are the only kind for now;
/// usage measurements (#218) join them here so they share the queue's caps.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ErrorReport), "error")]
public abstract record DiagnosticReport(int Schema, Guid EventId, DateTimeOffset Timestamp, ErrorReportContext Context);

/// <summary>An error report as it is saved and sent: up to three exceptions, innermost last.</summary>
public sealed record ErrorReport(int Schema, Guid EventId, DateTimeOffset Timestamp,
    ErrorReportContext Context, ErrorOrigin Origin, IReadOnlyList<ErrorException> Exceptions)
    : DiagnosticReport(Schema, EventId, Timestamp, Context);

public enum SendDisposition
{
    Accepted,
    Retry,
    Rejected,
}

/// <summary>Sentry's answer; a rate limit says when to try again.</summary>
public sealed record SendResult(SendDisposition Disposition, TimeSpan? RetryAfter = null);

/// <summary>Where reports go - Sentry in the app, a recording fake in tests.</summary>
public interface IDiagnosticsTransport
{
    /// <summary>This build has a usable key; without one nothing is ever sent.</summary>
    bool IsAvailable { get; }

    Task<SendResult> SendAsync(DiagnosticReport report, CancellationToken cancellationToken);
}

/// <summary>Where code that caught something unexpected reports it (#217).</summary>
public interface IErrorReports
{
    /// <summary>
    /// Queues <paramref name="exception"/> if error reports are allowed and it
    /// isn't an expected state - offline, a timeout, the server saying no.
    /// </summary>
    void Capture(Exception exception, ErrorOrigin origin);
}
