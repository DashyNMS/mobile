namespace DashyNMS.Mobile.Services;

/// <summary>Page navigation, so view models don't depend on MAUI's Shell.</summary>
public interface INavigationService
{
    /// <summary>Goes to <paramref name="route"/> (one of <see cref="Routes"/>), passing <paramref name="parameters"/> as query attributes.</summary>
    Task GoToAsync(string route, IDictionary<string, object>? parameters = null);
}

/// <summary>Shell routes. Absolute ("//...") routes replace the navigation stack.</summary>
public static class Routes
{
    public const string SignIn = "//signin";

    public const string Main = "//main";

    /// <summary>Back one page.</summary>
    public const string Back = "..";

    /// <summary>
    /// The Alerts page. Like the other tab routes, it's the tab when pinned
    /// and pushed onto More when not - see <see cref="AppPages.Resolve"/>.
    /// </summary>
    public const string Alerts = "//main/alerts";

    public const string Health = "//main/health";

    public const string Settings = "//main/settings";

    /// <summary>The More tab (#68): every page, and which are pinned to the tab bar.</summary>
    public const string More = "//main/more";

    /// <summary>A Settings section's own page (#67) - with <see cref="SettingsSectionParameter"/>.</summary>
    public const string SettingsSection = "settingssection";

    /// <summary>Query attribute: a <c>SettingsSection</c>, for <see cref="SettingsSection"/>.</summary>
    public const string SettingsSectionParameter = "section";

    /// <summary>The Devices tab - with <see cref="GroupParameter"/> or <see cref="LocationParameter"/>, filtered to it.</summary>
    public const string Devices = "//main/devices";

    /// <summary>Choosing and ordering the dashboard's cards.</summary>
    public const string CustomiseDashboard = "customisedashboard";

    /// <summary>Choosing the dashboard Sensors card's sensors.</summary>
    public const string PickSensors = "picksensors";

    /// <summary>Choosing the dashboard Graph card's device and graph.</summary>
    public const string PickGraph = "pickgraph";

    /// <summary>The geographical map of locations and their devices.</summary>
    public const string Map = "map";

    /// <summary>The network map: devices and the links between them, as a diagram (#86).</summary>
    public const string NetworkMap = "networkmap";

    /// <summary>Every CDP/LLDP link across the network.</summary>
    public const string Neighbours = "neighbours";

    /// <summary>The network-wide event and alert logs.</summary>
    public const string Logs = "logs";

    /// <summary>Graylog messages - every device's, or with <see cref="DeviceIdParameter"/> one device's.</summary>
    public const string Graylog = "graylog";

    /// <summary>Settings, Graylog.</summary>
    public const string GraylogSettings = "graylogsettings";

    /// <summary>Health thresholds, pushed from Settings.</summary>
    public const string Thresholds = "thresholds";

    /// <summary>Groups &amp; locations, pushed from the Devices tab.</summary>
    public const string GroupsLocations = "groupslocations";

    /// <summary>Query attribute: a device group's name (or the "not in a group" key), for <see cref="Devices"/>.</summary>
    public const string GroupParameter = "group";

    /// <summary>Query attribute: a location's name, for <see cref="Devices"/>.</summary>
    public const string LocationParameter = "location";

    /// <summary>Query attribute: a <c>DeviceState</c> to show only, for <see cref="Devices"/>.</summary>
    public const string StateParameter = "state";

    /// <summary>Query attribute: "critical", "warning" or "acknowledged" to show only, for <see cref="Alerts"/>.</summary>
    public const string AlertFilterParameter = "show";

    public const string DeviceDetail = "device";

    /// <summary>Query attribute carrying the device id for <see cref="DeviceDetail"/>.</summary>
    public const string DeviceIdParameter = "id";

    /// <summary>One Device View section - with <see cref="DeviceIdParameter"/> and <see cref="SectionParameter"/>.</summary>
    public const string DeviceSection = "devicesection";

    /// <summary>A device's graphs - or, with <see cref="PortParameter"/>, one port's.</summary>
    public const string DeviceGraphs = "devicegraphs";

    public const string Maintenance = "maintenance";

    /// <summary>One alert - with <see cref="AlertIdParameter"/>, and <see cref="DeviceIdParameter"/> when known.</summary>
    public const string AlertDetail = "alert";

    /// <summary>Query attribute: the alert id for <see cref="AlertDetail"/>.</summary>
    public const string AlertIdParameter = "alertid";

    /// <summary>Query attribute: a <c>DeviceSection</c>.</summary>
    public const string SectionParameter = "section";

    /// <summary>Query attribute: the device's name, for titles.</summary>
    public const string DeviceNameParameter = "name";

    /// <summary>Query attribute: a port's ifName, for <see cref="DeviceGraphs"/>.</summary>
    public const string PortParameter = "port";

    /// <summary>Query attribute: the port's display name, for titles.</summary>
    public const string PortNameParameter = "portname";
}

public static class NavigationServiceExtensions
{
    /// <summary>An alert's own page - what tapping an alert anywhere opens.</summary>
    public static Task GoToAlertAsync(this INavigationService navigation, DesktopNMS.Core.Models.Alert alert) =>
        navigation.GoToAsync(Routes.AlertDetail, new Dictionary<string, object>
        {
            [Routes.AlertIdParameter] = alert.Id,
            [Routes.DeviceIdParameter] = alert.DeviceId,
        });
}
