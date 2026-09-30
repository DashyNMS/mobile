namespace DashyNMS.Mobile.Services;

/// <summary>A page the More tab lists (#68), any of which can be pinned to the tab bar.</summary>
public enum AppPage
{
    Devices,
    Alerts,
    Health,
    Map,
    NetworkMap,
    Neighbours,
    GroupsLocations,
    Logs,
    Graylog,
    Settings,
}

/// <summary>Where a phone-only choice is kept: MAUI's Preferences in the app, memory in tests.</summary>
public interface IAppPreferences
{
    string? Get(string key);

    void Set(string key, string? value);
}

/// <summary>Kept in memory only: the default until the app head supplies the platform's, and in tests.</summary>
public sealed class InMemoryPreferences : IAppPreferences
{
    private readonly Dictionary<string, string?> _values = [];

    public string? Get(string key) => _values.GetValueOrDefault(key);

    public void Set(string key, string? value) => _values[key] = value;
}

/// <summary>
/// Which pages sit on the tab bar between Dashboard and More (#68): up to
/// three, Devices, Alerts and Health until the user chooses. Dashboard and
/// More are always there, so the bar never holds more than five.
/// </summary>
/// <remarks>
/// A phone preference rather than one of desktop's settings: desktop has no
/// tab bar to share it with. Pinned pages keep <see cref="AppPage"/>'s order,
/// so the bar reads the same whichever order they were pinned in.
/// </remarks>
public sealed class TabPins
{
    public const int MaxPinned = 3;

    internal const string Key = "tabpins";

    private readonly IAppPreferences _preferences;

    public TabPins(IAppPreferences preferences)
    {
        _preferences = preferences;
    }

    public static IReadOnlyList<AppPage> Defaults { get; } = [AppPage.Devices, AppPage.Alerts, AppPage.Health];

    /// <summary>Raised after a pin changes, for the tab bar to rebuild.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// What's pinned, in <see cref="AppPage"/> order. Never chosen: the
    /// defaults; chosen to have none: none. Unknown names (a page since
    /// removed) are skipped.
    /// </summary>
    public IReadOnlyList<AppPage> Pinned
    {
        get
        {
            var saved = _preferences.Get(Key);
            if (saved is null)
            {
                return Defaults;
            }

            return saved.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(name => Enum.TryParse<AppPage>(name, out var page) && Enum.IsDefined(page) ? page : (AppPage?)null)
                .OfType<AppPage>()
                .Distinct()
                .Order()
                .Take(MaxPinned)
                .ToList();
        }
    }

    public bool IsPinned(AppPage page) => Pinned.Contains(page);

    /// <summary>Room for another: fewer than <see cref="MaxPinned"/> are pinned.</summary>
    public bool CanPinMore => Pinned.Count < MaxPinned;

    /// <summary>
    /// Pins <paramref name="page"/>, or unpins it if it is. False when the bar
    /// is full and nothing changed - the caller says so.
    /// </summary>
    public bool Toggle(AppPage page)
    {
        var pinned = Pinned.ToList();
        if (!pinned.Remove(page))
        {
            if (pinned.Count >= MaxPinned)
            {
                return false;
            }

            pinned.Add(page);
        }

        _preferences.Set(Key, string.Join(',', pinned.Order()));
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}

/// <summary>Each <see cref="AppPage"/>'s title, line in More, icon and routes.</summary>
public static class AppPages
{
    public static IReadOnlyList<AppPage> All { get; } = Enum.GetValues<AppPage>();

    public static string Title(AppPage page) => page switch
    {
        AppPage.Devices => "Devices",
        AppPage.Alerts => "Alerts",
        AppPage.Health => "Health",
        AppPage.Map => "Map",
        AppPage.NetworkMap => "Network map",
        AppPage.Neighbours => "Neighbours",
        AppPage.GroupsLocations => "Groups & locations",
        AppPage.Logs => "Event and alert logs",
        AppPage.Graylog => "Graylog",
        AppPage.Settings => "Settings",
        _ => page.ToString(),
    };

    /// <summary>A shorter title for the tab bar, where there's room for about ten characters.</summary>
    public static string TabTitle(AppPage page) => page switch
    {
        AppPage.GroupsLocations => "Groups",
        AppPage.NetworkMap => "Network",
        AppPage.Logs => "Logs",
        _ => Title(page),
    };

    public static string Description(AppPage page) => page switch
    {
        AppPage.Devices => "Every device, with filters and pins",
        AppPage.Alerts => "Open alerts, most severe first",
        AppPage.Health => "Sensors across the network",
        AppPage.Map => "Devices by location",
        AppPage.NetworkMap => "What's connected to what, as a diagram",
        AppPage.Neighbours => "Every CDP/LLDP link",
        AppPage.GroupsLocations => "Device counts and what's down",
        AppPage.Logs => "The whole network, newest first",
        AppPage.Graylog => "Messages from your Graylog server",
        AppPage.Settings => "Server, notifications, thresholds and more",
        _ => string.Empty,
    };

    /// <summary>The More page's headings: Monitor, Network, Logs, App.</summary>
    public static string Group(AppPage page) => page switch
    {
        AppPage.Devices or AppPage.Alerts or AppPage.Health => "Monitor",
        AppPage.Map or AppPage.NetworkMap or AppPage.Neighbours or AppPage.GroupsLocations => "Network",
        AppPage.Logs or AppPage.Graylog => "Logs",
        _ => "App",
    };

    /// <summary>The tab bar icon, one of Resources/Images' tab_*.svg.</summary>
    public static string Icon(AppPage page) => page switch
    {
        AppPage.GroupsLocations => "tab_groups.png",
        _ => "tab_" + page.ToString().ToLowerInvariant() + ".png",
    };

    /// <summary>
    /// The page's route segment as a tab under <see cref="Routes.Main"/>.
    /// Devices, Alerts, Health and Settings keep the names they had as fixed
    /// tabs; the rest are "tab" + their pushed route, since a registered route
    /// can't share a name with a Shell element.
    /// </summary>
    public static string TabRoute(AppPage page) => page switch
    {
        AppPage.Devices => "devices",
        AppPage.Alerts => "alerts",
        AppPage.Health => "health",
        AppPage.Settings => "settings",
        _ => "tab" + PushRoute(page),
    };

    /// <summary>The route that pushes the page, for when it isn't pinned (or from More either way).</summary>
    public static string PushRoute(AppPage page) => page switch
    {
        AppPage.Map => Routes.Map,
        AppPage.NetworkMap => Routes.NetworkMap,
        AppPage.Neighbours => Routes.Neighbours,
        AppPage.GroupsLocations => Routes.GroupsLocations,
        AppPage.Logs => Routes.Logs,
        AppPage.Graylog => Routes.Graylog,
        _ => page.ToString().ToLowerInvariant() + "page",
    };

    /// <summary>
    /// Where a route really goes with these pins. A tab route
    /// ("//main/alerts") for a page that isn't pinned becomes a push onto the
    /// More tab ("//main/more/alertspage"), so view models can keep asking for
    /// <see cref="Routes.Alerts"/> whatever's on the bar. Anything else is unchanged.
    /// </summary>
    public static string Resolve(string route, TabPins pins)
    {
        var prefix = Routes.Main + "/";
        if (!route.StartsWith(prefix, StringComparison.Ordinal))
        {
            return route;
        }

        var segment = route[prefix.Length..].Split('/', '?')[0];
        foreach (var page in All)
        {
            if (TabRoute(page) == segment && !pins.IsPinned(page))
            {
                return Routes.More + "/" + PushRoute(page) + route[(prefix.Length + segment.Length)..];
            }
        }

        return route;
    }
}
