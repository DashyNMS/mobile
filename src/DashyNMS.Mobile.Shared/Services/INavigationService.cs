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

    public const string DeviceGraphs = "devicegraphs";

    public const string Maintenance = "maintenance";

    /// <summary>Query attribute: a <c>DeviceSection</c>.</summary>
    public const string SectionParameter = "section";

    /// <summary>Query attribute: the device's name, for titles.</summary>
    public const string DeviceNameParameter = "name";
}
