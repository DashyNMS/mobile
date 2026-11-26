using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Help;

/// <summary>A page of the guide, and the heading on it to open at.</summary>
public sealed record HelpTopic(string PageId, string? Anchor = null);

/// <summary>
/// Which page of the guide explains each part of the app (#668). A page's id
/// is its name on the wiki; a test holds every one of these to a page and a
/// heading the guide has.
/// </summary>
public static class HelpTopics
{
    private const string SettingsPage = "Change-your-settings";

    /// <summary>The sign-in page, where nothing else explains itself yet.</summary>
    public static HelpTopic SignIn { get; } = new("Getting-started");

    public static HelpTopic Alerts { get; } = new("Investigate-and-acknowledge-alerts");

    public static HelpTopic Intelligence { get; } = new("Ask-about-your-network-with-Intelligence");

    public static HelpTopic Thresholds { get; } = new(SettingsPage, "health-thresholds");

    public static HelpTopic SensorDefinitions { get; } = new(SettingsPage, "sensor-definitions");

    public static HelpTopic Graylog { get; } = new("Connect-Graylog-and-Unimus", "connect-graylog");

    public static HelpTopic Unimus { get; } = new("Connect-Graylog-and-Unimus", "connect-unimus");

    public static HelpTopic For(AppPage page) => page switch
    {
        AppPage.Devices or AppPage.GroupsLocations => new("Find-and-investigate-a-device"),
        AppPage.Alerts => Alerts,
        AppPage.AlertRules => new("Investigate-and-acknowledge-alerts", "see-what-a-rule-does"),
        AppPage.Health => new("Use-maps-neighbours-and-health-readings", "check-sensor-readings"),
        AppPage.Map => new("Use-maps-neighbours-and-health-readings", "see-your-sites-on-a-map"),
        AppPage.NetworkMap => new("Use-maps-neighbours-and-health-readings", "see-whats-connected"),
        AppPage.Neighbours => new("Use-maps-neighbours-and-health-readings", "check-links-between-devices"),
        AppPage.Logs => new("Read-logs"),
        AppPage.Graylog => new("Read-logs", "graylog-messages"),
        AppPage.Intelligence => Intelligence,
        AppPage.NetworkSearch => new("Find-where-something-is-connected"),
        _ => new(SettingsPage),
    };

    public static HelpTopic For(SettingsSection section) => section switch
    {
        SettingsSection.Server => new(SettingsPage, "server"),
        SettingsSection.Appearance or SettingsSection.Devices => new(SettingsPage, "tabs-and-appearance"),
        SettingsSection.AlertChecks => new(SettingsPage, "alert-checks-and-notifications"),
        SettingsSection.Notifications => new("Choose-which-alerts-notify-you"),
        SettingsSection.LockScreen => new("Add-home-screen-and-lock-screen-widgets", "hide-details-on-the-lock-screen"),
        _ => new(SettingsPage, "diagnostics"),
    };

    /// <summary>Every topic there is, for the test that the guide has them all.</summary>
    internal static IEnumerable<HelpTopic> All =>
    [
        SignIn, Alerts, Intelligence, Thresholds, SensorDefinitions, Graylog, Unimus,
        .. Enum.GetValues<AppPage>().Select(For),
        .. Enum.GetValues<SettingsSection>().Select(For),
    ];
}
