using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Alerts;

/// <summary>What the Alerts tab's dot shows: nothing, or the worst open severity.</summary>
public enum AlertTabSeverity
{
    None,
    Warning,
    Critical,
}

/// <summary>
/// The dot on the Alerts tab (#90): red with a critical alert open, orange
/// with a warning, none otherwise - so there's something to look at from
/// any page. Counts what the app icon badge counts (<see cref="AlertBadge"/>,
/// desktop's Alerts tab badge rules): active alerts, acknowledged ones only
/// with "include acknowledged" on, and nothing with the tab badge turned
/// off. Fed by every alert check - in the app and in the background - as
/// the badge is; the app shell draws it.
/// </summary>
public sealed class AlertTabDot(AlertCountThreshold? threshold = null)
{
    public AlertTabSeverity Severity { get; private set; }

    public int CriticalCount { get; private set; }

    public int WarningCount { get; private set; }

    /// <summary>"3 critical, 2 warning" - what VoiceOver says for the dot; empty with none.</summary>
    public string Description => Severity == AlertTabSeverity.None
        ? string.Empty
        : string.Join(", ", new[]
        {
            CriticalCount > 0 ? $"{CriticalCount} critical" : null,
            WarningCount > 0 ? $"{WarningCount} warning" : null,
        }.OfType<string>());

    /// <summary>Raised when the severity or the counts change, for the shell to redraw the dot.</summary>
    public event EventHandler? Changed;

    public void Update(IEnumerable<Alert> alerts, AppSettings settings)
    {
        // Only what the app icon counts, its severity threshold (#96) included.
        var counted = AlertBadge.Counted(alerts, settings, threshold?.Minimum ?? AlertSeverity.Ok).ToList();

        var critical = counted.Count(a => a.Severity == AlertSeverity.Critical);
        var warning = counted.Count(a => a.Severity == AlertSeverity.Warning);
        var severity = critical > 0 ? AlertTabSeverity.Critical
            : warning > 0 ? AlertTabSeverity.Warning
            : AlertTabSeverity.None;

        if (severity == Severity && critical == CriticalCount && warning == WarningCount)
        {
            return;
        }

        Severity = severity;
        CriticalCount = critical;
        WarningCount = warning;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Signed out: nothing to show.</summary>
    public void Clear() => Update([], new AppSettings { ShowAlertTabBadge = false });
}
