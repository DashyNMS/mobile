using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Alerts;

/// <summary>One notification to show, independent of how the platform shows it.</summary>
/// <param name="Tag">
/// Identity on the device: a later notification with the same tag replaces
/// this one, so an alert's "Recovered" takes the place of its "Critical".
/// </param>
/// <param name="IsProblem">Something starting or resuming (as opposed to a recovery or acknowledgement).</param>
/// <param name="DeviceId">The alert's device, or null for a summary of several.</param>
/// <param name="AlertId">Where tapping it goes - that alert's page - or null (a summary) for the alert list.</param>
public sealed record AlertNotification(
    string Tag,
    string Title,
    string Body,
    string? Detail,
    AlertSeverity Severity,
    bool IsProblem,
    int? DeviceId,
    int? AlertId = null)
{
    public const string SummaryTag = "summary";

    public static string TagFor(int alertId) => "alert-" + alertId.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>What one check decided: notifications to show, and ones made stale to take away.</summary>
public sealed record AlertNotificationPlan(IReadOnlyList<AlertNotification> Show, IReadOnlyList<string> Remove)
{
    public static AlertNotificationPlan Empty { get; } = new([], []);
}
