using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Alerts;

/// <summary>
/// Decides which alert changes become notifications, and what they say.
/// </summary>
/// <remarks>
/// The same rules as desktop's AlertNotificationService (which lives in the
/// WPF app, so can't be shared as-is): the per-severity switches, quiet hours,
/// "notify on recovery/acknowledge", the cap above which one summary replaces
/// many notifications, and not telling you about your own acknowledgements.
/// If desktop's rules change, change these to match - or better, move them
/// into Core so both apps share one copy.
/// </remarks>
public static class AlertNotificationPlanner
{
    /// <param name="deviceName">
    /// The device's name as the user chose to see names (Settings → Devices:
    /// hostname, sysName or LibreNMS's display name) - the alert's own
    /// hostname otherwise, which is often an IP (#80).
    /// </param>
    public static AlertNotificationPlan Plan(
        IReadOnlyList<AlertChange> changes,
        NotificationSettings settings,
        DateTime localNow,
        ISelfActionTracker selfActions,
        Func<Alert, string>? deviceName = null)
    {
        var name = deviceName ?? (alert => alert.DisplayHostname);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(settings);

        // Whatever gets shown: once an alert is acknowledged or recovered, its
        // "problem" notification is out of date, so take it away - as desktop
        // clears the matching toasts.
        var remove = changes
            .Where(c => c.Kind is AlertChangeKind.Acknowledged or AlertChangeKind.Recovered)
            .Select(c => AlertNotification.TagFor(c.Alert.Id))
            .ToList();

        if (!settings.Enabled || changes.Count == 0)
        {
            return new AlertNotificationPlan([], remove);
        }

        var notifiable = changes.Where(c => ShouldNotify(c, settings, localNow, selfActions)).ToList();
        if (notifiable.Count == 0)
        {
            return new AlertNotificationPlan([], remove);
        }

        if (notifiable.Count > settings.MaxToastsPerPoll)
        {
            return new AlertNotificationPlan([Summary(notifiable, name)], remove);
        }

        return new AlertNotificationPlan(notifiable.Select(c => ForChange(c, name)).ToList(), remove);
    }

    private static bool ShouldNotify(AlertChange change, NotificationSettings settings, DateTime localNow, ISelfActionTracker selfActions)
    {
        if (change.Kind is AlertChangeKind.Acknowledged or AlertChangeKind.Unacknowledged
            && selfActions.WasSelfInitiated(change.Alert.Id, change.Kind))
        {
            return false;
        }

        var severity = change.Alert.Severity;
        if (settings.IsInQuietHours(localNow, severity))
        {
            return false;
        }

        return change.Kind switch
        {
            AlertChangeKind.New or AlertChangeKind.Reopened or AlertChangeKind.Unacknowledged
                => settings.ForSeverity(severity).Enabled,
            AlertChangeKind.Recovered => settings.NotifyOnRecovery,
            AlertChangeKind.Acknowledged => settings.NotifyOnAcknowledge,
            _ => false,
        };
    }

    private static AlertNotification ForChange(AlertChange change, Func<Alert, string> name)
    {
        var alert = change.Alert;
        var device = name(alert);

        var title = change.Kind switch
        {
            AlertChangeKind.Recovered => $"Recovered: {device}",
            AlertChangeKind.Acknowledged => $"Acknowledged: {device}",
            AlertChangeKind.Unacknowledged => $"Unacknowledged: {device}",
            AlertChangeKind.Reopened => $"{alert.Severity.ToDisplayString()} again: {device}",
            _ => $"{alert.Severity.ToDisplayString()}: {device}",
        };

        var detail = string.IsNullOrWhiteSpace(alert.Note) ? null : FirstLine(alert.Note!);

        return new AlertNotification(
            AlertNotification.TagFor(alert.Id),
            title,
            alert.DisplayRuleName,
            detail,
            alert.Severity,
            change.IsProblem,
            alert.DeviceId,
            alert.Id);
    }

    private static AlertNotification Summary(IReadOnlyList<AlertChange> changes, Func<Alert, string> name)
    {
        var problems = changes.Where(c => c.IsProblem).ToList();

        // "3 new critical alerts" / "core-sw-02 — High temperature" / "+2 more".
        var (title, body, detail) = problems.Count > 0
            ? AlertSummaryText.Build(problems
                .Select(c => new AlertSummaryItem(c.Alert.Severity, name(c.Alert), c.Alert.DisplayRuleName, Location: null))
                .ToList())
            : ($"{changes.Count} alert updates", "See DashyNMS for details.", null);

        var worst = changes.Max(c => c.Alert.Severity);

        return new AlertNotification(AlertNotification.SummaryTag, title, body, detail, worst, problems.Count > 0, DeviceId: null);
    }

    private static string FirstLine(string text)
    {
        var index = text.IndexOfAny(['\r', '\n']);
        var line = index >= 0 ? text[..index] : text;
        return line.Length <= 120 ? line : line[..117] + "...";
    }
}
