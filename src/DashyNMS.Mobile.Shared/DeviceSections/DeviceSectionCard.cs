using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DashyNMS.Mobile.DeviceSections;

/// <summary>
/// One Device View section as a card on the device's page, opening the
/// section in full. The sections desktop's Overview has cards for show what
/// those cards show (#65) - availability's windows, the resources, the
/// sensors worth a look, what it's connected to, the busiest ports - and the
/// rest a count and their worst few rows (#43). As on desktop, a section the
/// device turns out to have nothing in (a switch's Wireless) disappears once
/// it has loaded, and shows while loading so the page doesn't jump about (#44).
/// </summary>
public sealed partial class DeviceSectionCard : ObservableObject
{
    /// <summary>How many rows a card shows, where desktop's Overview doesn't say otherwise.</summary>
    internal const int HighlightCount = 3;

    /// <summary>Desktop's Overview: the four sensors worth a look, five neighbours, five busiest ports.</summary>
    internal const int SensorCount = 4;
    internal const int NeighbourCount = 5;
    internal const int BusiestPortCount = 5;

    /// <summary>LibreNMS's availability windows, in seconds - the 30-day one is the headline figure.</summary>
    internal const double ThirtyDays = 2592000;

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

    /// <summary>Desktop's Overview card names where it has one: "Connected to", "Busiest ports".</summary>
    public string Title => Info.Section switch
    {
        DeviceSection.Neighbours => "Connected to",
        DeviceSection.Ports => "Busiest ports",
        _ => Info.Title,
    };

    public string Description => Info.Description;

    /// <summary>Whether it's fetched its own quick view - graphs and Graylog have their own pages and don't.</summary>
    public bool HasQuickView => Info.Section is not (DeviceSection.Graphs or DeviceSection.Graylog);

    public bool IsVisible => AlwaysShown.Contains(Info.Section) || IsLoading || !_loaded || _failed || Count > 0;

    public bool HasHighlights => Highlights.Count > 0;

    /// <summary>Something critical or warning in the section - the card gets its colour strip.</summary>
    public bool NeedsAttention => Status is RowStatus.Critical or RowStatus.Warning;

    /// <summary>The summary once loaded; the section's description before then, or when it has no quick view.</summary>
    public bool ShowSummary => HasQuickView && !IsLoading && SummaryText is not null;

    /// <summary>Everything the section loaded, for the page's stat tiles.</summary>
    public IReadOnlyList<SectionGroup> Groups { get; private set; } = [];

    /// <summary>Fills the card from the section's rows.</summary>
    public void Show(IReadOnlyList<SectionGroup> groups)
    {
        _failed = false;
        _loaded = true;
        Groups = groups;

        var rows = groups.SelectMany(g => g).ToList();
        var critical = rows.Count(r => r.Status == RowStatus.Critical);
        var warning = rows.Count(r => r.Status == RowStatus.Warning);
        Status = critical > 0 ? RowStatus.Critical : warning > 0 ? RowStatus.Warning : RowStatus.None;

        (SummaryText, Highlights) = Info.Section switch
        {
            DeviceSection.Availability => Availability(groups),
            DeviceSection.Resources => Resources(groups),
            DeviceSection.Neighbours => Neighbours(rows),
            DeviceSection.Ports => Ports(groups, rows),
            DeviceSection.Sensors => (Summary(rows, critical, warning), Worst(rows, SensorCount)),
            _ => (Summary(rows, critical, warning), Worst(rows, HighlightCount)),
        };

        Count = rows.Count;
        OnPropertyChanged(nameof(Groups));
        OnPropertyChanged(nameof(IsVisible));
    }

    /// <summary>A section that couldn't load stays, saying so, rather than vanishing as if empty.</summary>
    public void Failed()
    {
        _loaded = true;
        _failed = true;
        Groups = [];
        SummaryText = "Couldn't load - tap to try again";
        Highlights = [];
        Count = 0;
        OnPropertyChanged(nameof(Groups));
        OnPropertyChanged(nameof(IsVisible));
    }

    /// <summary>The 30-day figure, from <see cref="Groups"/> once availability has loaded.</summary>
    internal static SectionRow? ThirtyDayWindow(IEnumerable<SectionGroup> groups) =>
        groups.SelectMany(g => g).FirstOrDefault(r => r.SortValue == ThirtyDays);

    /// <summary>Desktop's Availability card: each window in order, with the outage count.</summary>
    private (string, IReadOnlyList<SectionRow>) Availability(IReadOnlyList<SectionGroup> groups)
    {
        var windows = groups.FirstOrDefault(g => g.Name == "Availability")?.ToList() ?? [];
        var outages = groups.FirstOrDefault(g => g.Name == "Outages")?.Count ?? 0;
        var summary = outages == 0 ? "No recent outages"
            : Counted(outages, "recent outage", "recent outages");
        return (summary, windows);
    }

    /// <summary>
    /// Desktop's Resources card: CPU, memory and the fullest disk, as bars -
    /// the first of each, since most devices have one.
    /// </summary>
    private (string, IReadOnlyList<SectionRow>) Resources(IReadOnlyList<SectionGroup> groups)
    {
        var picked = new List<SectionRow>();
        foreach (var group in groups)
        {
            var row = group.Name == "Storage"
                ? group.OrderByDescending(r => r.Bar ?? 0).FirstOrDefault()
                : group.FirstOrDefault();
            if (row is not null)
            {
                picked.Add(row);
            }
        }

        var total = groups.Sum(g => g.Count);
        return (Counted(total, "resource", "resources"), picked);
    }

    /// <summary>Desktop's Connected to card: the first few, and how many are devices LibreNMS knows.</summary>
    private static (string, IReadOnlyList<SectionRow>) Neighbours(IReadOnlyList<SectionRow> rows)
    {
        var known = rows.Count(r => r.LinkDeviceId is not null);
        var summary = rows.Count == 0 ? "No neighbours"
            : $"{Counted(rows.Count, "neighbour", "neighbours")} · {known.ToString("N0", CultureInfo.CurrentCulture)} in LibreNMS";
        return (summary, rows.Take(NeighbourCount).ToList());
    }

    /// <summary>Desktop's Busiest ports card: the five moving the most traffic, and how many are up.</summary>
    private static (string, IReadOnlyList<SectionRow>) Ports(IReadOnlyList<SectionGroup> groups, IReadOnlyList<SectionRow> rows)
    {
        var up = groups.FirstOrDefault(g => g.Name == "Up")?.Count ?? 0;
        var down = groups.FirstOrDefault(g => g.Name == "Down")?.Count ?? 0;
        var summary = rows.Count == 0 ? "No ports"
            : $"{up.ToString("N0", CultureInfo.CurrentCulture)} of {Counted(rows.Count, "port", "ports")} up"
              + (down > 0 ? $" · {down.ToString("N0", CultureInfo.CurrentCulture)} down" : string.Empty);

        var busiest = rows
            .Where(r => r.SortValue > 0)
            .OrderByDescending(r => r.SortValue)
            .Take(BusiestPortCount)
            .ToList();
        return (summary, busiest);
    }

    private string Summary(IReadOnlyList<SectionRow> rows, int critical, int warning)
    {
        if (rows.Count == 0)
        {
            return $"No {Noun(Info.Section).Plural}";
        }

        var parts = new List<string> { Counted(rows.Count, Noun(Info.Section)) };
        if (critical > 0)
        {
            parts.Add(Counted(critical, "critical", "critical"));
        }

        if (warning > 0)
        {
            parts.Add(Counted(warning, "warning", "warnings"));
        }

        return string.Join(" · ", parts);
    }

    /// <summary>The worst first, then as the section lists them - so what's in trouble is what shows.</summary>
    private static IReadOnlyList<SectionRow> Worst(IReadOnlyList<SectionRow> rows, int count) => rows
        .Select((row, index) => (row, index))
        .OrderByDescending(x => Rank(x.row.Status))
        .ThenBy(x => x.index)
        .Take(count)
        .Select(x => x.row)
        .ToList();

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
        DeviceSection.Sensors => ("sensor", "sensors"),
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
