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

    public const string Alerts = "//main/alerts";

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
