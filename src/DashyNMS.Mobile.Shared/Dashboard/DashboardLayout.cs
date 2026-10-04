using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Dashboard;

/// <summary>What a card's small preview in the card picker looks like - a hint of its shape, as desktop's widget picker.</summary>
public enum CardPreview
{
    /// <summary>Rows with a status dot (alerts, devices).</summary>
    List,

    /// <summary>Four count tiles (Alerts, Devices).</summary>
    Tiles,

    /// <summary>A big number over a line (Wireless).</summary>
    Total,

    /// <summary>Rows with bars of falling length (the Top cards, Sensors).</summary>
    Ranked,

    /// <summary>The same, in red and amber (Top errors).</summary>
    RankedWarning,

    /// <summary>A line graph.</summary>
    Graph,
}

/// <summary>A kind of dashboard card - one of desktop's widget types.</summary>
/// <param name="Title">The phone's name for it, and the card's title.</param>
/// <param name="Description">What it shows - desktop's widget picker's words.</param>
/// <param name="Category">Where the card picker files it: desktop's categories.</param>
/// <param name="AllowsSeveral">Sensors, Graph and the Top cards: as many as you like, each set up on its own (#87, #103).</param>
/// <param name="DesktopTitle">Desktop's name where it differs ("Alerts gauge") - a card with it hasn't been given a title of its own.</param>
public sealed record DashboardCardKind(
    string Type,
    string Title,
    string Description,
    string Category,
    CardPreview Preview,
    bool AllowsSeveral = false,
    string? DesktopTitle = null);

/// <summary>
/// The dashboard's cards, kept in desktop's own <see cref="AppSettings.DashboardWidgets"/>.
/// </summary>
/// <remarks>
/// <para>Desktop lays widgets out on a resizable grid; a phone has one column
/// (or a few on a tablet), so here the list's order is the layout and the grid
/// positions are left alone.</para>
/// <para>Sensors, Graph and Top cards can appear any number of times, each its
/// own widget with its own id, title and set-up - as desktop already allows,
/// so both apps show the same cards (#87, #103). Every other kind shows once. A removed
/// card is simply not in the list, so removing one forgets its set-up.</para>
/// <para>Widgets the phone doesn't show - a kind it doesn't know, or a second
/// copy of a once-only kind made on desktop - are kept as they are when the
/// phone saves, so arranging the phone's dashboard never loses desktop's.</para>
/// <para>No cards is an empty dashboard, as on desktop: the welcome card, with
/// the starter dashboard a tap away (#140).</para>
/// </remarks>
public static class DashboardLayout
{
    public const string AlertsGauge = DashboardWidgetTypes.AlertsGauge;
    public const string DeviceStatus = DashboardWidgetTypes.DeviceStatus;
    public const string Alerts = DashboardWidgetTypes.Alerts;
    public const string PinnedDevices = DashboardWidgetTypes.PinnedDevices;
    public const string RecentlyViewed = DashboardWidgetTypes.RecentlyViewed;
    public const string Sensors = DashboardWidgetTypes.Sensors;
    public const string Graph = DashboardWidgetTypes.Graph;
    public const string Wireless = DashboardWidgetTypes.Wireless;
    public const string TopInterfaces = DashboardWidgetTypes.TopInterfaces;
    public const string TopErrors = DashboardWidgetTypes.TopErrors;
    public const string TopDevices = DashboardWidgetTypes.TopDevices;

    /// <summary>The card picker's categories, in desktop's order - without its Logs, as the phone has no log cards.</summary>
    public static IReadOnlyList<string> Categories { get; } = ["Alerts", "Devices", "Traffic", "Sensors and graphs"];

    /// <summary>
    /// Every card the phone can show, in the order the card picker lists them.
    /// Alerts and Devices keep the phone's names and count tiles, which work
    /// better on a phone than desktop's gauges (#140).
    /// </summary>
    public static IReadOnlyList<DashboardCardKind> Kinds { get; } =
    [
        new(AlertsGauge, "Alerts", "Critical, warning, OK and acknowledged counts", "Alerts", CardPreview.Tiles, DesktopTitle: "Alerts gauge"),
        new(Alerts, "Needs attention", "A live feed of active alerts", "Alerts", CardPreview.List, DesktopTitle: "Alerts"),
        new(DeviceStatus, "Devices", "Up, down, maintenance and disabled counts", "Devices", CardPreview.Tiles, DesktopTitle: "Device status"),
        new(PinnedDevices, "Pinned devices", "Devices you've pinned on the Devices tab", "Devices", CardPreview.List),
        new(RecentlyViewed, "Recently viewed", "A quick way back into devices you just looked at", "Devices", CardPreview.List),
        new(Wireless, "Wireless", "APs and clients on each wireless controller", "Devices", CardPreview.Total),
        new(TopInterfaces, "Top interfaces", "The busiest ports in the fleet, in and out", "Traffic", CardPreview.Ranked, AllowsSeveral: true),
        new(TopErrors, "Top errors", "The ports with the most errors per second", "Traffic", CardPreview.RankedWarning, AllowsSeveral: true),
        new(TopDevices, "Top devices", "The devices moving the most traffic", "Traffic", CardPreview.Ranked, AllowsSeveral: true),
        new(Sensors, "Sensors", "Track temperature, signal, fan speed and more", "Sensors and graphs", CardPreview.Ranked, AllowsSeveral: true),
        new(Graph, "Graph", "Any device graph, over a time range you pick", "Sensors and graphs", CardPreview.Graph, AllowsSeveral: true),
    ];

    /// <summary>
    /// The phone's dashboard before the welcome card (#140) - what an install
    /// that never changed its cards has been showing, and is given to keep.
    /// </summary>
    internal static IReadOnlyList<string> PreviousDefaultTypes { get; } = [AlertsGauge, DeviceStatus, Alerts, PinnedDevices, RecentlyViewed];

    /// <summary>"Alerts, Devices, Needs attention, Top interfaces and Recently viewed" - the welcome card's note, in the phone's names.</summary>
    public static string StarterContents
    {
        get
        {
            var names = StarterDashboard.Create().Select(w => KindOf(w.WidgetType).Title).ToList();
            return string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
        }
    }

    public static DashboardCardKind KindOf(string type) => Kinds.First(k => k.Type == type);

    public static bool IsKnown(string? type) => Kinds.Any(k => k.Type == type);

    /// <summary>
    /// The cards to show, in order: desktop's list, the kinds a phone knows -
    /// each once-only kind once, the rest as often as they appear.
    /// </summary>
    public static IReadOnlyList<DashboardWidget> Current(AppSettings settings)
    {
        var seen = new HashSet<string>();
        return settings.DashboardWidgets
            .Where(w => IsKnown(w.WidgetType) && (KindOf(w.WidgetType).AllowsSeveral || seen.Add(w.WidgetType)))
            .ToList();
    }

    /// <summary>The card with <paramref name="id"/>, if it's showing.</summary>
    public static DashboardWidget? Find(AppSettings settings, string? id) =>
        id is null ? null : Current(settings).FirstOrDefault(w => w.Id == id);

    /// <summary>How many of <paramref name="type"/> are on the dashboard - the card picker's "On your dashboard".</summary>
    public static int CountOf(AppSettings settings, string type) => Current(settings).Count(w => w.WidgetType == type);

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

    /// <summary>Takes a card off the dashboard, its set-up with it.</summary>
    public static void Remove(AppSettings settings, string id) =>
        Save(settings, Current(settings).Where(w => w.Id != id));

    /// <summary>
    /// Desktop's starter dashboard (#140), from Core so each widget carries
    /// desktop's grid position and looks right there too - after any widgets
    /// the phone doesn't show, which are kept.
    /// </summary>
    public static IReadOnlyList<DashboardWidget> UseStarter(AppSettings settings)
    {
        var starter = StarterDashboard.Create();
        Save(settings, starter);
        return starter;
    }

    /// <summary>
    /// Once, the first time this version runs: an install that never changed
    /// its cards kept none, and showed <see cref="PreviousDefaultTypes"/>.
    /// Someone who has been using it - with devices pinned or opened - keeps
    /// those rather than finding the welcome card; a new install starts empty.
    /// </summary>
    /// <returns>Whether the old defaults were saved.</returns>
    public static bool KeepPreviousDefaults(AppSettings settings)
    {
        if (Current(settings).Count > 0 || (settings.PinnedDevices.Count == 0 && settings.RecentlyViewedDevices.Count == 0))
        {
            return false;
        }

        Save(settings, PreviousDefaultTypes.Select(New));
        return true;
    }

    /// <summary>The whole list as it is now - put back by <see cref="Restore"/>, for Undo.</summary>
    public static IReadOnlyList<DashboardWidget> Snapshot(AppSettings settings) => settings.DashboardWidgets.ToList();

    public static void Restore(AppSettings settings, IReadOnlyList<DashboardWidget> snapshot) =>
        settings.DashboardWidgets = snapshot.ToList();

    /// <summary>A card of <paramref name="type"/> that isn't on the dashboard yet, named as desktop names it.</summary>
    public static DashboardWidget New(string type)
    {
        var kind = KindOf(type);
        return new() { WidgetType = type, Title = kind.DesktopTitle ?? kind.Title };
    }

    /// <summary>
    /// Whether the card has a title of its own ("Core switch temps") - not
    /// the kind's name on the phone or on desktop ("Alerts gauge").
    /// </summary>
    public static bool HasOwnTitle(DashboardWidget widget) =>
        !string.IsNullOrWhiteSpace(widget.Title) && IsKnown(widget.WidgetType)
        && KindOf(widget.WidgetType) is var kind
        && widget.Title != kind.Title && widget.Title != kind.DesktopTitle;

    /// <summary>The title a card shows: its own, else the phone's name for its kind.</summary>
    public static string TitleOf(DashboardWidget widget) =>
        HasOwnTitle(widget) ? widget.Title : KindOf(widget.WidgetType).Title;
}
