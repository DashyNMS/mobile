using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Dashboard;

/// <summary>A kind of dashboard card - one of desktop's widget types.</summary>
/// <param name="AllowsSeveral">Sensors, Graph and the Top cards: as many as you like, each set up on its own (#87, #103).</param>
public sealed record DashboardCardKind(string Type, string Title, string Description, bool AllowsSeveral = false);

/// <summary>
/// The dashboard's cards, kept in desktop's own <see cref="AppSettings.DashboardWidgets"/>.
/// </summary>
/// <remarks>
/// <para>Desktop lays widgets out on a resizable grid; a phone has one column
/// (or a few on a tablet), so here the list's order is the layout and the grid
/// positions are left alone.</para>
/// <para>Sensors, Graph and Top cards can appear any number of times, each its
/// own widget with its own id, title and set-up - as desktop already allows,
/// so both apps show the same cards (#87, #103). Every other kind shows once. A hidden
/// card is simply not in the list, so hiding one forgets its set-up.</para>
/// <para>Widgets the phone doesn't show - a kind it doesn't know, or a second
/// copy of a once-only kind made on desktop - are kept as they are when the
/// phone saves, so arranging the phone's dashboard never loses desktop's.</para>
/// <para>An empty list means the defaults, which is also what an existing
/// install had before cards could be chosen.</para>
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
    public const string TopInterfaces = DashboardWidgetTypes.TopInterfaces;
    public const string TopErrors = DashboardWidgetTypes.TopErrors;
    public const string TopDevices = DashboardWidgetTypes.TopDevices;

    /// <summary>Every card the phone can show, in the order Customise lists them.</summary>
    public static IReadOnlyList<DashboardCardKind> Kinds { get; } =
    [
        new(AlertsGauge, "Alerts", "Open alerts by severity: critical, warning, OK and acknowledged."),
        new(DeviceStatus, "Devices", "Devices up, down, in maintenance and disabled."),
        new(Alerts, "Needs attention", "The five alerts that most need looking at."),
        new(PinnedDevices, "Pinned devices", "The devices you've pinned, with their state."),
        new(RecentlyViewed, "Recently viewed", "Devices you've opened lately."),
        new(Sensors, "Sensors", "Sensors you pick, coloured against their thresholds.", AllowsSeveral: true),
        new(Graph, "Graph", "A device graph you pick.", AllowsSeveral: true),
        new(Wireless, "Wireless", "Access points and clients on each wireless controller."),
        new(TopInterfaces, "Top interfaces", "The busiest ports across the network.", AllowsSeveral: true),
        new(TopErrors, "Top errors", "The ports with the most errors.", AllowsSeveral: true),
        new(TopDevices, "Top devices", "The devices moving the most traffic.", AllowsSeveral: true),
    ];

    /// <summary>The dashboard as it was before cards could be chosen.</summary>
    public static IReadOnlyList<string> DefaultTypes { get; } = [AlertsGauge, DeviceStatus, Alerts, PinnedDevices, RecentlyViewed];

    public static DashboardCardKind KindOf(string type) => Kinds.First(k => k.Type == type);

    public static bool IsKnown(string? type) => Kinds.Any(k => k.Type == type);

    /// <summary>
    /// The cards to show, in order: desktop's list, the kinds a phone knows -
    /// each once-only kind once, Sensors and Graph as often as they appear -
    /// or the defaults.
    /// </summary>
    public static IReadOnlyList<DashboardWidget> Current(AppSettings settings)
    {
        var seen = new HashSet<string>();
        var shown = settings.DashboardWidgets
            .Where(w => IsKnown(w.WidgetType) && (KindOf(w.WidgetType).AllowsSeveral || seen.Add(w.WidgetType)))
            .ToList();

        return shown.Count > 0 ? shown : DefaultTypes.Select(New).ToList();
    }

    /// <summary>The card with <paramref name="id"/>, if it's showing.</summary>
    public static DashboardWidget? Find(AppSettings settings, string? id) =>
        id is null ? null : Current(settings).FirstOrDefault(w => w.Id == id);

    /// <summary>
    /// Saves which cards show and in what order. Each is the widget itself, so
    /// its set-up (a Graph's device, a Sensors card's sensors, its title) goes
    /// with it; whatever the phone doesn't show is kept after them, untouched.
    /// </summary>
    public static void Save(AppSettings settings, IEnumerable<DashboardWidget> shown)
    {
        var listed = Current(settings).Select(w => w.Id).ToHashSet();
        var kept = settings.DashboardWidgets.Where(w => !listed.Contains(w.Id));
        settings.DashboardWidgets = shown.DistinctBy(w => w.Id).Concat(kept).ToList();
    }

    /// <summary>
    /// A new card of <paramref name="type"/> at the end - another Sensors or
    /// Graph card, or a once-only kind if it isn't showing yet (else that one).
    /// </summary>
    public static DashboardWidget Add(AppSettings settings, string type)
    {
        var current = Current(settings).ToList();
        if (!KindOf(type).AllowsSeveral && current.FirstOrDefault(w => w.WidgetType == type) is { } existing)
        {
            return existing;
        }

        var widget = New(type);
        current.Add(widget);
        Save(settings, current);
        return widget;
    }

    /// <summary>A card of <paramref name="type"/> that isn't on the dashboard yet.</summary>
    public static DashboardWidget New(string type) => new() { WidgetType = type, Title = KindOf(type).Title };

    /// <summary>
    /// The title a card shows: its own, once given one - else the kind's
    /// ("Sensors"), or for a set-up graph, the graph and its device.
    /// </summary>
    public static bool HasOwnTitle(DashboardWidget widget) =>
        !string.IsNullOrWhiteSpace(widget.Title) && IsKnown(widget.WidgetType) && widget.Title != KindOf(widget.WidgetType).Title;
}
