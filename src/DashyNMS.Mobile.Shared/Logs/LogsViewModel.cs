using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Logs;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Logs;

/// <summary>
/// LibreNMS's event log and alert log, laid out as the Graylog page is (#118):
/// search, then chips for the device, the event's type or the alert's state,
/// and a time range, then the entries in one card. One page for both of:
/// <list type="bullet">
/// <item>Logs, from More or Alerts - every device's, narrowed to one with the Device chip.</item>
/// <item>A device's Event log section (<see cref="Initialise"/> with a device) - the Device chip fixed to it, as desktop's device view.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// LibreNMS's API can filter the logs by device but not by type or text, so
/// the Device chip asks the server and the rest filter what's loaded. To keep
/// a rare type from turning up nothing, a type or a search fetches at least
/// <see cref="EventLogFeed.FilteredFetchLimit"/> events first (#125) -
/// Core's <see cref="EventLogFeed"/>, so the phone matches as desktop does.
/// </para>
/// <para>
/// "Load more" asks for a bigger page (the endpoints have no offset). The
/// alert log names rules from the rule list, best effort - a token that
/// can't read rules gets "Rule 12".
/// </para>
/// </remarks>
public sealed partial class LogsViewModel : ViewModelBase, IRefreshable, IDeviceChipList
{
    internal const int PageSize = 100;

    /// <summary>Far enough back for a phone; past this, desktop or the web UI.</summary>
    internal const int MaxEntries = 1000;

    /// <summary>The time range chip's choices; LibreNMS can't ask by time, so they narrow what's loaded.</summary>
    internal static readonly IReadOnlyList<(string Label, TimeSpan? Span)> Ranges =
    [
        ("Any time", null),
        ("Last hour", TimeSpan.FromHours(1)),
        ("Last 24 hours", TimeSpan.FromDays(1)),
        ("Last 7 days", TimeSpan.FromDays(7)),
    ];

    /// <summary>The State chip's choices, worst first.</summary>
    internal static readonly IReadOnlyList<AlertState> States =
        [AlertState.Active, AlertState.Worse, AlertState.Better, AlertState.Acknowledged, AlertState.Recovered];

    private const string AnyType = "Any type";
    private const string AnyState = "Any state";
    private const string TypeChip = "type";

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ChipMemory _chips;
    private IReadOnlyList<Device>? _devices;
    private IReadOnlyDictionary<int, string> _names = new Dictionary<int, string>();
    private IReadOnlyDictionary<int, string> _rules = new Dictionary<int, string>();
    private IReadOnlyList<EventLogEntry> _events = [];
    private IReadOnlyList<AlertLogEntry> _alerts = [];
    private CancellationTokenSource? _loadCts;
    private int _eventLimit = PageSize;
    private int _alertLimit = PageSize;
    private int _eventsFetched;
    private bool _eventsLoaded;
    private bool _alertsLoaded;
    private bool _eventsExhausted;
    private bool _alertsExhausted;
    private string? _deviceName;
    private bool _suppressLoad;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowingAlertLog), nameof(HasActiveFilters))]
    private bool _showingEventLog = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private string _searchText = string.Empty;

    /// <summary>The Type chip (#125): one type, as LibreNMS wrote it, or null for every type.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TypeChipText), nameof(IsTypeSet), nameof(HasActiveFilters))]
    private string? _eventType;

    /// <summary>The alert log's State chip, or null for every state.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateChipText), nameof(IsStateSet), nameof(HasActiveFilters))]
    private AlertState? _stateFilter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeChipText), nameof(IsRangeSet), nameof(HasActiveFilters))]
    private int _rangeIndex;

    /// <summary>The Device chip: whose entries, or null for every device's.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceChipText), nameof(HasDeviceFilter), nameof(CanClearDevice), nameof(ShowsDevice), nameof(HasActiveFilters))]
    private GraylogDeviceFilter? _device;

    public LogsViewModel(
        ILibreNmsClient client,
        ISettingsStore settings,
        INavigationService navigation,
        IDialogService dialogs,
        IAppPreferences? preferences = null)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
        _dialogs = dialogs;
        _chips = new ChipMemory(preferences ?? new InMemoryPreferences(), "logs");
        _eventType = _chips.GetChoice(TypeChip);
    }

    /// <summary>The phone's clock, for ages and the time range - swapped in tests.</summary>
    internal Func<DateTime> Now { get; set; } = () => DateTime.Now;

    /// <summary>The device this page is fixed to (a device's Event log section), or null.</summary>
    public int? DeviceId { get; private set; }

    /// <summary>A device's Event log: its Device chip can't be changed or cleared.</summary>
    public bool IsDeviceFixed => DeviceId is not null;

    public string Title => IsDeviceFixed ? "Event log" : "Logs";

    /// <summary>Event log or Alert log: a device's section is its event log only.</summary>
    public bool ShowsLogSwitch => !IsDeviceFixed;

    public bool ShowingAlertLog => !ShowingEventLog;

    public BulkObservableCollection<LogEntryItem> Entries { get; } = new();

    public string SearchPlaceholder => ShowingEventLog ? "Search messages, types and devices" : "Search rules and devices";

    // ------------------------------------------------------------------ chips

    public string DeviceChipText => Device?.Name ?? "Device";

    public bool HasDeviceFilter => Device is not null;

    /// <summary>The chip's ×: not on a device's own Event log.</summary>
    public bool CanClearDevice => HasDeviceFilter && !IsDeviceFixed;

    /// <summary>Each row names its device - except when they're all the one device's.</summary>
    public bool ShowsDevice => Device is null;

    public string TypeChipText => EventType ?? "Type";

    public bool IsTypeSet => EventType is not null;

    public string StateChipText => StateFilter?.ToDisplayString() ?? "State";

    public bool IsStateSet => StateFilter is not null;

    public string RangeChipText => Ranges[Math.Clamp(RangeIndex, 0, Ranges.Count - 1)].Label;

    public bool IsRangeSet => RangeIndex != 0;

    /// <summary>Anything Clear would undo, among the log showing's chips.</summary>
    public bool HasActiveFilters =>
        IsRangeSet || CanClearDevice || !string.IsNullOrWhiteSpace(SearchText)
        || (ShowingEventLog ? IsTypeSet : IsStateSet);

    // ------------------------------------------------------------------- list

    private bool Loaded => ShowingEventLog ? _eventsLoaded : _alertsLoaded;

    private int LoadedCount => ShowingEventLog ? _events.Count : _alerts.Count;

    public bool CanLoadMore => Loaded && !IsBusy && (ShowingEventLog
        ? !_eventsExhausted && _eventsFetched < MaxEntries
        : !_alertsExhausted && _alertLimit < MaxEntries);

    public bool IsEmpty => Loaded && !IsBusy && !HasError && Entries.Count == 0;

    /// <summary>Narrowed by anything but the device - the server does that.</summary>
    private bool IsNarrowed => IsRangeSet || !string.IsNullOrWhiteSpace(SearchText) || (ShowingEventLog ? IsTypeSet : IsStateSet);

    /// <summary>"3 interface events from core-sw-01 in the last 250", or "100 events, newest first".</summary>
    public string SummaryText
    {
        get
        {
            if (!Loaded || HasError || LoadedCount == 0)
            {
                return string.Empty;
            }

            var shown = Entries.Count;
            var text = string.Create(CultureInfo.CurrentCulture, $"{shown:N0} {Noun(shown)}{FromDevice}");
            return IsNarrowed
                ? string.Create(CultureInfo.CurrentCulture, $"{text} in the last {LoadedCount:N0}{RangeSuffix}")
                : text + ", newest first";
        }
    }

    public string EmptyTitle => ShowingEventLog ? "No events" : "No alerts logged";

    /// <summary>What was looked through: "No reboot events from core-sw-01 in the last 250."</summary>
    public string EmptyText
    {
        get
        {
            if (!IsNarrowed || LoadedCount == 0)
            {
                return IsDeviceFixed || HasDeviceFilter ? $"Nothing logged for {Device!.Name}." : "Nothing logged.";
            }

            var where = string.Create(CultureInfo.CurrentCulture, $"No {Noun(0)}{FromDevice} in the last {LoadedCount:N0}{RangeSuffix}.");
            return where + (CanLoadMore ? " Look further back, or clear the filters." : " Clear the filters to see the rest.");
        }
    }

    private string FromDevice => Device is { } device && !IsDeviceFixed ? " from " + device.Name : string.Empty;

    private string RangeSuffix => IsRangeSet ? " · " + RangeChipText.ToLower(CultureInfo.CurrentCulture) : string.Empty;

    /// <summary>"interface events", "event", "acknowledged alerts".</summary>
    private string Noun(int count)
    {
        var one = count == 1;
        if (ShowingEventLog)
        {
            return (IsTypeSet ? EventType + " " : string.Empty) + (one ? "event" : "events");
        }

        return IsStateSet
            ? StateFilter!.Value.ToDisplayString().ToLower(CultureInfo.CurrentCulture) + (one ? " alert" : " alerts")
            : one ? "alert log entry" : "alert log entries";
    }

    // ------------------------------------------------------- the Device chip

    public IReadOnlyList<Device> KnownDevices => _devices ?? [];

    public string SendersHeading => "In these entries";

    /// <summary>LibreNMS's logs only know its own devices.</summary>
    public bool SearchesAddresses => false;

    /// <summary>Who the entries on screen are about, busiest first - the Device chooser's suggestions.</summary>
    public IReadOnlyList<(GraylogDeviceFilter Filter, int Count)> Senders() => Entries
        .Where(e => e.HasDevice)
        .GroupBy(e => e.DeviceId)
        .Select(g => (GraylogDeviceFilter.ForDevice(g.Key, g.First().DeviceName), g.Count()))
        .OrderByDescending(s => s.Item2)
        .ThenBy(s => s.Item1.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    /// <summary>Sets the Device chip, from its chooser or an entry's "Only" - null for every device.</summary>
    public void ShowDevice(GraylogDeviceFilter? device)
    {
        if (IsDeviceFixed || (device is null ? Device is null : device.SameAs(Device)))
        {
            return;
        }

        Device = device;
    }

    // ------------------------------------------------------------- setting up

    /// <summary>Every device's logs, or with <paramref name="deviceId"/> only that device's event log.</summary>
    public void Initialise(int? deviceId, string? deviceName)
    {
        if (DeviceId == deviceId && _deviceName == deviceName)
        {
            return;
        }

        DeviceId = deviceId;
        _deviceName = deviceName;
        ForgetLoaded();

        // One load when the page shows, rather than one per change here. A
        // device's log starts unfiltered: the type left on the fleet's is its own.
        _suppressLoad = true;
        Device = deviceId is { } id ? GraylogDeviceFilter.ForDevice(id, deviceName ?? $"Device {id}") : null;
        ShowingEventLog = true;
        if (deviceId is not null)
        {
            EventType = null;
        }

        _suppressLoad = false;

        OnPropertyChanged(nameof(IsDeviceFixed));
        OnPropertyChanged(nameof(CanClearDevice));
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ShowsLogSwitch));
    }

    /// <summary>The log showing, loaded if it hasn't been yet.</summary>
    public Task EnsureLoadedAsync()
    {
        ApplyFilter();
        return Loaded ? Task.CompletedTask : LoadAsync();
    }

    /// <summary>Whether an entry's page offers "Only this device's".</summary>
    public bool CanShowOnlyDevice(LogEntryItem item) => !IsDeviceFixed && item.HasDevice && Device?.DeviceId != item.DeviceId;

    /// <summary>Whether an entry's page offers "Only this type" or "Only this state".</summary>
    public bool CanShowOnlyKind(LogEntryItem item) => item.IsAlert
        ? StateFilter != item.Alert!.State
        : item.Type is { } type && !string.Equals(type, EventType, StringComparison.OrdinalIgnoreCase);

    /// <summary>An entry's "Only core-sw-01's events".</summary>
    public void ShowOnlyDevice(LogEntryItem item)
    {
        if (CanShowOnlyDevice(item))
        {
            ShowDevice(GraylogDeviceFilter.ForDevice(item.DeviceId, item.DeviceName));
        }
    }

    /// <summary>An entry's "Only interface events" or "Only recovered alerts".</summary>
    public void ShowOnlyKind(LogEntryItem item)
    {
        if (!CanShowOnlyKind(item))
        {
            return;
        }

        if (item.IsAlert)
        {
            StateFilter = item.Alert!.State;
        }
        else
        {
            EventType = item.Type;
        }
    }

    // --------------------------------------------------------------- changes

    partial void OnShowingEventLogChanged(bool value)
    {
        OnPropertyChanged(nameof(SearchPlaceholder));
        if (!_suppressLoad)
        {
            _ = EnsureLoadedAsync();
        }
    }

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(FilterOrFetch);

    partial void OnEventTypeChanged(string? value)
    {
        if (!IsDeviceFixed)
        {
            _chips.SetChoice(TypeChip, value);
        }

        if (!_suppressLoad)
        {
            FilterOrFetch();
        }
    }

    partial void OnStateFilterChanged(AlertState? value)
    {
        if (!_suppressLoad)
        {
            ApplyFilter();
        }
    }

    partial void OnRangeIndexChanged(int value) => ApplyFilter();

    /// <summary>The server filters by device: both logs start again.</summary>
    partial void OnDeviceChanged(GraylogDeviceFilter? value)
    {
        ForgetLoaded();
        if (!_suppressLoad)
        {
            _ = LoadAsync();
        }
    }

    [RelayCommand]
    private void ShowEventLog() => ShowingEventLog = true;

    [RelayCommand]
    private void ShowAlertLog() => ShowingEventLog = IsDeviceFixed;

    /// <summary>The newest page again, and the device names with it.</summary>
    [RelayCommand]
    private Task RefreshAsync()
    {
        _devices = null;
        if (ShowingEventLog)
        {
            _eventLimit = PageSize;
        }
        else
        {
            _alertLimit = PageSize;
        }

        return LoadAsync();
    }

    [RelayCommand]
    private Task LoadMoreAsync()
    {
        if (!CanLoadMore)
        {
            return Task.CompletedTask;
        }

        if (ShowingEventLog)
        {
            _eventLimit = Math.Min(Math.Max(_eventLimit, _eventsFetched) + PageSize, MaxEntries);
        }
        else
        {
            _alertLimit = Math.Min(_alertLimit + PageSize, MaxEntries);
        }

        return LoadAsync();
    }

    [RelayCommand]
    private async Task ChooseTypeAsync()
    {
        var types = EventLogFeed.Types(_events).ToList();
        if (EventType is { } current && !types.Contains(current, StringComparer.OrdinalIgnoreCase))
        {
            types.Insert(0, current);
        }

        if (await _dialogs.ChooseAsync("Type", [AnyType, .. types]) is { } choice)
        {
            EventType = choice == AnyType ? null : choice;
        }
    }

    [RelayCommand]
    private async Task ChooseStateAsync()
    {
        var labels = States.Select(s => s.ToDisplayString()).ToList();
        if (await _dialogs.ChooseAsync("State", [AnyState, .. labels]) is { } choice)
        {
            StateFilter = labels.IndexOf(choice) is var index and >= 0 ? States[index] : null;
        }
    }

    [RelayCommand]
    private async Task ChooseRangeAsync()
    {
        var labels = Ranges.Select(r => r.Label).ToList();
        if (await _dialogs.ChooseAsync("Time range", labels) is { } choice && labels.IndexOf(choice) is var index and >= 0)
        {
            RangeIndex = index;
        }
    }

    /// <summary>The Device chip: the chooser Graylog's uses, which sets it through <see cref="ShowDevice"/>.</summary>
    [RelayCommand]
    private Task ChooseDeviceAsync() => IsDeviceFixed
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.GraylogDevice, new Dictionary<string, object> { [Routes.GraylogListParameter] = this });

    [RelayCommand]
    private void ClearDevice() => ShowDevice(null);

    [RelayCommand]
    private void ClearType() => EventType = null;

    [RelayCommand]
    private void ClearState() => StateFilter = null;

    [RelayCommand]
    private void ClearRange() => RangeIndex = 0;

    /// <summary>Back to how the page started.</summary>
    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        EventType = null;
        StateFilter = null;
        RangeIndex = 0;
        ShowDevice(null);
        ApplyFilter();
    }

    /// <summary>An entry's own page - or beside the list, on a larger screen (#88).</summary>
    [RelayCommand]
    private Task OpenAsync(LogEntryItem? item) => item is null
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.LogEntry, new Dictionary<string, object>
        {
            [Routes.LogEntryParameter] = item,
            [Routes.LogsListParameter] = this,
        });

    // ---------------------------------------------------------------- loading

    private void ForgetLoaded()
    {
        _events = [];
        _alerts = [];
        _eventsLoaded = _alertsLoaded = false;
        _eventsExhausted = _alertsExhausted = false;
        _eventLimit = _alertLimit = PageSize;
        _eventsFetched = 0;
        Entries.ReplaceAll([]);
    }

    /// <summary>
    /// A type or search needs a deeper page than is loaded (#125) - fetch it;
    /// otherwise narrow what's there.
    /// </summary>
    private void FilterOrFetch()
    {
        if (ShowingEventLog && _eventsLoaded && !_eventsExhausted && EventFetchLimit > _eventsFetched)
        {
            _ = LoadAsync();
            return;
        }

        ApplyFilter();
    }

    private int EventFetchLimit => Math.Min(EventLogFeed.FetchLimit(_eventLimit, EventType, SearchText), MaxEntries);

    private async Task LoadAsync()
    {
        // A newer load always wins over one still on its way.
        _loadCts?.Cancel();
        using var cts = new CancellationTokenSource();
        _loadCts = cts;
        var token = cts.Token;
        var eventLog = ShowingEventLog;

        await RunAsync(async () =>
        {
            if (_devices is null)
            {
                _devices = await _client.Devices.ListAsync(token);
                var style = _settings.Current.DeviceNameStyle;
                _names = _devices.ToDictionary(d => d.DeviceId, d => new DeviceItem(d, style).Name);
            }

            var deviceId = Device?.DeviceId;
            if (eventLog)
            {
                var limit = EventFetchLimit;
                var entries = await _client.Logs.ListEventLogAsync(deviceId, limit, token);
                token.ThrowIfCancellationRequested();
                _events = entries;
                _eventsFetched = limit;
                _eventsExhausted = entries.Count < limit;
                _eventsLoaded = true;
            }
            else
            {
                var entriesTask = _client.Logs.ListAlertLogAsync(deviceId, _alertLimit, token);
                var rulesTask = RuleNamesAsync(token);
                await Task.WhenAll(entriesTask, rulesTask);
                token.ThrowIfCancellationRequested();
                _alerts = entriesTask.Result;
                _rules = rulesTask.Result;
                _alertsExhausted = _alerts.Count < _alertLimit;
                _alertsLoaded = true;
            }
        });

        if (ReferenceEquals(_loadCts, cts))
        {
            _loadCts = null;
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var now = Now();
        var since = Ranges[Math.Clamp(RangeIndex, 0, Ranges.Count - 1)].Span is { } span ? now - span : (DateTime?)null;
        var utc = _settings.Current.ServerTimestampsAreUtc;
        var term = SearchText.Trim();

        IEnumerable<LogEntryItem> items;
        if (ShowingEventLog)
        {
            // Core's match - message, type, hostname, sysName - and the name
            // the app shows, which may be the display name instead.
            var typed = EventLogFeed.Filter(_events, EventType, null, int.MaxValue);
            var matched = term.Length == 0 ? null : EventLogFeed.Filter(typed, null, term, int.MaxValue).ToHashSet();
            items = typed
                .Select(e => LogEntryItem.ForEvent(e, DeviceName(e.DeviceId, e.Hostname), utc, now))
                .Where(i => matched is null || matched.Contains(i.Event!) || i.DeviceName.Contains(term, StringComparison.CurrentCultureIgnoreCase));
        }
        else
        {
            items = _alerts
                .Where(a => StateFilter is not { } state || a.State == state)
                .Select(a => LogEntryItem.ForAlert(a, DeviceName(a.DeviceId, a.Hostname), RuleName(a.RuleId), utc, now))
                .Where(i => term.Length == 0 || i.Matches(term));
        }

        Entries.ReplaceAll(items.Where(i => since is null || i.LocalTime >= since).ToList());

        OnPropertyChanged(nameof(CanLoadMore));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyText));
    }

    private async Task<IReadOnlyDictionary<int, string>> RuleNamesAsync(CancellationToken token)
    {
        try
        {
            return (await _client.Rules.ListAsync(token)).ToDictionary(r => r.Id, r => r.Name ?? $"Rule {r.Id}");
        }
        catch (LibreNmsApiException)
        {
            return new Dictionary<int, string>();
        }
    }

    private string RuleName(int ruleId) => _rules.TryGetValue(ruleId, out var rule) ? rule : $"Rule {ruleId}";

    private string DeviceName(int deviceId, string? hostname) =>
        _names.TryGetValue(deviceId, out var name) ? name
        : !string.IsNullOrWhiteSpace(hostname) ? hostname!
        : deviceId > 0 ? $"Device {deviceId}" : "LibreNMS";
}
