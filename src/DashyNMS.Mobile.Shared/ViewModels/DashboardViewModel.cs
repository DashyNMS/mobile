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

    /// <summary>Just added from the card picker: outlined for a moment where it lands (#140).</summary>
    [ObservableProperty]
    private bool _isHighlighted;

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

    // ------------------------------------------------- Needs attention's chips (#140)

    /// <summary>Desktop's Alerts widget's title chips: which severities show, and acknowledged ones too.</summary>
    public bool ShowsCritical => Widget.AlertsShowCritical;

    public bool ShowsWarning => Widget.AlertsShowWarning;

    public bool IncludesAcknowledged => Widget.AlertsIncludeAcknowledged;

    [RelayCommand]
    private void ToggleCritical() => ChangeAlerts(() => Widget.AlertsShowCritical = !Widget.AlertsShowCritical);

    [RelayCommand]
    private void ToggleWarning() => ChangeAlerts(() => Widget.AlertsShowWarning = !Widget.AlertsShowWarning);

    [RelayCommand]
    private void ToggleAcknowledged() => ChangeAlerts(() => Widget.AlertsIncludeAcknowledged = !Widget.AlertsIncludeAcknowledged);

    private void ChangeAlerts(Action change)
    {
        change();
        OnPropertyChanged(nameof(ShowsCritical));
        OnPropertyChanged(nameof(ShowsWarning));
        OnPropertyChanged(nameof(IncludesAcknowledged));
        Dashboard.AlertFiltersChanged();
    }

    // ------------------------------------------------- Top cards' title and headings (#140)

    /// <summary>"Top 5" - the title's chip, as desktop's "Top 5 ▾".</summary>
    public string TopCountText => $"Top {TopCards.CountOf(Widget)}";

    /// <summary>
    /// A heading tapped, as desktop's: In or Out ranks by it; tapped again,
    /// by the two together.
    /// </summary>
    [RelayCommand]
    private void RankHeading(string? heading)
    {
        if (Enum.TryParse<RankBy>(heading, ignoreCase: true, out var by))
        {
            Rank((Widget.TopRankBy == by ? RankBy.Total : by).ToString());
        }
    }

    [RelayCommand]
    private Task ChooseTopCountAsync() => Dashboard.ChooseTopCountAsync(this);

    // ------------------------------------------------- Graph card's range (#140)

    /// <summary>"Day ▾" - the title's chip, as desktop's graph widget shows its range.</summary>
    public string GraphRangeText => (GraphPickerViewModel.RangeChoices.FirstOrDefault(r => r.Preset == Widget.GraphTimeRangePreset)?.Label ?? "Day") + " ▾";

    [RelayCommand]
    private Task ChooseGraphRangeAsync() => Dashboard.ChooseGraphRangeAsync(this);

    internal void RefreshOptions()
    {
        OnPropertyChanged(nameof(TopCountText));
        OnPropertyChanged(nameof(GraphRangeText));
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
public sealed partial class DashboardViewModel : ViewModelBase, IRefreshable
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
    /// <summary>Set once the first run of the welcome card's version has checked for an old dashboard to keep (#140).</summary>
    private const string PreviousDefaultsCheckedKey = "dashboard.previousDefaultsChecked";

    private readonly DashboardToast _toast;
    private readonly IDialogService? _dialogs;
    private IReadOnlyList<Alert> _openAlerts = [];
    private IReadOnlyDictionary<int, Device> _devicesById = new Dictionary<int, Device>();
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

    /// <summary>"1,016" - the Wireless card's headline, the fleet's clients first, as desktop's.</summary>
    [ObservableProperty]
    private string _wirelessClientsText = string.Empty;

    /// <summary>"clients on 84 access points".</summary>
    [ObservableProperty]
    private string _wirelessClientsNote = string.Empty;

    public DashboardViewModel(
        ILibreNmsClient client,
        ISettingsStore settings,
        INavigationService navigation,
        DeviceBookmarks bookmarks,
        MaintenanceScan? maintenance = null,
        DashboardToast? toast = null,
        DashboardWelcome? welcome = null,
        IAppPreferences? preferences = null,
        IDialogService? dialogs = null,
        BackupAddressStatus? backup = null)
    {
        _dialogs = dialogs;
        Backup = backup;

        // Back on the server address (#114): everything again from there.
        if (backup is not null)
        {
            backup.SwitchedBack += (_, _) => _ = RefreshCommand.ExecuteAsync(null);
        }

        _client = client;
        _settings = settings;
        _navigation = navigation;
        _bookmarks = bookmarks;
        _maintenance = maintenance ?? new MaintenanceScan(client, TimeProvider.System);
        preferences ??= new InMemoryPreferences();
        _toast = toast ?? new DashboardToast(settings);
        Welcome = welcome ?? new DashboardWelcome(settings, navigation, preferences);

        KeepPreviousDefaultsOnce(preferences);

        _toast.LayoutRestored += (_, _) => _ = RefreshCommand.ExecuteAsync(null);
        _toast.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DashboardToast.HighlightId))
            {
                Highlight();
            }
        };

        ApplyLayout();
    }

    /// <summary>On the server's backup address (#114): the amber pill under the header, and the way back.</summary>
    public BackupAddressStatus? Backup { get; }

    /// <summary>The empty dashboard's welcome card (#140).</summary>
    public DashboardWelcome Welcome { get; }

    /// <summary>"Wireless removed · Undo", "Top errors added" - shared with Edit dashboard and the card picker.</summary>
    public DashboardToast Toast => _toast;

    /// <summary>No cards, and the welcome card wasn't turned off: show it.</summary>
    public bool ShowWelcome => Cards.Count == 0 && !_settings.Current.WelcomeDismissed;

    /// <summary>No cards, and no welcome card either: just say how to add one.</summary>
    public bool ShowEmpty => Cards.Count == 0 && _settings.Current.WelcomeDismissed;

    /// <summary>
    /// An install from before the welcome card that never changed its cards
    /// keeps the dashboard it had - checked once (see <see cref="DashboardLayout.KeepPreviousDefaults"/>).
    /// </summary>
    private void KeepPreviousDefaultsOnce(IAppPreferences preferences)
    {
        if (preferences.Get(PreviousDefaultsCheckedKey) is not null)
        {
            return;
        }

        if (DashboardLayout.KeepPreviousDefaults(_settings.Current))
        {
            _settings.Save();
        }

        preferences.Set(PreviousDefaultsCheckedKey, "1");
    }

    /// <summary>"Use the starter dashboard": desktop's set, laid out as desktop lays it out (#140).</summary>
    [RelayCommand]
    private async Task UseStarterAsync()
    {
        var before = _toast.Before();
        DashboardLayout.UseStarter(_settings.Current);
        _settings.Save();
        _toast.Show("Starter dashboard added", before);
        await RefreshCommand.ExecuteAsync(null);
    }

    /// <summary>"Choose cards…" and the empty dashboard's Add card: the card picker.</summary>
    [RelayCommand]
    private Task ChooseCardsAsync() => _navigation.GoToAsync(Routes.AddCard);

    /// <summary>"Don't show again" - desktop's own setting, so it stays off there too.</summary>
    [RelayCommand]
    private void DismissWelcome()
    {
        _settings.Current.WelcomeDismissed = true;
        _settings.Save();
        OnPropertyChanged(nameof(ShowWelcome));
        OnPropertyChanged(nameof(ShowEmpty));
    }

    private void Highlight()
    {
        foreach (var card in Cards)
        {
            card.IsHighlighted = card.Widget.Id == _toast.HighlightId;
        }
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

    /// <summary>As desktop's Recently viewed widget, newest first: each device's state, hardware and location, and when it was opened.</summary>
    public BulkObservableCollection<RecentDeviceRow> RecentlyViewed { get; } = new();

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

        Highlight();
        Welcome.RebuildSteps();
        OnPropertyChanged(nameof(ShowWelcome));
        OnPropertyChanged(nameof(ShowEmpty));
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
        _devicesById = byId;

        var alerts = alertsTask.Result;
        var active = alerts.Where(a => !a.IsAcknowledged).ToList();
        CriticalAlerts = active.Count(a => a.Severity == AlertSeverity.Critical);
        WarningAlerts = active.Count(a => a.Severity == AlertSeverity.Warning);
        AcknowledgedAlerts = alerts.Count - active.Count;

        OkAlerts = active.Count(a => a.Severity is not (AlertSeverity.Critical or AlertSeverity.Warning));
        Welcome.ShowCounts(devices.Count, active.Count);

        // Last, and without holding up the rest: one request per device.
        if (Shows(DashboardLayout.DeviceStatus))
        {
            MaintenanceCounted = CountMaintenanceAsync(devices);
        }

        _openAlerts = alerts;
        ShowTopAlerts();

        // Pinned in the order they were pinned, as desktop; a pin whose device
        // has since gone from LibreNMS just doesn't show.
        var style = _settings.Current.DeviceNameStyle;
        PinnedDevices.ReplaceAll((_bookmarks.PinningEnabled ? _settings.Current.PinnedDevices : [])
            .Where(pin => byId.ContainsKey(pin.DeviceId))
            .Select(pin => new DeviceItem(byId[pin.DeviceId], style) { IsPinned = true }));

        var now = DateTimeOffset.Now;
        RecentlyViewed.ReplaceAll(_bookmarks.RecentlyViewed
            .Select(r => new RecentDeviceRow(r, byId.TryGetValue(r.DeviceId, out var device) ? new DeviceItem(device, style) : null, now))
            .ToList());

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
    private Task OpenRecentAsync(RecentDeviceRow? recent) => recent is null ? Task.CompletedTask : OpenAsync(recent.DeviceId);

    /// <summary>
    /// The alerts that most need looking at, through the Needs attention
    /// card's chips - desktop's Alerts widget's: critical, warning, and
    /// whether acknowledged ones count.
    /// </summary>
    private void ShowTopAlerts()
    {
        var widget = CardsOf(DashboardLayout.Alerts).FirstOrDefault()?.Widget ?? new DashboardWidget();
        TopAlerts.ReplaceAll(_openAlerts
            .Where(a => widget.AlertsIncludeAcknowledged || !a.IsAcknowledged)
            .Where(a => (widget.AlertsShowCritical && a.Severity == AlertSeverity.Critical)
                        || (widget.AlertsShowWarning && a.Severity == AlertSeverity.Warning))
            .OrderByDescending(a => a.Severity.SortRank())
            .ThenByDescending(a => a.Timestamp)
            .Take(TopAlertCount)
            .Select(alert => AlertItem.For(alert, _settings.Current, _devicesById))
            .ToList());
        OnPropertyChanged(nameof(HasNoAlerts));
    }

    /// <summary>A Needs attention chip changed: saved, and the list narrowed without asking LibreNMS again.</summary>
    internal void AlertFiltersChanged()
    {
        _settings.Save();
        ShowTopAlerts();
    }

    /// <summary>A Top card's "Top 5" chip: how many rows, from desktop's choices.</summary>
    internal async Task ChooseTopCountAsync(DashboardCard card)
    {
        var labels = TopCards.CountChoices.Select(c => $"Top {c}").ToList();
        int? chosen = _dialogs is null
            ? TopCards.CountChoices[(TopCards.CountChoices.ToList().IndexOf(TopCards.CountOf(card.Widget)) + 1) % TopCards.CountChoices.Count]
            : await _dialogs.ChooseAsync("Rows", labels) is { } label && labels.IndexOf(label) is var index and >= 0 ? TopCards.CountChoices[index] : null;

        if (chosen is { } count && count != card.Widget.TopCount)
        {
            card.Widget.TopCount = count;
            card.RefreshOptions();
            TopOptionsChanged(card);
        }
    }

    /// <summary>A Graph card's range chip: saved, and that graph drawn again.</summary>
    internal async Task ChooseGraphRangeAsync(DashboardCard card)
    {
        if (_dialogs is null || card.Widget.GraphDeviceId is null)
        {
            return;
        }

        var labels = GraphPickerViewModel.RangeChoices.Select(r => r.Label).ToList();
        if (await _dialogs.ChooseAsync("Time range", labels) is { } label && labels.IndexOf(label) is var index and >= 0)
        {
            card.Widget.GraphTimeRangePreset = GraphPickerViewModel.RangeChoices[index].Preset;
            _settings.Save();
            card.RefreshOptions();
            await LoadGraphAsync(card, _devicesById, _settings.Current.DeviceNameStyle);
        }
    }

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

    /// <summary>
    /// A Graph card opens its device's graphs page - or a port graph, that
    /// port's (#172) - or, not set up yet, its set-up.
    /// </summary>
    [RelayCommand]
    private Task OpenGraphAsync(DashboardCard? card)
    {
        if (card?.Widget is not { GraphDeviceId: { } id } widget)
        {
            return PickGraphAsync(card);
        }

        var parameters = new Dictionary<string, object>
        {
            [Routes.DeviceIdParameter] = id,

            // On the card's own graph and range, not the device's first (#119).
            [Routes.GraphParameter] = widget.GraphName ?? string.Empty,
            [Routes.GraphRangeParameter] = widget.GraphTimeRangePreset,
        };

        if (widget.GraphPortIfName is { } port)
        {
            parameters[Routes.PortParameter] = port;
            parameters[Routes.PortNameParameter] = port;
        }

        return _navigation.GoToAsync(Routes.DeviceGraphs, parameters);
    }

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

        // A port's graph (#172) - desktop's Graph widget can show one, and the
        // layout is shared: "port_bits" isn't one of the device's own graphs,
        // so it's fetched as the port's.
        var port = widget.GraphPortIfName;
        if (!DashboardLayout.HasOwnTitle(widget))
        {
            var name = port is null ? graph : DeviceGraphsViewModel.PortGraphs.FirstOrDefault(g => g.Name == graph)?.Description ?? graph;
            card.Title = port is null ? $"{name} · {deviceName}" : $"{name} · {port} · {deviceName}";
        }
        else
        {
            card.Title = widget.Title;
        }

        try
        {
            var range = new GraphTimeRange(widget.GraphTimeRangePreset == GraphTimeRangePreset.Custom ? GraphTimeRangePreset.Day : widget.GraphTimeRangePreset);
            var svg = port is not null
                ? await _client.Graphs.GetPortSvgAsync(deviceId, port, graph, range, GraphWidth, GraphHeight)
                : await _client.Graphs.GetSvgAsync(deviceId, graph, range, GraphWidth, GraphHeight);
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
            : $"{rows.Count} {(rows.Count == 1 ? "controller" : "controllers")}";
        var totalAps = rows.Count == 0 ? 0 : controllers
            .Where(d => readings.ContainsKey(d.DeviceId))
            .Sum(d => WirelessFleet.Summarise(d.DeviceId, readings[d.DeviceId]).ApCount ?? 0);
        WirelessClientsText = rows.Count == 0 ? string.Empty : totalClients.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
        WirelessClientsNote = rows.Count == 0 ? string.Empty
            : totalAps > 0 ? $"clients on {totalAps:N0} access points" : "clients";
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
