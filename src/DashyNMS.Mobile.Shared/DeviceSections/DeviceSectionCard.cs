using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DashyNMS.Mobile.DeviceSections;

/// <summary>
/// One Device View section as a card on the device's page: a quick view -
/// how many there are, how many need attention, and the few worst rows -
/// that opens the section in full (#43). As on desktop, a section the device
/// turns out to have nothing in (a switch's Wireless) disappears once it has
/// loaded, and shows while loading so the list doesn't jump about (#44).
/// </summary>
public sealed partial class DeviceSectionCard : ObservableObject
{
    /// <summary>How many rows the quick view shows.</summary>
    internal const int HighlightCount = 3;

    /// <summary>
    /// Desktop always shows these tabs, empty or not: there's always
    /// availability to report, sensors and the event log say "none" rather
    /// than vanish, and graphs have their own page.
    /// </summary>
    private static readonly HashSet<DeviceSection> AlwaysShown =
    [
        DeviceSection.Availability,
        DeviceSection.Sensors,
        DeviceSection.Graphs,
        DeviceSection.EventLog,
        DeviceSection.Graylog,
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible), nameof(ShowSummary))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible), nameof(HasHighlights), nameof(ShowSummary))]
    private int _count;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSummary))]
    private string? _summaryText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHighlights))]
    private IReadOnlyList<SectionRow> _highlights = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsAttention))]
    private RowStatus _status;

    private bool _loaded;
    private bool _failed;

    public DeviceSectionCard(DeviceSectionInfo info) => Info = info;

    public DeviceSectionInfo Info { get; }

    public string Title => Info.Title;

    public string Description => Info.Description;

    /// <summary>Whether it's fetched its own quick view - graphs and Graylog have their own pages and don't.</summary>
    public bool HasQuickView => Info.Section is not (DeviceSection.Graphs or DeviceSection.Graylog);

    public bool IsVisible => AlwaysShown.Contains(Info.Section) || IsLoading || !_loaded || _failed || Count > 0;

    public bool HasHighlights => Highlights.Count > 0;

    /// <summary>Something critical or warning in the section - the card gets its colour strip.</summary>
    public bool NeedsAttention => Status is RowStatus.Critical or RowStatus.Warning;

    /// <summary>The summary once loaded; the section's description before then, or when it has no quick view.</summary>
    public bool ShowSummary => HasQuickView && !IsLoading && SummaryText is not null;

    /// <summary>Fills the quick view from the section's rows.</summary>
    public void Show(IReadOnlyList<SectionGroup> groups)
    {
        _failed = false;
        var rows = groups.SelectMany(g => g).ToList();
        var critical = rows.Count(r => r.Status == RowStatus.Critical);
        var warning = rows.Count(r => r.Status == RowStatus.Warning);

        var parts = new List<string> { Counted(rows.Count, Noun(Info.Section)) };
        if (critical > 0)
        {
            parts.Add(Counted(critical, "critical", plural: "critical"));
        }

        if (warning > 0)
        {
            parts.Add(Counted(warning, "warning", plural: "warnings"));
        }

        _loaded = true;
        SummaryText = rows.Count == 0 ? $"No {Noun(Info.Section).Plural}" : string.Join(" · ", parts);
        Status = critical > 0 ? RowStatus.Critical : warning > 0 ? RowStatus.Warning : RowStatus.None;

        // The worst first, then as the section lists them - so sensors or
        // ports in trouble are what the card shows.
        Highlights = rows
            .Select((row, index) => (row, index))
            .OrderByDescending(x => Rank(x.row.Status))
            .ThenBy(x => x.index)
            .Take(HighlightCount)
            .Select(x => x.row)
            .ToList();
        Count = rows.Count;
        OnPropertyChanged(nameof(IsVisible));
    }

    /// <summary>A section that couldn't load stays, saying so, rather than vanishing as if empty.</summary>
    public void Failed()
    {
        _loaded = true;
        _failed = true;
        SummaryText = "Couldn't load - tap to try again";
        Highlights = [];
        Count = 0;
        OnPropertyChanged(nameof(IsVisible));
    }

    private static int Rank(RowStatus status) => status switch
    {
        RowStatus.Critical => 3,
        RowStatus.Warning => 2,
        _ => 0,
    };

    private static string Counted(int count, (string Singular, string Plural) noun) =>
        count.ToString("N0", CultureInfo.CurrentCulture) + " " + (count == 1 ? noun.Singular : noun.Plural);

    private static string Counted(int count, string singular, string plural) => Counted(count, (singular, plural));

    /// <summary>What a section's rows are, for "12 sensors".</summary>
    private static (string Singular, string Plural) Noun(DeviceSection section) => section switch
    {
        DeviceSection.Availability => ("figure", "figures"),
        DeviceSection.Sensors => ("sensor", "sensors"),
        DeviceSection.Resources => ("resource", "resources"),
        DeviceSection.Ports => ("port", "ports"),
        DeviceSection.Neighbours => ("neighbour", "neighbours"),
        DeviceSection.Vlans => ("VLAN", "VLANs"),
        DeviceSection.Fdb => ("MAC address", "MAC addresses"),
        DeviceSection.Arp => ("ARP entry", "ARP entries"),
        DeviceSection.Routing => ("routing entry", "routing entries"),
        DeviceSection.Wireless => ("wireless reading", "wireless readings"),
        DeviceSection.Inventory => ("inventory item", "inventory items"),
        DeviceSection.EventLog => ("recent event", "recent events"),
        _ => ("item", "items"),
    };
}
