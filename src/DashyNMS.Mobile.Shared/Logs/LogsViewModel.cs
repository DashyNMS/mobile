using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Logs;

/// <summary>
/// LibreNMS's event log and alert log across the whole network - what
/// Device View's Event log shows for one device, for every device at once.
/// </summary>
/// <remarks>
/// Loads a page at a time, newest first, and "Load more" asks for a bigger
/// page (the endpoint has no offset). The alert log names rules from the rule
/// list, best effort - a token that can't read rules gets "Rule 12".
/// Search covers what's loaded.
/// </remarks>
public sealed partial class LogsViewModel : ViewModelBase
{
    internal const int PageSize = 100;

    /// <summary>Far enough back for a phone; past this, desktop or the web UI.</summary>
    internal const int MaxEntries = 1000;

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private IReadOnlyList<SectionRow> _events = [];
    private IReadOnlyList<SectionRow> _alerts = [];
    private int _eventLimit = PageSize;
    private int _alertLimit = PageSize;
    private bool _eventsLoaded;
    private bool _alertsLoaded;
    private bool _eventsExhausted;
    private bool _alertsExhausted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowingAlertLog))]
    private bool _showingEventLog = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public LogsViewModel(
        ILibreNmsClient client,
        ISettingsStore settings,
        INavigationService navigation)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
    }

    public bool ShowingAlertLog => !ShowingEventLog;

    public BulkObservableCollection<SectionRow> Entries { get; } = new();

    public bool CanLoadMore => ShowingEventLog
        ? _eventsLoaded && !_eventsExhausted && _eventLimit < MaxEntries
        : _alertsLoaded && !_alertsExhausted && _alertLimit < MaxEntries;

    public bool IsEmpty => (ShowingEventLog ? _eventsLoaded : _alertsLoaded) && Entries.Count == 0 && !IsBusy;

    public string EmptyText => !string.IsNullOrWhiteSpace(SearchText) ? "Nothing loaded matches." : "Nothing logged.";

    partial void OnShowingEventLogChanged(bool value) => _ = EnsureLoadedAsync();

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(ApplyFilter);

    [RelayCommand]
    private void ShowEventLog() => ShowingEventLog = true;

    [RelayCommand]
    private void ShowAlertLog() => ShowingEventLog = false;

    /// <summary>The log showing, loaded if it hasn't been yet.</summary>
    public Task EnsureLoadedAsync()
    {
        ApplyFilter();
        return (ShowingEventLog ? _eventsLoaded : _alertsLoaded) ? Task.CompletedTask : RefreshAsync();
    }

    /// <summary>The newest page again.</summary>
    [RelayCommand]
    private Task RefreshAsync()
    {
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
            _eventLimit = Math.Min(_eventLimit + PageSize, MaxEntries);
        }
        else
        {
            _alertLimit = Math.Min(_alertLimit + PageSize, MaxEntries);
        }

        return LoadAsync();
    }

    /// <summary>An entry opens its device.</summary>
    [RelayCommand]
    private Task OpenAsync(SectionRow? row) => row?.LinkDeviceId is { } id and > 0
        ? _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = id })
        : Task.CompletedTask;

    private Task LoadAsync() => RunAsync(async () =>
    {
        var utc = _settings.Current.ServerTimestampsAreUtc;
        var names = await DeviceNamesAsync();

        if (ShowingEventLog)
        {
            var entries = await _client.Logs.ListEventLogAsync(null, _eventLimit);
            _events = entries.Select(e => EventRow(e, names, utc)).ToList();
            _eventsExhausted = entries.Count < _eventLimit;
            _eventsLoaded = true;
        }
        else
        {
            var entriesTask = _client.Logs.ListAlertLogAsync(null, _alertLimit);
            var rulesTask = RuleNamesAsync();
            await Task.WhenAll(entriesTask, rulesTask);
            _alerts = entriesTask.Result.Select(e => AlertRow(e, names, rulesTask.Result, utc)).ToList();
            _alertsExhausted = entriesTask.Result.Count < _alertLimit;
            _alertsLoaded = true;
        }

        ApplyFilter();
    });

    internal static SectionRow EventRow(EventLogEntry entry, IReadOnlyDictionary<int, string> names, bool utc) =>
        new(entry.Message ?? "(no message)")
        {
            Subtitle = string.Join(" · ", new[] { DeviceName(entry.DeviceId, null, names), entry.Type, entry.Username }
                .Where(s => !string.IsNullOrWhiteSpace(s))),
            Detail = When(entry.Timestamp, utc),
            Status = DeviceSectionLoader.EventStatus(entry.Severity),
            LinkDeviceId = entry.DeviceId,
        };

    internal static SectionRow AlertRow(AlertLogEntry entry, IReadOnlyDictionary<int, string> names, IReadOnlyDictionary<int, string> rules, bool utc) =>
        new(rules.TryGetValue(entry.RuleId, out var rule) ? rule : $"Rule {entry.RuleId}")
        {
            Subtitle = DeviceName(entry.DeviceId, entry.Hostname, names),
            Value = entry.State.ToDisplayString(),
            Detail = When(entry.TimeLogged, utc),
            Status = entry.State switch
            {
                AlertState.Recovered or AlertState.Better => RowStatus.Ok,
                AlertState.Acknowledged => RowStatus.Inactive,
                _ => RowStatus.Critical,
            },
            LinkDeviceId = entry.DeviceId,
        };

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        Entries.ReplaceAll((ShowingEventLog ? _events : _alerts)
            .Where(r => term.Length == 0 || r.Matches(term))
            .ToList());

        OnPropertyChanged(nameof(CanLoadMore));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    private async Task<IReadOnlyDictionary<int, string>> DeviceNamesAsync()
    {
        var style = _settings.Current.DeviceNameStyle;
        return (await _client.Devices.ListAsync()).ToDictionary(d => d.DeviceId, d => new DeviceItem(d, style).Name);
    }

    private async Task<IReadOnlyDictionary<int, string>> RuleNamesAsync()
    {
        try
        {
            return (await _client.Rules.ListAsync()).ToDictionary(r => r.Id, r => r.Name ?? $"Rule {r.Id}");
        }
        catch (LibreNmsApiException)
        {
            return new Dictionary<int, string>();
        }
    }

    private static string DeviceName(int deviceId, string? hostname, IReadOnlyDictionary<int, string> names) =>
        names.TryGetValue(deviceId, out var name) ? name
        : !string.IsNullOrWhiteSpace(hostname) ? hostname!
        : deviceId > 0 ? $"Device {deviceId}" : "LibreNMS";

    private static string? When(DateTime? at, bool utc) =>
        ServerTime.ToLocal(at, utc)?.ToString("d MMM HH:mm:ss", CultureInfo.CurrentCulture);
}
