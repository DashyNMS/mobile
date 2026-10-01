using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Alerts;

/// <summary>
/// The least serious alert the app icon's count and the Alerts tab's dot
/// take notice of (#96): Critical only, Warning and above, or everything (OK
/// and above, the default and how it always worked) - so a phone full of
/// warnings needn't show the same red number as an outage.
/// </summary>
/// <remarks>
/// A phone preference, as <see cref="Services.IAppearance"/> is: desktop's
/// Alerts tab badge has no such setting to share. "And above" follows
/// desktop's severity order (<see cref="AlertSeverityExtensions.SortRank"/>).
/// </remarks>
public sealed class AlertCountThreshold(Services.IAppPreferences preferences)
{
    internal const string Key = "alerts.count.minimum";

    /// <summary>The choices, most inclusive first, as Settings lists them.</summary>
    public static IReadOnlyList<AlertSeverity> Choices { get; } = [AlertSeverity.Ok, AlertSeverity.Warning, AlertSeverity.Critical];

    public AlertSeverity Minimum
    {
        get => Enum.TryParse<AlertSeverity>(preferences.Get(Key), out var value) && Choices.Contains(value) ? value : AlertSeverity.Ok;
        set => preferences.Set(Key, value.ToString());
    }

    /// <summary>"Critical only", "Warning and above", "OK and above".</summary>
    public static string Describe(AlertSeverity minimum) => minimum switch
    {
        AlertSeverity.Critical => "Critical only",
        AlertSeverity.Warning => "Warning and above",
        _ => "OK and above",
    };

    /// <summary>
    /// Whether <paramref name="alert"/> is serious enough to count. At OK and
    /// above everything does, an alert of no known severity included, as before.
    /// </summary>
    public bool Counts(Alert alert) => Counts(alert, Minimum);

    internal static bool Counts(Alert alert, AlertSeverity minimum) =>
        minimum.SortRank() <= AlertSeverity.Ok.SortRank() || alert.Severity.SortRank() >= minimum.SortRank();
}
