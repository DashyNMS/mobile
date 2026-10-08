using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using CorePlanner = DesktopNMS.Core.Alerting.AlertNotificationPlanner;

namespace DashyNMS.Mobile.Alerts;

/// <summary>
/// Turns a check's alert changes into the phone's notifications: Core
/// decides which deserve one and what each says, the same rules desktop's
/// toasts follow (DashyNMS/desktop#195); this adds what's particular to a
/// phone.
/// </summary>
/// <remarks>
/// Core's <see cref="CorePlanner"/> covers which alert rules notify (#167),
/// the per-severity switches, quiet hours, "notify on recovery/acknowledge", the cap above which one summary
/// replaces many, and not telling you about your own acknowledgements. On
/// top of that each notification gets a tag (so an alert's "Recovered"
/// replaces its "Critical") and where a tap goes, and notifications made out
/// of date by an acknowledgement or recovery are taken away.
/// </remarks>
public static class AlertNotificationPlanner
{
    /// <param name="deviceName">
    /// The device's name as the user chose to see names (Settings → Devices:
    /// hostname, sysName or LibreNMS's display name) - the alert's own
    /// hostname otherwise, which is often an IP (#80).
    /// </param>
    /// <param name="deviceLocation">The device's location, for a summary's "+2 more in Rack 3".</param>
    public static AlertNotificationPlan Plan(
        IReadOnlyList<AlertChange> changes,
        NotificationSettings settings,
        DateTime localNow,
        ISelfActionTracker selfActions,
        Func<Alert, string>? deviceName = null,
        Func<Alert, string?>? deviceLocation = null)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(settings);

        // Whatever gets shown: once an alert is acknowledged or recovered, its
        // "problem" notification is out of date, so take it away - as desktop
        // clears the matching toasts.
        var remove = changes
            .Where(c => c.Kind is AlertChangeKind.Acknowledged or AlertChangeKind.Recovered)
            .Select(c => AlertNotification.TagFor(c.Alert.Id))
            .ToList();

        // Not the first check's quiet: the watcher already takes its first
        // look as the baseline, without notifying.
        var plan = CorePlanner.Plan(
            changes,
            isFirstPoll: false,
            settings,
            localNow,
            deviceName ?? (alert => alert.DisplayHostname),
            deviceLocation,
            change => selfActions.WasSelfInitiated(change.Alert.Id, change.Kind));

        var show = plan.Notifications
            .Select(planned => planned.Change is { Alert: var alert }
                ? new AlertNotification(AlertNotification.TagFor(alert.Id), planned.Title, planned.Body, planned.Detail, planned.Severity, planned.IsProblem, alert.DeviceId, alert.Id)
                : new AlertNotification(AlertNotification.SummaryTag, planned.Title, planned.Body, planned.Detail, planned.Severity, planned.IsProblem, DeviceId: null))
            .ToList();

        // Core leaves out what the notification rules don't allow (#167) and says how many.
        return new AlertNotificationPlan(show, remove) { LeftOutByRuleCount = plan.LeftOutByRuleCount, SuppressionReason = plan.SuppressionReason };
    }
}
