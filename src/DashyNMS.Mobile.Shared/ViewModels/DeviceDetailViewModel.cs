using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// One device, as desktop's Device View: its state, identity and open
/// alerts, the way into each section (sensors, ports, graphs...), and the
/// actions - pin, rediscover, schedule maintenance, SSH and Telnet.
/// </summary>
public sealed partial class DeviceDetailViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly ILauncherService _launcher;
    private readonly DeviceBookmarks _bookmarks;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly Graylog.GraylogSetup? _graylog;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(StateText), nameof(State), nameof(UptimeText), nameof(Properties), nameof(PinText), nameof(FreshnessText), nameof(Subtitle), nameof(TypeText))]
    [NotifyCanExecuteChangedFor(nameof(TogglePinCommand), nameof(RediscoverCommand), nameof(ScheduleMaintenanceCommand), nameof(OpenSshCommand), nameof(OpenTelnetCommand))]
    private Device? _device;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinText))]
    private bool _isPinned;

    public DeviceDetailViewModel(
        ILibreNmsClient client,
        ISettingsStore settings,
        ILauncherService launcher,
        DeviceBookmarks bookmarks,
        IDialogService dialogs,
        INavigationService navigation,
        Graylog.GraylogSetup? graylog = null,
        DeviceSectionLoader? sections = null)
    {
        _client = client;
        _settings = settings;
        _launcher = launcher;
        _bookmarks = bookmarks;
        _dialogs = dialogs;
        _navigation = navigation;
        _graylog = graylog;
        _sectionLoader = sections;
        BuildSectionCards();
    }

    private readonly DeviceSectionLoader? _sectionLoader;
    private IReadOnlyList<DeviceSectionCard> _sectionCards = [];
    private CancellationTokenSource? _sectionsCts;

    /// <summary>
    /// Desktop's Device View tabs as cards, each with a quick view and
    /// opening on its own page (#43) - Graylog's too, once it's set up. Only
    /// the ones with something to show, as desktop hides empty tabs (#44).
    /// </summary>
    public BulkObservableCollection<DeviceSectionCard> SectionCards { get; } = new();

    /// <summary>The sections listed, in order.</summary>
    public IReadOnlyList<DeviceSectionInfo> Sections => SectionCards.Select(c => c.Info).ToList();

    /// <summary>
    /// What the page's card grid shows: ping and active alerts, then the
    /// sections - one grid, so a three-column tablet has no gap after the
    /// first two (#69). The page picks each one's template by type.
    /// </summary>
    public IReadOnlyList<object> GridCards => [DeviceOverviewCard.Ping, DeviceOverviewCard.Alerts, .. SectionCards];

    /// <summary>Awaited by tests: every section's quick view has loaded (or failed).</summary>
    internal Task SectionsLoaded { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Desktop's Overview cards first, in its order - availability,
    /// resources, sensors, connected to, busiest ports - then its other
    /// sections as they come in its sidebar.
    /// </summary>
    internal static readonly DeviceSection[] CardOrder =
    [
        DeviceSection.Availability,
        DeviceSection.Resources,
        DeviceSection.Sensors,
        DeviceSection.Neighbours,
        DeviceSection.Ports,
        DeviceSection.Graphs,
        DeviceSection.Vlans,
        DeviceSection.Fdb,
        DeviceSection.Arp,
        DeviceSection.Routing,
        DeviceSection.Wireless,
        DeviceSection.Inventory,
        DeviceSection.EventLog,
        DeviceSection.Graylog,
    ];

    private void BuildSectionCards()
    {
        IEnumerable<DeviceSectionInfo> infos = (_graylog?.IsConfigured == true
                ? [.. DeviceSectionInfo.All, DeviceSectionInfo.Graylog]
                : DeviceSectionInfo.All)
            .OrderBy(info => Array.IndexOf(CardOrder, info.Section) is var i and >= 0 ? i : int.MaxValue);

        foreach (var card in _sectionCards)
        {
            card.PropertyChanged -= OnSectionCardChanged;
        }

        _sectionCards = infos.Select(info => new DeviceSectionCard(info)).ToList();
        foreach (var card in _sectionCards)
        {
            card.PropertyChanged += OnSectionCardChanged;
        }

        ShowVisibleSections();
    }

    private void OnSectionCardChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DeviceSectionCard.IsVisible))
        {
            ShowVisibleSections();
        }
        else if (e.PropertyName == nameof(DeviceSectionCard.Groups))
        {
            RaiseTiles();
        }
    }

    // ------------------------------------------------------------ stat tiles

    /// <summary>A loaded section's card, for the tiles.</summary>
    private DeviceSectionCard? Card(DeviceSection section) => _sectionCards.FirstOrDefault(c => c.Info.Section == section);

    /// <summary>Desktop's Availability tile: the 30-day figure, "—" until it's in.</summary>
    public string AvailabilityTileText =>
        Card(DeviceSection.Availability) is { } card && DeviceSectionCard.ThirtyDayWindow(card.Groups) is { } window
            ? window.Value ?? "—"
            : "—";

    /// <summary>Green from 99.9%, amber from 99%, red below - desktop's thresholds, as the section colours them.</summary>
    public RowStatus AvailabilityTileStatus =>
        Card(DeviceSection.Availability) is { } card && DeviceSectionCard.ThirtyDayWindow(card.Groups) is { } window
            ? window.Status
            : RowStatus.None;

    /// <summary>Desktop's Ports up tile: "46 / 52".</summary>
    public string PortsTileText => Card(DeviceSection.Ports) is { Groups.Count: > 0 } card
        ? $"{card.Groups.FirstOrDefault(g => g.Name == "Up")?.Count ?? 0} / {card.Count}"
        : "—";

    /// <summary>Green with every port up, amber with any down - desktop's.</summary>
    public RowStatus PortsTileStatus => Card(DeviceSection.Ports) is { Groups.Count: > 0 } card
        ? card.Groups.Any(g => g.Name == "Down" && g.Count > 0) ? RowStatus.Warning : RowStatus.Ok
        : RowStatus.None;

    /// <summary>Red with any critical, amber with other alerts, green with none - desktop's.</summary>
    public RowStatus AlertsTileStatus =>
        Alerts.Any(a => a.Severity == AlertSeverity.Critical && !a.IsAcknowledged) ? RowStatus.Critical
        : Alerts.Count > 0 ? RowStatus.Warning
        : RowStatus.Ok;

    private void RaiseTiles()
    {
        OnPropertyChanged(nameof(AvailabilityTileText));
        OnPropertyChanged(nameof(AvailabilityTileStatus));
        OnPropertyChanged(nameof(PortsTileText));
        OnPropertyChanged(nameof(PortsTileStatus));
        OnPropertyChanged(nameof(AlertsTileStatus));
    }

    [RelayCommand]
    private Task OpenTileAsync(string? section) =>
        Enum.TryParse<DeviceSection>(section, out var parsed)
            ? OpenSectionAsync(DeviceSectionInfo.For(parsed))
            : Task.CompletedTask;

    private void ShowVisibleSections()
    {
        SectionCards.ReplaceAll(_sectionCards.Where(c => c.IsVisible).ToList());
        OnPropertyChanged(nameof(Sections));
        OnPropertyChanged(nameof(GridCards));
    }

    /// <summary>
    /// Every section's quick view, all at once and in the background, as
    /// desktop loads its tabs - the page is already showing the device.
    /// </summary>
    private async Task LoadSectionsAsync(CancellationToken cancellationToken)
    {
        if (_sectionLoader is null)
        {
            return;
        }

        await Task.WhenAll(_sectionCards.Where(c => c.HasQuickView).Select(async card =>
        {
            card.IsLoading = true;
            try
            {
                var groups = await _sectionLoader.LoadAsync(card.Info.Section, DeviceId, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    card.Show(groups);
                }
            }
            catch (OperationCanceledException)
            {
                // A newer refresh (or another device) took over.
            }
            catch (Exception)
            {
                card.Failed();
            }
            finally
            {
                card.IsLoading = false;
            }
        }));
    }

    /// <summary>"took 4.2s": how long LibreNMS's last poll of the device ran.</summary>
    internal static string? TookText(double? seconds) => seconds is { } s && s >= 0
        ? "took " + s.ToString(s < 10 ? "0.#" : "0", CultureInfo.CurrentCulture) + "s"
        : null;

    /// <summary>
    /// The foot of the page: "Last polled 3m ago" (#97) - polls run every few
    /// minutes, so it says whether what's shown is current. A server that
    /// doesn't send the poll time gets "Last discovered 3h ago", as before.
    /// </summary>
    public string? FreshnessText => Device switch
    {
        null => null,
        { LastPolled: { } polled } => "Last polled " + Formatting.Age(DesktopNMS.Core.ServerTime.Age(polled, _settings.Current.ServerTimestampsAreUtc)),
        { LastDiscovered: { } discovered } => "Last discovered " + Formatting.Age(DesktopNMS.Core.ServerTime.Age(discovered, _settings.Current.ServerTimestampsAreUtc)),
        _ => null,
    };

    public int DeviceId { get; private set; }

    /// <summary>Following desktop's hostname / sysName / display name preference.</summary>
    public string Title => Device is null ? "Device" : _settings.Current.DeviceNameStyle.Resolve(Device, Device.Hostname);

    public string PinText => IsPinned ? "Unpin" : "Pin";

    public DeviceState? State => Device?.State;

    public string StateText => Device?.State.ToDisplayString() ?? string.Empty;

    /// <summary>Desktop's Uptime tile: a dash for a device that isn't up, as uptime means nothing then.</summary>
    public string UptimeText => Device is { State: DeviceState.Up } up ? Formatting.Uptime(up.Uptime) : "—";

    /// <summary>Under the name, as desktop's header: "C9300-48P · iosxe · 192.0.2.10", leaving out what isn't known.</summary>
    public string Subtitle => Device is null ? string.Empty
        : string.Join(" · ", new[] { Device.Hardware, Device.Os, Device.Ip }.Where(p => !string.IsNullOrWhiteSpace(p)));

    /// <summary>"Network", "Server"... - LibreNMS's lowercase type, capitalised as the Devices tab shows it.</summary>
    public string? TypeText => Device?.Type is { Length: > 0 } type
        ? char.ToUpper(type[0], CultureInfo.CurrentCulture) + type[1..]
        : null;

    private IReadOnlyList<string> _groups = [];

    /// <summary>
    /// Desktop's Device card: what the device is and where, each only when
    /// LibreNMS knows it (a card full of dashes is noise). Its other names
    /// in one "Also known as" row, only when there are any, as desktop's.
    /// </summary>
    public IReadOnlyList<DeviceProperty> Properties
    {
        get
        {
            if (Device is not { } d)
            {
                return [];
            }

            var utc = _settings.Current.ServerTimestampsAreUtc;

            // Its other names in one row, as desktop's Overview (#116): Core's
            // AlsoKnownAs leaves out the name in the title and any that are
            // really the same ("core-sw-01" and "CORE-SW-01.example.net").
            var alsoKnownAs = DeviceNameStyleExtensions.AlsoKnownAs(d, Title);

            return new (string Label, string? Value)[]
                {
                    ("Also known as", alsoKnownAs.Count > 0 ? string.Join(", ", alsoKnownAs) : null),
                    ("IP address", d.Ip),
                    ("Operating system", string.Join(" ", new[] { d.Os, d.Version }.Where(p => !string.IsNullOrWhiteSpace(p)))),
                    ("Hardware", d.Hardware),
                    ("Serial", d.Serial),
                    ("Type", TypeText),
                    ("Purpose", d.Purpose),
                    ("Location", d.LocationName()),
                    ("Contact", d.Contact),
                    ("Groups", _groups.Count > 0 ? string.Join(", ", _groups) : null),
                    ("Depends on", d.DependencyParentHostname),
                    ("Added", d.Inserted is { } added ? DesktopNMS.Core.ServerTime.ToLocal(added, utc).ToString("d MMM yyyy", CultureInfo.CurrentCulture) : null),
                    ("Last discovered", d.LastDiscovered is { } at ? Formatting.Age(DesktopNMS.Core.ServerTime.Age(at, utc)) : null),
                    ("Last polled", d.LastPolled is { } polled
                        ? string.Join(", ", new[] { Formatting.Age(DesktopNMS.Core.ServerTime.Age(polled, utc)), TookText(d.LastPolledTimeTaken) }.Where(p => p is not null))
                        : null),
                    ("Object ID", d.SysObjectId),
                    ("Description", d.SysDescr),
                }
                .Where(p => !string.IsNullOrWhiteSpace(p.Value))
                .Select(p => new DeviceProperty(p.Label, p.Value!.Trim())
                {
                    // As desktop's Groups & locations: tap through to the devices there.
                    Link = p.Label switch
                    {
                        "Location" => DevicePropertyLink.Location,
                        "Groups" => DevicePropertyLink.Groups,
                        _ => DevicePropertyLink.None,
                    },
                })
                .ToList();
        }
    }

    // ------------------------------------------------------------ ping

    /// <summary>Set by the page from the phone's theme, for the ping graph.</summary>
    public bool DarkTheme { get; set; }

    /// <summary>
    /// A tapped Location or Groups row: the Devices page showing only the
    /// devices there, as desktop's Groups &amp; locations. Several groups ask
    /// which first.
    /// </summary>
    [RelayCommand]
    private async Task OpenPropertyAsync(DeviceProperty? property)
    {
        switch (property?.Link)
        {
            case DevicePropertyLink.Location:
                await _navigation.GoToAsync(Routes.Devices, new Dictionary<string, object> { [Routes.LocationParameter] = property.Value });
                break;

            case DevicePropertyLink.Groups:
                var group = _groups.Count == 1 ? _groups[0] : await _dialogs.ChooseAsync("Show the devices in", _groups);
                if (group is not null)
                {
                    await _navigation.GoToAsync(Routes.Devices, new Dictionary<string, object> { [Routes.GroupParameter] = group });
                }

                break;
        }
    }

    /// <summary>LibreNMS's ping graph, as desktop's Ping response card draws.</summary>
    internal const string PingGraphName = "device_icmp_perf";

    /// <summary>LibreNMS's own 24h ping graph (<c>device_icmp_perf</c>), as desktop's Ping response card; null until it's in.</summary>
    [ObservableProperty]
    private string? _pingGraphPage;

    /// <summary>
    /// The ping graph opens the device's graphs, as desktop's does - on the
    /// ping graph itself, over the day it showed, not the first graph (#119).
    /// </summary>
    [RelayCommand]
    private Task OpenGraphsAsync() => _navigation.GoToAsync(Routes.DeviceGraphs, new Dictionary<string, object>
    {
        [Routes.DeviceIdParameter] = DeviceId,
        [Routes.SectionParameter] = DeviceSection.Graphs,
        [Routes.DeviceNameParameter] = Title,
        [Routes.GraphParameter] = PingGraphName,
        [Routes.GraphRangeParameter] = GraphTimeRangePreset.Day,
    });

    /// <summary>Best effort: a device LibreNMS doesn't ping just has no card.</summary>
    private async Task LoadPingGraphAsync(int deviceId)
    {
        try
        {
            var svg = await _client.Graphs.GetSvgAsync(deviceId, PingGraphName, new GraphTimeRange(GraphTimeRangePreset.Day), 800, 400);
            PingGraphPage = DeviceId == deviceId ? GraphHtml.Build(svg, DarkTheme) : PingGraphPage;
        }
        catch (Exception)
        {
            PingGraphPage = null;
        }
    }

    /// <summary>The device's groups, for the Device card - best effort, as a token may not read them.</summary>
    private async Task LoadGroupsAsync(int deviceId)
    {
        try
        {
            var groups = await _client.DeviceGroups.ListForDeviceAsync(deviceId);
            _groups = groups.Select(g => g.Name).Where(n => !string.IsNullOrWhiteSpace(n)).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        catch (Exception)
        {
            _groups = [];
        }

        OnPropertyChanged(nameof(Properties));
    }

    public BulkObservableCollection<AlertItem> Alerts { get; } = new();

    public bool HasAlerts => Alerts.Count > 0;

    /// <summary>Loads (or reloads) <paramref name="deviceId"/>.</summary>
    public Task LoadAsync(int deviceId)
    {
        DeviceId = deviceId;
        return RefreshAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var deviceTask = _client.Devices.GetAsync(DeviceId.ToString(CultureInfo.InvariantCulture));
        var alertsTask = _client.Alerts.ListAsync(AlertQuery.Open);
        var graylogTask = _graylog?.EnsureConfiguredAsync() ?? Task.FromResult(false);
        await Task.WhenAll(deviceTask, alertsTask, graylogTask);
        Device = deviceTask.Result;
        if (Device is null)
        {
            ErrorMessage = "LibreNMS no longer has this device.";
            SectionCards.ReplaceAll([]);
            OnPropertyChanged(nameof(Sections));
            OnPropertyChanged(nameof(GridCards));
        }
        else
        {
            // Onto the Devices tab's recently viewed strip, as desktop's Device View does.
            _bookmarks.RecordViewed(DeviceId, Title);
            IsPinned = _bookmarks.IsPinned(DeviceId);

            // Fresh cards each refresh (Graylog may have been set up since),
            // loading behind the device's own details.
            _sectionsCts?.Cancel();
            _sectionsCts = new CancellationTokenSource();
            BuildSectionCards();
            SectionsLoaded = LoadSectionsAsync(_sectionsCts.Token);

            // Extras for the Device and Ping response cards, behind the rest.
            Extras = Task.WhenAll(LoadGroupsAsync(DeviceId), LoadPingGraphAsync(DeviceId));
        }

        var utc = _settings.Current.ServerTimestampsAreUtc;
        Alerts.ReplaceAll(alertsTask.Result
            .Where(a => a.DeviceId == DeviceId)
            .OrderByDescending(a => a.Severity.SortRank())
            .ThenByDescending(a => a.Timestamp)
            .Select(alert => new AlertItem(alert, utc, Device is null ? null : Title)));

        OnPropertyChanged(nameof(HasAlerts));
        OnPropertyChanged(nameof(ActiveAlertsNote));
        RaiseTiles();
    });

    /// <summary>Awaited by tests: the groups and ping graph, loaded after the device.</summary>
    internal Task Extras { get; private set; } = Task.CompletedTask;

    /// <summary>The Active alerts card's note, as desktop's: "2 open".</summary>
    public string ActiveAlertsNote => Alerts.Count == 0 ? "None" : $"{Alerts.Count} open";

    private bool CanTogglePin() => Device is not null && _bookmarks.PinningEnabled;

    /// <summary>Pinned devices stay at the top of the Devices tab.</summary>
    [RelayCommand]
    private Task OpenAlertAsync(AlertItem? item) => item is null ? Task.CompletedTask : _navigation.GoToAlertAsync(item.Alert);

    [RelayCommand(CanExecute = nameof(CanTogglePin))]
    private void TogglePin()
    {
        _bookmarks.SetPinned(DeviceId, Title, !IsPinned);
        IsPinned = _bookmarks.IsPinned(DeviceId);
    }

    [RelayCommand]
    private Task OpenSectionAsync(DeviceSectionInfo? section) => section is null
        ? Task.CompletedTask
        : _navigation.GoToAsync(section.Section switch
        {
            DeviceSection.Graphs => Routes.DeviceGraphs,
            DeviceSection.Graylog => Routes.Graylog,
            _ => Routes.DeviceSection,
        }, new Dictionary<string, object>
        {
            [Routes.DeviceIdParameter] = DeviceId,
            [Routes.SectionParameter] = section.Section,
            [Routes.DeviceNameParameter] = Title,
        });

    private bool HasDevice() => Device is not null;

    /// <summary>Asks LibreNMS to rediscover the device now, as desktop's Rediscover.</summary>
    [RelayCommand(CanExecute = nameof(HasDevice))]
    private async Task RediscoverAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "Rediscover",
            $"Ask LibreNMS to rediscover {Title} now? It runs on the server's next discovery pass.",
            "Rediscover",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        string? message = null;
        if (await RunAsync(async () => message = await _client.Devices.DiscoverAsync(DeviceId)))
        {
            await _dialogs.AlertAsync("Rediscover requested", string.IsNullOrWhiteSpace(message) ? $"{Title} will be rediscovered shortly." : message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasDevice))]
    private Task ScheduleMaintenanceAsync() => _navigation.GoToAsync(Routes.Maintenance, new Dictionary<string, object>
    {
        [Routes.DeviceIdParameter] = DeviceId,
        [Routes.DeviceNameParameter] = Title,
    });

    private bool CanOpenInApp() => DeviceLinks.For(DeviceLinks.Ssh, Device) is not null;

    [RelayCommand(CanExecute = nameof(CanOpenInApp))]
    private Task OpenSshAsync() => OpenInAppAsync(DeviceLinks.Ssh, "SSH");

    [RelayCommand(CanExecute = nameof(CanOpenInApp))]
    private Task OpenTelnetAsync() => OpenInAppAsync(DeviceLinks.Telnet, "Telnet");

    /// <summary>Desktop's "Open in" SSH / Telnet: the phone's own app for the link, or a word on why nothing happened.</summary>
    private async Task OpenInAppAsync(string scheme, string name)
    {
        if (DeviceLinks.For(scheme, Device) is not { } uri)
        {
            return;
        }

        if (!await _launcher.TryOpenAppAsync(uri))
        {
            await _dialogs.AlertAsync(
                $"No {name} app",
                $"Nothing on this phone opens {scheme}:// links. Install an {name} app that does, then try again.");
        }
    }
}
