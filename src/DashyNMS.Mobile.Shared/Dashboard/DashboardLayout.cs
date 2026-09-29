using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Dashboard;

/// <summary>A kind of dashboard card - one of desktop's widget types.</summary>
public sealed record DashboardCardKind(string Type, string Title, string Description);

/// <summary>
/// The dashboard's cards, kept in desktop's own <see cref="AppSettings.DashboardWidgets"/>.
/// </summary>
/// <remarks>
/// Desktop lays widgets out on a resizable grid; a phone has one column, so
/// here the list's order is the layout and the grid positions are left alone.
/// Each kind appears at most once (desktop allows several Sensors or Graph
/// widgets; one of each is plenty on a phone), and a hidden card is simply
/// not in the list - so hiding a Graph or Sensors card forgets its set-up.
/// An empty list means the defaults, which is also what an existing install
/// had before cards could be chosen.
/// </remarks>
public static class DashboardLayout
{
    public const string AlertsGauge = "AlertsGauge";
    public const string DeviceStatus = "DeviceStatus";
    public const string Alerts = "Alerts";
    public const string PinnedDevices = "PinnedDevices";
    public const string RecentlyViewed = "RecentlyViewed";
    public const string Sensors = "Sensors";
    public const string Graph = "Graph";
    public const string Wireless = "Wireless";

    /// <summary>Every card the phone can show, in the order Customise lists them.</summary>
    public static IReadOnlyList<DashboardCardKind> Kinds { get; } =
    [
        new(AlertsGauge, "Alerts", "Open alerts by severity: critical, warning, OK and acknowledged."),
        new(DeviceStatus, "Devices", "Devices up, down, in maintenance and disabled."),
        new(Alerts, "Needs attention", "The five alerts that most need looking at."),
        new(PinnedDevices, "Pinned devices", "The devices you've pinned, with their state."),
        new(RecentlyViewed, "Recently viewed", "Devices you've opened lately."),
        new(Sensors, "Sensors", "Sensors you pick, coloured against their thresholds."),
        new(Graph, "Graph", "One device graph you pick."),
        new(Wireless, "Wireless", "Access points and clients on each wireless controller."),
    ];

    /// <summary>The dashboard as it was before cards could be chosen.</summary>
    public static IReadOnlyList<string> DefaultTypes { get; } = [AlertsGauge, DeviceStatus, Alerts, PinnedDevices, RecentlyViewed];

    public static DashboardCardKind KindOf(string type) => Kinds.First(k => k.Type == type);

    /// <summary>The cards to show, in order: desktop's list, the kinds a phone knows, each once - or the defaults.</summary>
    public static IReadOnlyList<DashboardWidget> Current(AppSettings settings)
    {
        var known = settings.DashboardWidgets
            .Where(w => Kinds.Any(k => k.Type == w.WidgetType))
            .DistinctBy(w => w.WidgetType)
            .ToList();

        return known.Count > 0 ? known : DefaultTypes.Select(New).ToList();
    }

    /// <summary>
    /// Saves which cards show and in what order, keeping each shown card's
    /// own set-up (a Graph's device, a Sensors card's sensors).
    /// </summary>
    public static void Save(AppSettings settings, IEnumerable<string> shownTypes)
    {
        var existing = Current(settings).ToDictionary(w => w.WidgetType);
        settings.DashboardWidgets = shownTypes
            .Distinct()
            .Select(type => existing.GetValueOrDefault(type) ?? New(type))
            .ToList();
    }

    /// <summary>The card of <paramref name="type"/>, added at the end if it isn't showing - for setting one up.</summary>
    public static DashboardWidget Ensure(AppSettings settings, string type)
    {
        var current = Current(settings).ToList();
        var widget = current.FirstOrDefault(w => w.WidgetType == type);
        if (widget is null)
        {
            widget = New(type);
            current.Add(widget);
        }

        settings.DashboardWidgets = current;
        return widget;
    }

    private static DashboardWidget New(string type) => new() { WidgetType = type, Title = KindOf(type).Title };
}
