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

/// <summary>One card on the dashboard. Its template reads what it shows from <see cref="Dashboard"/>.</summary>
public sealed record DashboardCard(string Type, string Title, DashboardViewModel Dashboard);

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
    private readonly HashSet<string> _probedOses = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _wirelessOses = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private int _devicesUp;

    [ObservableProperty]
    private int _devicesDown;

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

    /// <summary>The Graph card's page (see <see cref="GraphHtml"/>), or null until it has one.</summary>
    [ObservableProperty]
    private string? _graphPage;

    [ObservableProperty]
    private string _graphTitle = "Graph";

    [ObservableProperty]
    private string _wirelessSummary = string.Empty;

    public DashboardViewModel(ILibreNmsClient client, ISettingsStore settings, INavigationService navigation, DeviceBookmarks bookmarks)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
        _bookmarks = bookmarks;
        ApplyLayout();
    }

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

    /// <summary>Desktop's alerts gauge: each severity's share of open alerts, for the proportion bar.</summary>
    public double CriticalShare => Share(CriticalAlerts);

    public double WarningShare => Share(WarningAlerts);

    public double AcknowledgedShare => Share(AcknowledgedAlerts);

    public bool HasOpenAlerts => CriticalAlerts + WarningAlerts + AcknowledgedAlerts > 0;

    /// <summary>The Sensors card's sensors, coloured as on the Health tab.</summary>
    public BulkObservableCollection<SectionRow> PinnedSensors { get; } = new();

    public bool HasNoPinnedSensors => PinnedSensors.Count == 0;

    public bool GraphNeedsSetUp => GraphWidget is not { GraphDeviceId: not null, GraphName: not null };

    /// <summary>The Wireless card's controllers: down first, then worst state.</summary>
    public BulkObservableCollection<SectionRow> WirelessControllers { get; } = new();

    public bool HasNoWirelessControllers => WirelessControllers.Count == 0 && LastUpdated is not null;

    private DashboardWidget? GraphWidget => Widget(DashboardLayout.Graph);

    /// <summary>Re-reads which cards show, e.g. after Customise.</summary>
    public void ApplyLayout() => Cards.ReplaceAll(DashboardLayout.Current(_settings.Current)
        .Select(w => new DashboardCard(w.WidgetType, DashboardLayout.KindOf(w.WidgetType).Title, this))
        .ToList());

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        ApplyLayout();

        var devicesTask = _client.Devices.ListAsync();
        var alertsTask = _client.Alerts.ListAsync(AlertQuery.Open);
        await Task.WhenAll(devicesTask, alertsTask);

        var devices = devicesTask.Result;
        DevicesUp = devices.Count(d => d.State == DeviceState.Up);
        DevicesDown = devices.Count(d => d.State == DeviceState.Down);
        DevicesInactive = devices.Count(d => d.State is DeviceState.Disabled or DeviceState.Ignored);

        var alerts = alertsTask.Result;
        var active = alerts.Where(a => !a.IsAcknowledged).ToList();
        CriticalAlerts = active.Count(a => a.Severity == AlertSeverity.Critical);
        WarningAlerts = active.Count(a => a.Severity == AlertSeverity.Warning);
        AcknowledgedAlerts = alerts.Count - active.Count;

        var utc = _settings.Current.ServerTimestampsAreUtc;
        TopAlerts.ReplaceAll(active
            .OrderByDescending(a => a.Severity.SortRank())
            .ThenByDescending(a => a.Timestamp)
            .Take(TopAlertCount)
            .Select(alert => new AlertItem(alert, utc)));

        // Pinned in the order they were pinned, as desktop; a pin whose device
        // has since gone from LibreNMS just doesn't show.
        var style = _settings.Current.DeviceNameStyle;
        var byId = devices.ToDictionary(d => d.DeviceId);
        PinnedDevices.ReplaceAll((_bookmarks.PinningEnabled ? _settings.Current.PinnedDevices : [])
            .Where(pin => byId.ContainsKey(pin.DeviceId))
            .Select(pin => new DeviceItem(byId[pin.DeviceId], style) { IsPinned = true }));

        RecentlyViewed.ReplaceAll(_bookmarks.RecentlyViewed);

        // The extras, only for the cards that show them; one failing doesn't stop the rest.
        await Task.WhenAll(
            Shows(DashboardLayout.Sensors) ? LoadSensorsAsync(byId, style) : Task.CompletedTask,
            Shows(DashboardLayout.Graph) ? LoadGraphAsync(byId, style) : Task.CompletedTask,
            Shows(DashboardLayout.Wireless) ? LoadWirelessAsync(devices, style) : Task.CompletedTask);

        LastUpdated = DateTime.Now;
        OnPropertyChanged(nameof(HasNoAlerts));
        OnPropertyChanged(nameof(HasPinnedDevices));
        OnPropertyChanged(nameof(HasRecentlyViewed));
        OnPropertyChanged(nameof(HasOpenAlerts));
        OnPropertyChanged(nameof(CriticalShare));
        OnPropertyChanged(nameof(WarningShare));
        OnPropertyChanged(nameof(AcknowledgedShare));
        OnPropertyChanged(nameof(HasNoPinnedSensors));
        OnPropertyChanged(nameof(GraphNeedsSetUp));
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

    [RelayCommand]
    private Task PickSensorsAsync() => _navigation.GoToAsync(Routes.PickSensors);

    [RelayCommand]
    private Task PickGraphAsync() => _navigation.GoToAsync(Routes.PickGraph);

    /// <summary>The Graph card opens the device's graphs page.</summary>
    [RelayCommand]
    private Task OpenGraphAsync() => GraphWidget?.GraphDeviceId is { } id
        ? _navigation.GoToAsync(Routes.DeviceGraphs, new Dictionary<string, object> { [Routes.DeviceIdParameter] = id })
        : PickGraphAsync();

    private async Task LoadSensorsAsync(IReadOnlyDictionary<int, Device> devices, DeviceNameStyle style)
    {
        var pinned = Widget(DashboardLayout.Sensors)?.Sensors ?? [];
        if (pinned.Count == 0)
        {
            PinnedSensors.ReplaceAll([]);
            return;
        }

        try
        {
            var all = (await _client.Sensors.ListAsync()).ToDictionary(s => s.SensorId);
            var settings = _settings.Current;

            // In the order they were added; one LibreNMS has since dropped just doesn't show.
            PinnedSensors.ReplaceAll(pinned
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
        catch (LibreNmsApiException)
        {
            // Kept as they were; the rest of the dashboard still refreshes.
        }
    }

    private async Task LoadGraphAsync(IReadOnlyDictionary<int, Device> devices, DeviceNameStyle style)
    {
        if (GraphWidget is not { GraphDeviceId: { } deviceId, GraphName: { } graph } widget)
        {
            GraphPage = null;
            GraphTitle = "Graph";
            return;
        }

        var deviceName = devices.TryGetValue(deviceId, out var device) ? new DeviceItem(device, style).Name : $"Device {deviceId}";
        GraphTitle = string.IsNullOrWhiteSpace(widget.Title) || widget.Title == DashboardLayout.KindOf(DashboardLayout.Graph).Title
            ? $"{graph} · {deviceName}"
            : widget.Title;

        try
        {
            var range = new GraphTimeRange(widget.GraphTimeRangePreset == GraphTimeRangePreset.Custom ? GraphTimeRangePreset.Day : widget.GraphTimeRangePreset);
            var svg = await _client.Graphs.GetSvgAsync(deviceId, graph, range, GraphWidth, GraphHeight);
            GraphPage = GraphHtml.Build(svg, DarkTheme);
        }
        catch (LibreNmsApiException)
        {
            GraphPage = null;
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

    private bool Shows(string type) => Cards.Any(c => c.Type == type);

    private DashboardWidget? Widget(string type) =>
        DashboardLayout.Current(_settings.Current).FirstOrDefault(w => w.WidgetType == type);

    private double Share(int count)
    {
        var total = CriticalAlerts + WarningAlerts + AcknowledgedAlerts;
        return total == 0 ? 0 : (double)count / total;
    }

    private Task OpenAsync(int deviceId) =>
        _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = deviceId });
}
