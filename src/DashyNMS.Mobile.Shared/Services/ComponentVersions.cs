using System.Reflection;
using System.Text.RegularExpressions;
using DesktopNMS.Core.Sensors;

namespace DashyNMS.Mobile.Services;

/// <summary>One line of About's versions (#215): a component, its version and its state.</summary>
public sealed record ComponentVersion(string Name, string Version, string State)
{
    /// <summary>Without the build metadata after "+" - the diagnostics header keeps it.</summary>
    public string DisplayVersion => Version.Split('+')[0];
}

/// <summary>
/// What About lists (#215), as desktop's (DashyNMS/desktop#529): the app,
/// Core and the sensor definitions. The phone's system and a copy button
/// went (#269): the system is the phone's own, and Share diagnostics already
/// carries these versions, the system and the phone for a bug report.
/// </summary>
/// <remarks>
/// The diagnostics header carries this text, so it holds versions only -
/// never the server, device names, a token or a path. Different component
/// versions are normal.
/// </remarks>
public static partial class ComponentVersions
{
    public const string Unavailable = "Unavailable";

    public static string AssemblyVersion(Assembly assembly) =>
        Clean(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
              ?? assembly.GetName().Version?.ToString() ?? Unavailable);

    /// <summary>A version-shaped string, or <see cref="Unavailable"/>: support text never copies arbitrary text.</summary>
    internal static string Clean(string? value) => value is { Length: > 0 and <= 200 } && VersionShape().IsMatch(value)
        ? value
        : Unavailable;

    /// <summary>Desktop's states. Core's status text is never shown: it can hold a custom source's address.</summary>
    public static ComponentVersion Sensor(SensorDefinitionStore? store)
    {
        if (store is null)
        {
            return new("Sensor definitions", Unavailable, "Unavailable");
        }

        var failed = store.Status.StartsWith("Couldn't", StringComparison.Ordinal)
            || store.Status.Contains("; update unavailable,", StringComparison.Ordinal);
        var state = failed ? store.Version is null ? "Unavailable" : "Using cached catalogue; update unavailable"
            : !store.Options.Enabled ? "Disabled"
            : store.Version is null ? "Not downloaded"
            : "Enabled";
        return new("Sensor definitions", Clean(store.Version), state);
    }

    /// <param name="appVersion">"2.0.0 (412)" - the version and build.</param>
    public static IReadOnlyList<ComponentVersion> Build(string? appVersion, SensorDefinitionStore? definitions) =>
    [
        new("App", AppVersion(appVersion), "Installed"),
        new("Core", AssemblyVersion(typeof(SensorDefinitionStore).Assembly), "Loaded"),
        Sensor(definitions),
    ];

    /// <summary>"2.0.0 (412)" kept as it is; anything else not version-shaped is unavailable.</summary>
    private static string AppVersion(string? value)
    {
        var match = value is null ? null : AppVersionShape().Match(value.Trim());
        return match is { Success: true } ? match.Value : Clean(value);
    }

    [GeneratedRegex(@"\A[0-9A-Za-z][0-9A-Za-z.+_-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex VersionShape();

    [GeneratedRegex(@"\A[0-9][0-9A-Za-z.+_-]* \([0-9A-Za-z.+_-]+\)\z", RegexOptions.CultureInvariant)]
    private static partial Regex AppVersionShape();
}
