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

    /// <summary>
    /// What a locked phone shows instead, when details are hidden there (#8):
    /// how serious, but no device, rule or note - those can be sensitive
    /// detail about the network for anyone who picks the phone up.
    /// </summary>
    public string LockScreenText => Tag == SummaryTag
        ? "New alerts - unlock to see them"
        : !IsProblem
            ? "An alert has changed - unlock to see it"
            : Severity switch
            {
                AlertSeverity.Critical => "Critical alert - unlock to see it",
                AlertSeverity.Warning => "Warning - unlock to see it",
                _ => "New alert - unlock to see it",
            };
}

/// <summary>
/// Whether alert notifications hide their details on the lock screen (#8).
/// Android shows them unless told not to, so it has a setting; iPhone hides
/// previews while locked by default, under the system's own Show Previews.
/// </summary>
public interface INotificationPrivacy
{
    /// <summary>Whether this platform offers the setting at all.</summary>
    bool CanHideLockScreenDetails { get; }

    /// <summary>On by default: the safer choice for a phone that might be picked up.</summary>
    bool HideLockScreenDetails { get; set; }
}

/// <summary>For platforms (and tests) where the system decides.</summary>
public sealed class SystemNotificationPrivacy : INotificationPrivacy
{
    public bool CanHideLockScreenDetails => false;

    public bool HideLockScreenDetails { get; set; } = true;
}

/// <summary>What one check decided: notifications to show, and ones made stale to take away.</summary>
public sealed record AlertNotificationPlan(IReadOnlyList<AlertNotification> Show, IReadOnlyList<string> Remove)
{
    public static AlertNotificationPlan Empty { get; } = new([], []);
}
