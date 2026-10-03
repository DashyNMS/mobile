using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Devices;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// One card on the dashboard. Most templates read what they show from
/// <see cref="Dashboard"/>; a Sensors or Graph card has its own, since there
/// can be several, each set up on its own (#87).
/// </summary>
public sealed partial class DashboardCard : ObservableObject
{
    public DashboardCard(DashboardWidget widget, DashboardViewModel dashboard)
    {
        Widget = widget;
        Dashboard = dashboard;
        _title = DashboardLayout.HasOwnTitle(widget) ? widget.Title : DashboardLayout.KindOf(widget.WidgetType).Title;
    }

    public DashboardWidget Widget { get; }

    public DashboardViewModel Dashboard { get; }

    public string Type => Widget.WidgetType;

    /// <summary>Its own ("Core switch temps"), the graph and its device, or the kind's.</summary>
    [ObservableProperty]
    private string _title;

    /// <summary>A Sensors card's sensors, coloured as on the Health tab.</summary>
    public BulkObservableCollection<SectionRow> Sensors { get; } = new();

    public bool HasNoSensors => Sensors.Count == 0;

    /// <summary>A Graph card's page (see <see cref="GraphHtml"/>), or null until it has one.</summary>
    [ObservableProperty]
    private string? _graphPage;

    public bool GraphNeedsSetUp => Widget is not { GraphDeviceId: not null, GraphName: not null };

    // Set up since the card was made: the hint goes as the graph arrives.
    partial void OnGraphPageChanged(string? value) => OnPropertyChanged(nameof(GraphNeedsSetUp));

    /// <summary>Back from Customise or its set-up: a Sensors card's title may be new.</summary>
    internal void RefreshTitle()
    {
        if (Type != DashboardLayout.Graph)
        {
            Title = DashboardLayout.HasOwnTitle(Widget) ? Widget.Title : DashboardLayout.KindOf(Widget.WidgetType).Title;
        }
    }

    internal void ShowSensors(IEnumerable<SectionRow> rows)
    {
        Sensors.ReplaceAll(rows);
        OnPropertyChanged(nameof(HasNoSensors));
    }

    // ------------------------------------------------------------ Top cards (#103)

    /// <summary>A Top card's rows, ranked from the network's ports.</summary>
    public BulkObservableCollection<TopRow> TopRows { get; } = new();

    /// <summary>Loaded and nothing to list: "No interface errors.", say.</summary>
    [ObservableProperty]
    private bool _hasNoTopRows;

    public string TopEmptyText => TopCards.EmptyText(Type);

    public string? TopFootnote => TopCards.Footnote(Type);

    public bool HasTopFootnote => TopFootnote is not null;

    /// <summary>The first column's heading, as desktop's: just the device for Top devices.</summary>
    public string TopNameHeading => Type == DashboardLayout.TopDevices ? "Device" : "Device · interface";

    /// <summary>For the chips: what the card ranks by now.</summary>
    public bool RanksByTotal => Widget.TopRankBy == RankBy.Total;

    public bool RanksByIn => Widget.TopRankBy == RankBy.In;

    public bool RanksByOut => Widget.TopRankBy == RankBy.Out;

    /// <summary>
    /// A ranking chip: "Total", "In" or "Out". Saved to the card, as
    /// desktop's In and Out headings do, and re-ranked from the ports
    /// already fetched rather than asking LibreNMS again.
    /// </summary>
    [RelayCommand]
    private void Rank(string? by)
    {
        if (!Enum.TryParse<RankBy>(by, ignoreCase: true, out var rankBy) || rankBy == Widget.TopRankBy)
        {
            return;
        }

        Widget.TopRankBy = rankBy;
        OnPropertyChanged(nameof(RanksByTotal));
        OnPropertyChanged(nameof(RanksByIn));
        OnPropertyChanged(nameof(RanksByOut));
        Dashboard.TopOptionsChanged(this);
    }

    internal void ShowTop(IReadOnlyList<TopRow> rows)
    {
        TopRows.ReplaceAll(rows);
        HasNoTopRows = rows.Count == 0;
        OnPropertyChanged(nameof(RanksByTotal));
        OnPropertyChanged(nameof(RanksByIn));
        OnPropertyChanged(nameof(RanksByOut));
    }
}

/// <summary>
/// Desktop's dashboard widgets, as a column of cards the user chooses and
/// orders (<see cref="DashboardLayout"/>): alert counts with desktop's gauge,
/// device counts, the alerts needing attention, pinned and recently viewed
/// devices, picked sensors, a graph and the wireless controllers.
/// </summary>
/// <remarks>
/// A refresh only fetches what the cards showing need: the device and alert
/// lists always (they're cheap and most cards use them), every sensor only
/// for a Sensors card, a graph only for a Graph card, and wireless readings
/// only for a Wireless card - found the way desktop's widget finds them, by
/// asking one device of each OS first rather than every device.
/// </remarks>
public sealed partial class DashboardViewModel : ViewModelBase
{
    /// <summary>How many alerts the dashboard lists before "see all".</summary>
    public const int TopAlertCount = 5;

    /// <summary>Requested graph size: LibreNMS draws at this, the phone scales it to fit.</summary>
    internal const int GraphWidth = 800;
    internal const int GraphHeight = 400;

    private const int MaxConcurrentWirelessRequests = 4;

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly DeviceBookmarks _bookmarks;
    private readonly MaintenanceScan _maintenance;
    private readonly HashSet<string> _probedOses = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _wirelessOses = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Up and not in a maintenance window - maintenance is counted on its own, as on the Devices tab.</summary>
    [ObservableProperty]
    private int _devicesUp;

    [ObservableProperty]
    private int _devicesDown;

    /// <summary>In a maintenance window (#33) - filled in after the rest, since it's one request per device.</summary>
    [ObservableProperty]
    private int _devicesMaintenance;

    /// <summary>
    /// Open, unacknowledged alerts with LibreNMS's ok severity (#64) -
    /// what the Alerts tab's OK chip shows.
    /// </summary>
    [ObservableProperty]
    private int _okAlerts;

    /// <summary>Disabled plus ignored - not being watched either way.</summary>
    [ObservableProperty]
    private int _devicesInactive;

    [ObservableProperty]
    private int _criticalAlerts;

    [ObservableProperty]
    private int _warningAlerts;

    [ObservableProperty]
    private int _acknowledgedAlerts;

    [ObservableProperty]
    private DateTime? _lastUpdated;

    [ObservableProperty]
    private string _wirelessSummary = string.Empty;

    public DashboardViewModel(
        ILibreNmsClient client,
        ISettingsStore settings,
        INavigationService navigation,
        DeviceBookmarks bookmarks,
        MaintenanceScan? maintenance = null)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
        _bookmarks = bookmarks;
        _maintenance = maintenance ?? new MaintenanceScan(client, TimeProvider.System);
        ApplyLayout();
    }

    /// <summary>Awaited by tests: the maintenance count, filled in after the rest of the dashboard.</summary>
    internal Task MaintenanceCounted { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// A count's tap: the Alerts tab with only that kind showing (#32) -
    /// "critical", "warning" or "acknowledged".
    /// </summary>
    [RelayCommand]
    private Task ShowAlertsAsync(string? kind) => string.IsNullOrEmpty(kind)
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.Alerts, new Dictionary<string, object> { [Routes.AlertFilterParameter] = kind });

    /// <summary>
    /// A device count's tap: the Devices tab with only that state showing
    /// (#34) - "up", "down", "maintenance" or "disabled".
    /// </summary>
    [RelayCommand]
    private Task ShowDevicesAsync(string? state) =>
        Enum.TryParse<DeviceState>(state, ignoreCase: true, out var parsed)
            ? _navigation.GoToAsync(Routes.Devices, new Dictionary<string, object> { [Routes.StateParameter] = parsed })
            : Task.CompletedTask;

    /// <summary>The cards showing, in the order chosen.</summary>
    public BulkObservableCollection<DashboardCard> Cards { get; } = new();

    /// <summary>Set by the page from the phone's theme, for the Graph card.</summary>
    public bool DarkTheme { get; set; }

    /// <summary>Pinned devices with their live state, as desktop's Pinned devices widget.</summary>
    public BulkObservableCollection<DeviceItem> PinnedDevices { get; } = new();

    /// <summary>As desktop's Recently viewed widget, newest first.</summary>
    public BulkObservableCollection<RecentlyViewedDevice> RecentlyViewed { get; } = new();

    public bool HasPinnedDevices => PinnedDevices.Count > 0;

    public bool HasRecentlyViewed => RecentlyViewed.Count > 0;

    /// <summary>The most severe, then most recent, open unacknowledged alerts.</summary>
    public BulkObservableCollection<AlertItem> TopAlerts { get; } = new();

    public bool HasNoAlerts => TopAlerts.Count == 0 && LastUpdated is not null;

    /// <summary>The Wireless card's controllers: down first, then worst state.</summary>
    public BulkObservableCollection<SectionRow> WirelessControllers { get; } = new();

    public bool HasNoWirelessControllers => WirelessControllers.Count == 0 && LastUpdated is not null;

    /// <summary>
    /// Re-reads which cards show, e.g. after Customise. A card still there
    /// keeps what it has loaded, so a graph doesn't blank while it reloads.
    /// </summary>
    public void ApplyLayout()
    {
        var existing = Cards.ToDictionary(c => c.Widget.Id);
        Cards.ReplaceAll(DashboardLayout.Current(_settings.Current)
            .Select(w => existing.TryGetValue(w.Id, out var card) && ReferenceEquals(card.Widget, w) ? card : new DashboardCard(w, this))
            .ToList());

        foreach (var card in Cards)
        {
            card.RefreshTitle();
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        ApplyLayout();

        var devicesTask = _client.Devices.ListAsync();
        var alertsTask = _client.Alerts.ListAsync(AlertQuery.Open);
        await Task.WhenAll(devicesTask, alertsTask);

        var devices = devicesTask.Result;
        DevicesDown = devices.Count(d => d.State == DeviceState.Down);
        DevicesInactive = devices.Count(d => d.State is DeviceState.Disabled or DeviceState.Ignored);
        CountUp(devices, _maintenanceIds);

        var byId = devices.ToDictionary(d => d.DeviceId);

        var alerts = alertsTask.Result;
        var active = alerts.Where(a => !a.IsAcknowledged).ToList();
        CriticalAlerts = active.Count(a => a.Severity == AlertSeverity.Critical);
        WarningAlerts = active.Count(a => a.Severity == AlertSeverity.Warning);
        AcknowledgedAlerts = alerts.Count - active.Count;

        OkAlerts = active.Count(a => a.Severity is not (AlertSeverity.Critical or AlertSeverity.Warning));

        // Last, and without holding up the rest: one request per device.
        if (Shows(DashboardLayout.DeviceStatus))
        {
            MaintenanceCounted = CountMaintenanceAsync(devices);
        }

        TopAlerts.ReplaceAll(active
            .OrderByDescending(a => a.Severity.SortRank())
            .ThenByDescending(a => a.Timestamp)
            .Take(TopAlertCount)
            .Select(alert => AlertItem.For(alert, _settings.Current, byId)));

        // Pinned in the order they were pinned, as desktop; a pin whose device
        // has since gone from LibreNMS just doesn't show.
        var style = _settings.Current.DeviceNameStyle;
        PinnedDevices.ReplaceAll((_bookmarks.PinningEnabled ? _settings.Current.PinnedDevices : [])
            .Where(pin => byId.ContainsKey(pin.DeviceId))
            .Select(pin => new DeviceItem(byId[pin.DeviceId], style) { IsPinned = true }));

        RecentlyViewed.ReplaceAll(_bookmarks.RecentlyViewed);

        // The extras, only for the cards that show them; one failing doesn't stop the rest.
        await Task.WhenAll(
            Shows(DashboardLayout.Sensors) ? LoadSensorsAsync(byId, style) : Task.CompletedTask,
            Shows(DashboardLayout.Graph) ? LoadGraphsAsync(byId, style) : Task.CompletedTask,
            Shows(DashboardLayout.Wireless) ? LoadWirelessAsync(devices, style) : Task.CompletedTask,
            Cards.Any(c => TopCards.IsTop(c.Type)) ? LoadTopAsync(byId, style) : Task.CompletedTask);

        LastUpdated = DateTime.Now;
        OnPropertyChanged(nameof(HasNoAlerts));
        OnPropertyChanged(nameof(HasPinnedDevices));
        OnPropertyChanged(nameof(HasRecentlyViewed));
        OnPropertyChanged(nameof(HasNoWirelessControllers));
    });

    [RelayCommand]
    private Task OpenAlertAsync(AlertItem? item) => item is null ? Task.CompletedTask : _navigation.GoToAlertAsync(item.Alert);

    [RelayCommand]
    private Task OpenPinnedAsync(DeviceItem? item) => item is null ? Task.CompletedTask : OpenAsync(item.DeviceId);

    [RelayCommand]
    private Task OpenRecentAsync(RecentlyViewedDevice? recent) => recent is null ? Task.CompletedTask : OpenAsync(recent.DeviceId);

    /// <summary>A sensor or wireless controller opens its device.</summary>
    [RelayCommand]
    private Task OpenRowAsync(SectionRow? row) => row?.LinkDeviceId is { } id ? OpenAsync(id) : Task.CompletedTask;

    [RelayCommand]
    private Task CustomiseAsync() => _navigation.GoToAsync(Routes.CustomiseDashboard);

    /// <summary>That card's own set-up: its sensors and title.</summary>
    [RelayCommand]
    private Task PickSensorsAsync(DashboardCard? card) => _navigation.GoToAsync(
        Routes.PickSensors, new Dictionary<string, object> { [Routes.WidgetIdParameter] = card?.Widget.Id ?? string.Empty });

    [RelayCommand]
    private Task PickGraphAsync(DashboardCard? card) => _navigation.GoToAsync(
        Routes.PickGraph, new Dictionary<string, object> { [Routes.WidgetIdParameter] = card?.Widget.Id ?? string.Empty });

    /// <summary>A Graph card opens its device's graphs page - or, not set up yet, its set-up.</summary>
    [RelayCommand]
    private Task OpenGraphAsync(DashboardCard? card) => card?.Widget is { GraphDeviceId: { } id } widget
        ? _navigation.GoToAsync(Routes.DeviceGraphs, new Dictionary<string, object>
        {
            [Routes.DeviceIdParameter] = id,

            // On the card's own graph and range, not the device's first (#119).
            [Routes.GraphParameter] = widget.GraphName ?? string.Empty,
            [Routes.GraphRangeParameter] = widget.GraphTimeRangePreset,
        })
        : PickGraphAsync(card);

    /// <summary>
    /// Every Sensors card's sensors, from one list of the network's sensors -
    /// so several cards cost no more than one (#87).
    /// </summary>
    private async Task LoadSensorsAsync(IReadOnlyDictionary<int, Device> devices, DeviceNameStyle style)
    {
        var cards = CardsOf(DashboardLayout.Sensors);
        foreach (var card in cards.Where(c => c.Widget.Sensors.Count == 0))
        {
            card.ShowSensors([]);
        }

        if (cards.All(c => c.Widget.Sensors.Count == 0))
        {
            return;
        }

        try
        {
            var all = (await _client.Sensors.ListAsync()).ToDictionary(s => s.SensorId);
            var settings = _settings.Current;

            // In the order they were added; one LibreNMS has since dropped just doesn't show.
            foreach (var card in cards.Where(c => c.Widget.Sensors.Count > 0))
            {
                card.ShowSensors(card.Widget.Sensors
                    .Where(p => all.ContainsKey(p.SensorId))
                    .Select(p =>
                    {
                        var sensor = all[p.SensorId];
                        var reading = DeviceSectionLoader.ReadSensor(sensor, settings);
                        return new SectionRow(reading.Name)
                        {
                            Value = reading.Value,
                            Status = reading.Status,
                            Subtitle = devices.TryGetValue(sensor.DeviceId, out var device) ? new DeviceItem(device, style).Name : p.DeviceName,
                            LinkDeviceId = sensor.DeviceId,
                        };
                    })
                    .ToList());
            }
        }
        catch (LibreNmsApiException)
        {
            // Kept as they were; the rest of the dashboard still refreshes.
        }
    }

    /// <summary>
    /// Every Top card's rows, from one list of the network's ports - as
    /// desktop shares one fetch between its Top widgets, since it's every
    /// port there is (#103). If LibreNMS can't be reached, the rows stay.
    /// </summary>
    private async Task LoadTopAsync(IReadOnlyDictionary<int, Device> devices, DeviceNameStyle style)
    {
        try
        {
            _ports = await _client.Ports.ListAllStatusAsync();
        }
        catch (LibreNmsApiException)
        {
            return;
        }

        _portDevices = devices;
        _portNames = style;
        foreach (var card in Cards.Where(c => TopCards.IsTop(c.Type)))
        {
            ShowTop(card);
        }
    }

    /// <summary>A Top card's chip changed its ranking: saved, then re-ranked from the ports already in hand.</summary>
    internal void TopOptionsChanged(DashboardCard card)
    {
        _settings.Save();
        if (_ports is not null)
        {
            ShowTop(card);
        }
    }

    private void ShowTop(DashboardCard card) =>
        card.ShowTop(TopCards.Rows(card.Widget, _ports ?? [], _portDevices, d => new DeviceItem(d, _portNames).Name));

    /// <summary>A Top card's row opens its device.</summary>
    [RelayCommand]
    private Task OpenTopRowAsync(TopRow? row) => row is null ? Task.CompletedTask : OpenAsync(row.DeviceId);

    /// <summary>Every Graph card's graph, side by side; one that fails just shows none.</summary>
    private Task LoadGraphsAsync(IReadOnlyDictionary<int, Device> devices, DeviceNameStyle style) =>
        Task.WhenAll(CardsOf(DashboardLayout.Graph).Select(card => LoadGraphAsync(card, devices, style)));

    private async Task LoadGraphAsync(DashboardCard card, IReadOnlyDictionary<int, Device> devices, DeviceNameStyle style)
    {
        if (card.Widget is not { GraphDeviceId: { } deviceId, GraphName: { } graph } widget)
        {
            card.GraphPage = null;
            card.Title = DashboardLayout.HasOwnTitle(card.Widget) ? card.Widget.Title : DashboardLayout.KindOf(DashboardLayout.Graph).Title;
            return;
        }

        var deviceName = devices.TryGetValue(deviceId, out var device) ? new DeviceItem(device, style).Name : $"Device {deviceId}";
        card.Title = DashboardLayout.HasOwnTitle(widget) ? widget.Title : $"{graph} · {deviceName}";

        try
        {
            var range = new GraphTimeRange(widget.GraphTimeRangePreset == GraphTimeRangePreset.Custom ? GraphTimeRangePreset.Day : widget.GraphTimeRangePreset);
            var svg = await _client.Graphs.GetSvgAsync(deviceId, graph, range, GraphWidth, GraphHeight);
            card.GraphPage = GraphHtml.Build(svg, DarkTheme);
        }
        catch (LibreNmsApiException)
        {
            card.GraphPage = null;
        }
    }

    private async Task LoadWirelessAsync(IReadOnlyList<Device> devices, DeviceNameStyle style)
    {
        // Desktop's approach: learn which OSes have wireless readings by
        // asking one device of each, then only ever ask devices of those.
        var probes = WirelessFleet.ProbeCandidates(devices, _probedOses);
        if (probes.Count > 0)
        {
            var answers = await WirelessReadingsAsync(probes);
            foreach (var probe in probes.Where(p => answers.ContainsKey(p.DeviceId)))
            {
                _probedOses.Add(probe.Os!);
                if (answers[probe.DeviceId].Any(s => !s.Deleted))
                {
                    _wirelessOses.Add(probe.Os!);
                }
            }
        }

        var controllers = WirelessFleet.DevicesToPoll(devices, _wirelessOses);
        var readings = await WirelessReadingsAsync(controllers);

        var rows = controllers
            .Where(d => readings.ContainsKey(d.DeviceId))
            .Select(d => (Device: d, Summary: WirelessFleet.Summarise(d.DeviceId, readings[d.DeviceId])))
            .Where(x => x.Summary.HasAny)
            .OrderByDescending(x => x.Device.State == DeviceState.Down)
            .ThenByDescending(x => x.Summary.Severity)
            .ThenBy(x => x.Device.Hostname, StringComparer.OrdinalIgnoreCase)
            .Select(x => new SectionRow(new DeviceItem(x.Device, style).Name)
            {
                Value = x.Summary.Clients is { } clients ? $"{clients:N0} clients" : null,
                Subtitle = x.Summary.ApCount is { } aps ? $"{aps:N0} access points" : null,
                Status = x.Device.State == DeviceState.Down ? RowStatus.Critical : x.Summary.Severity switch
                {
                    AlertSeverity.Critical => RowStatus.Critical,
                    AlertSeverity.Warning => RowStatus.Warning,
                    _ => RowStatus.Ok,
                },
                LinkDeviceId = x.Device.DeviceId,
            })
            .ToList();

        WirelessControllers.ReplaceAll(rows);
        var totalClients = rows.Count == 0 ? 0 : controllers
            .Where(d => readings.ContainsKey(d.DeviceId))
            .Sum(d => WirelessFleet.Summarise(d.DeviceId, readings[d.DeviceId]).Clients ?? 0);
        WirelessSummary = rows.Count == 0 ? string.Empty
            : $"{rows.Count} {(rows.Count == 1 ? "controller" : "controllers")} · {totalClients:N0} clients";
    }

    /// <summary>Each device's wireless readings, a few at a time; one that fails is left out.</summary>
    private async Task<Dictionary<int, IReadOnlyList<WirelessSensor>>> WirelessReadingsAsync(IReadOnlyList<Device> devices)
    {
        var results = new Dictionary<int, IReadOnlyList<WirelessSensor>>();
        using var gate = new SemaphoreSlim(MaxConcurrentWirelessRequests);

        await Task.WhenAll(devices.Select(async device =>
        {
            await gate.WaitAsync();
            try
            {
                var readings = await _client.Devices.GetWirelessSensorsAsync(device.DeviceId);
                lock (results)
                {
                    results[device.DeviceId] = readings;
                }
            }
            catch (LibreNmsApiException)
            {
                // Asked again next refresh.
            }
            finally
            {
                gate.Release();
            }
        }));

        return results;
    }

    private IReadOnlySet<int> _maintenanceIds = new HashSet<int>();

    // The last list of the network's ports, and how to name their devices, for the Top cards.
    private IReadOnlyList<Port>? _ports;
    private IReadOnlyDictionary<int, Device> _portDevices = new Dictionary<int, Device>();
    private DeviceNameStyle _portNames;

    private void CountUp(IReadOnlyList<Device> devices, IReadOnlySet<int> maintenance)
    {
        DevicesMaintenance = devices.Count(d => maintenance.Contains(d.DeviceId));
        DevicesUp = devices.Count(d => d.State == DeviceState.Up && !maintenance.Contains(d.DeviceId));
    }

    private async Task CountMaintenanceAsync(IReadOnlyList<Device> devices)
    {
        _maintenanceIds = await _maintenance.ScanAsync(devices);
        CountUp(devices, _maintenanceIds);
    }

    private bool Shows(string type) => Cards.Any(c => c.Type == type);

    private List<DashboardCard> CardsOf(string type) => Cards.Where(c => c.Type == type).ToList();

    private Task OpenAsync(int deviceId) =>
        _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = deviceId });
}
