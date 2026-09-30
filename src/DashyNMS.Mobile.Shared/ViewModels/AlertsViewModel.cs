using DashyNMS.Mobile.Alerts;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Infrastructure;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// Open alerts, most severe first: desktop's Alerts tab filters (critical,
/// warning, acknowledged, search), acknowledge/unacknowledge, and CSV export.
/// </summary>
/// <remarks>
/// Filters mean what they do on desktop, and are kept in the same
/// <see cref="AlertFilterSettings"/>: active alerts always show,
/// acknowledged ones only with <see cref="ShowAcknowledged"/>, and alerts with
/// no severity of their own always show, as desktop has no chip for them.
/// </remarks>
public sealed partial class AlertsViewModel : ViewModelBase
{
    /// <summary>Desktop's CSV columns, so an export opens the same either way.</summary>
    internal static readonly string[] CsvHeaders = ["Severity", "Device", "Alert", "State", "Age", "Note"];

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly ISelfActionTracker _selfActions;
    private readonly IShareService _share;
    private readonly IAppBadge _badge;
    private readonly AlertTabDot? _tabDot;
    private readonly TimeProvider _time;
    private IReadOnlyList<AlertItem> _all = Array.Empty<AlertItem>();

    private int? _selectedId;
    private bool _loading;

    /// <summary>
    /// Showing one of the dashboard's shortcuts (<see cref="ShowOnly"/>), not
    /// the user's own filter: it isn't saved, and <see cref="ShowSavedFilter"/>
    /// puts theirs back. Before #81 a shortcut was saved over it, so a chip
    /// turned off never seemed to stay off.
    /// </summary>
    private bool _shortcut;

    [ObservableProperty]
    private bool _showCritical;

    [ObservableProperty]
    private bool _showWarning;

    /// <summary>
    /// Alerts with LibreNMS's ok severity (and any unrecognised one) - the
    /// OK chip (#64). Kept in desktop's ShowUnknownSeverity, its filter for
    /// everything that isn't critical or warning, which has no chip there.
    /// </summary>
    [ObservableProperty]
    private bool _showOk;

    [ObservableProperty]
    private bool _showAcknowledged;

    [ObservableProperty]
    private string _searchText;

    public AlertsViewModel(
        ILibreNmsClient client,
        ISettingsStore settings,
        IDialogService dialogs,
        INavigationService navigation,
        ISelfActionTracker selfActions,
        IShareService share,
        IAppBadge badge,
        TimeProvider time,
        AlertTabDot? tabDot = null)
    {
        _client = client;
        _settings = settings;
        _dialogs = dialogs;
        _navigation = navigation;
        _selfActions = selfActions;
        _share = share;
        _badge = badge;
        _time = time;
        _tabDot = tabDot;

        // Carry on where the last session left off, as desktop does.
        var filter = settings.Current.Filter;
        _showCritical = filter.ShowCritical;
        _showWarning = filter.ShowWarning;
        _showOk = filter.ShowUnknownSeverity;
        _showAcknowledged = filter.ShowAcknowledged;
        _searchText = filter.SearchText ?? string.Empty;
    }

    public BulkObservableCollection<AlertItem> Alerts { get; } = new();

    public bool IsEmpty => Alerts.Count == 0 && !IsBusy;

    /// <summary>What the empty list says: nothing open at all, or nothing matching.</summary>
    public string EmptyText => _all.Count == 0 ? "No open alerts." : "No alerts match these filters.";

    /// <summary>Unacknowledged critical alerts, for the Critical chip - whatever the filters.</summary>
    public int CriticalCount => _all.Count(a => a.Severity == AlertSeverity.Critical && !a.IsAcknowledged);

    public int WarningCount => _all.Count(a => a.Severity == AlertSeverity.Warning && !a.IsAcknowledged);

    /// <summary>Unacknowledged alerts that are neither critical nor warning - LibreNMS's ok severity.</summary>
    public int OkCount => _all.Count(a => a.Severity is not (AlertSeverity.Critical or AlertSeverity.Warning) && !a.IsAcknowledged);

    public int AcknowledgedCount => _all.Count(a => a.IsAcknowledged);

    /// <summary>"12 alerts", or "3 of 12 alerts" when filtered.</summary>
    public string CountText => Alerts.Count == _all.Count
        ? $"{_all.Count} {Plural(_all.Count)}"
        : $"{Alerts.Count} of {_all.Count} {Plural(_all.Count)}";

    /// <summary>True when anything narrows the list - shows the Clear button.</summary>
    public bool HasActiveFilters => !ShowCritical || !ShowWarning || !ShowOk || !ShowAcknowledged || !string.IsNullOrWhiteSpace(SearchText);

    partial void OnShowCriticalChanged(bool value) => OnFilterChanged();

    partial void OnShowWarningChanged(bool value) => OnFilterChanged();

    partial void OnShowOkChanged(bool value) => OnFilterChanged();

    partial void OnShowAcknowledgedChanged(bool value) => OnFilterChanged();

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(OnFilterChanged);

    // A chip tapped on a shortcut makes what's showing the user's own filter.
    [RelayCommand]
    private void ToggleCritical()
    {
        _shortcut = false;
        ShowCritical = !ShowCritical;
    }

    [RelayCommand]
    private void ToggleWarning()
    {
        _shortcut = false;
        ShowWarning = !ShowWarning;
    }

    [RelayCommand]
    private void ToggleOk()
    {
        _shortcut = false;
        ShowOk = !ShowOk;
    }

    [RelayCommand]
    private void ToggleAcknowledged()
    {
        _shortcut = false;
        ShowAcknowledged = !ShowAcknowledged;
    }

    /// <summary>
    /// Back to the user's own filter after a shortcut - the page calls this
    /// when the tab is opened without one, so the dashboard's "Critical"
    /// doesn't stay on for good. Nothing to do otherwise.
    /// </summary>
    public void ShowSavedFilter()
    {
        if (!_shortcut)
        {
            return;
        }

        _shortcut = false;
        var filter = _settings.Current.Filter;
        _loading = true;
        ShowCritical = filter.ShowCritical;
        ShowWarning = filter.ShowWarning;
        ShowOk = filter.ShowUnknownSeverity;
        ShowAcknowledged = filter.ShowAcknowledged;
        SearchText = filter.SearchText ?? string.Empty;
        _loading = false;
        ApplyFilter();
    }

    /// <summary>
    /// Only one kind of alert, for the dashboard's counts (#32): "critical"
    /// or "warning" turns every other chip off; "acknowledged" has no chip of
    /// its own that shows only those, so it searches for the state, which
    /// the search box shows and ✕ clears. "all" - the dashboard's See all -
    /// clears every filter.
    /// </summary>
    public void ShowOnly(string kind)
    {
        // Shown, not saved: the user's own filter comes back afterwards (#81).
        _shortcut = true;
        if (string.Equals(kind, "all", StringComparison.OrdinalIgnoreCase))
        {
            ResetFilters();
            return;
        }

        _loading = true;
        var acknowledged = string.Equals(kind, "acknowledged", StringComparison.OrdinalIgnoreCase);
        ShowCritical = acknowledged || string.Equals(kind, "critical", StringComparison.OrdinalIgnoreCase);
        ShowWarning = acknowledged || string.Equals(kind, "warning", StringComparison.OrdinalIgnoreCase);
        ShowOk = acknowledged || string.Equals(kind, "ok", StringComparison.OrdinalIgnoreCase);
        ShowAcknowledged = acknowledged;
        SearchText = acknowledged ? AlertState.Acknowledged.ToDisplayString() : string.Empty;
        _loading = false;
        OnFilterChanged();
    }

    /// <summary>Back to everything, as desktop's clear (✕) button - the user's choice, so it's saved.</summary>
    [RelayCommand]
    private void ClearFilters()
    {
        _shortcut = false;
        ResetFilters();
    }

    private void ResetFilters()
    {
        _loading = true;
        ShowCritical = true;
        ShowWarning = true;
        ShowOk = true;
        ShowAcknowledged = true;
        SearchText = string.Empty;
        _loading = false;
        OnFilterChanged();
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var alertsTask = _client.Alerts.ListAsync(AlertQuery.Open);
        var devicesTask = DevicesByIdAsync();
        await Task.WhenAll(alertsTask, devicesTask);

        var alerts = alertsTask.Result;
        _all = alerts
            .OrderBy(a => a.IsAcknowledged)
            .ThenByDescending(a => a.Severity.SortRank())
            .ThenByDescending(a => a.Timestamp)
            .Select(a => AlertItem.For(a, _settings.Current, devicesTask.Result))
            .ToList();
        MarkSelected(_all);
        ApplyFilter();

        // Straight away, rather than at the next check - after acknowledging, say.
        _badge.SetCount(AlertBadge.Count(alerts, _settings.Current));
        _tabDot?.Update(alerts, _settings.Current);
    });

    /// <summary>The list as it's filtered now, as a CSV file, through the share sheet.</summary>
    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (Alerts.Count == 0)
        {
            await _dialogs.AlertAsync("Nothing to export", "No alerts match the current filters.");
            return;
        }

        var fileName = $"alerts-{_time.GetLocalNow():yyyy-MM-dd-HHmmss}.csv";
        await RunAsync(() => _share.ShareTextFileAsync(fileName, BuildCsv(), "text/csv", "Export alerts"));
    }

    /// <summary>Acknowledges until the alert clears, with an optional note - as desktop's default.</summary>
    [RelayCommand]
    private async Task AcknowledgeAsync(AlertItem? item)
    {
        if (item is null || item.IsAcknowledged)
        {
            return;
        }

        var note = await _dialogs.PromptAsync(
            "Acknowledge alert",
            $"{item.Rule} on {item.Device}. Add a note (optional):",
            "Acknowledge",
            "Note");
        if (note is null)
        {
            return;
        }

        if (await RunAsync(() => _client.Alerts.AcknowledgeAsync(item.Id, note.Trim())))
        {
            // So the next alert check doesn't notify you about your own acknowledgement.
            _selfActions.Record(item.Id, AlertChangeKind.Acknowledged);
            UpdateInPlace(item, AcknowledgedState, string.IsNullOrWhiteSpace(note) ? null : note.Trim());
        }
    }

    [RelayCommand]
    private async Task UnacknowledgeAsync(AlertItem? item)
    {
        if (item is null || !item.IsAcknowledged)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "Unacknowledge alert",
            $"Put {item.Rule} on {item.Device} back to active?",
            "Unacknowledge",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        if (await RunAsync(() => _client.Alerts.UnmuteAsync(item.Id)))
        {
            _selfActions.Record(item.Id, AlertChangeKind.Unacknowledged);
            UpdateInPlace(item, ActiveState, note: null);
        }
    }

    /// <summary>
    /// Highlights the alert whose detail is showing beside the list (#88), or
    /// none. Kept across refreshes, which make new rows.
    /// </summary>
    public void Select(int? alertId)
    {
        _selectedId = alertId;
        MarkSelected(_all);
    }

    private void MarkSelected(IEnumerable<AlertItem> items)
    {
        foreach (var item in items)
        {
            item.IsSelected = item.Id == _selectedId;
        }
    }

    /// <summary>
    /// A change made in Alert detail beside the list (#88), onto its row as
    /// if it were made here.
    /// </summary>
    public void ShowChange(AlertStateChange change)
    {
        if (_all.FirstOrDefault(a => a.Id == change.AlertId) is { } item)
        {
            UpdateInPlace(item, change.Acknowledged ? AcknowledgedState : ActiveState, change.Note);
        }
    }

    /// <summary>LibreNMS's alert state numbers (see AlertStateExtensions.FromValue).</summary>
    private const int ActiveState = 1;

    private const int AcknowledgedState = 2;

    /// <summary>
    /// The alert's new state straight onto its own row - replaced, or taken
    /// out if the filters no longer show it - rather than reloading the whole
    /// list, which on iOS reset it to the top: with 100 alerts, acknowledging
    /// one lost your place (#93). The counts, the icon badge and the tab dot
    /// follow from the same change; the next refresh or check picks up
    /// anything else.
    /// </summary>
    private void UpdateInPlace(AlertItem item, int state, string? note)
    {
        var alert = item.Alert;
        alert.StateValue = state;
        if (note is not null)
        {
            alert.Note = note;
        }

        var changed = new AlertItem(alert, _settings.Current.ServerTimestampsAreUtc, item.Device);
        MarkSelected([changed]);
        _all = _all.Select(a => a.Id == item.Id ? changed : a).ToList();

        var index = Alerts.IndexOf(item);
        if (index >= 0)
        {
            if (Allows(changed))
            {
                Alerts[index] = changed;
            }
            else
            {
                Alerts.RemoveAt(index);
            }
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(CriticalCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(OkCount));
        OnPropertyChanged(nameof(AcknowledgedCount));

        var alerts = _all.Select(a => a.Alert).ToList();
        _badge.SetCount(AlertBadge.Count(alerts, _settings.Current));
        _tabDot?.Update(alerts, _settings.Current);
    }

    /// <summary>The network-wide alert and event logs.</summary>
    [RelayCommand]
    private Task OpenLogsAsync() => _navigation.GoToAsync(Routes.Logs);

    [RelayCommand]
    private Task OpenAlertAsync(AlertItem? item) => item is null ? Task.CompletedTask : _navigation.GoToAlertAsync(item.Alert);

    internal string BuildCsv() => CsvWriter.ToCsv(
        CsvHeaders,
        Alerts.Select(a => (IReadOnlyList<string>)[a.SeverityText, a.Device, a.Rule, a.StateText, a.AgeText, a.Note ?? string.Empty]));

    private bool Allows(AlertItem alert)
    {
        var severityAllowed = alert.Severity switch
        {
            AlertSeverity.Critical => ShowCritical,
            AlertSeverity.Warning => ShowWarning,
            _ => ShowOk,
        };

        var stateAllowed = alert.State switch
        {
            AlertState.Acknowledged => ShowAcknowledged,
            AlertState.Recovered => false,
            _ => true,
        };

        var term = SearchText.Trim();
        return severityAllowed && stateAllowed && (term.Length == 0 || alert.Matches(term));
    }

    private void OnFilterChanged()
    {
        if (_loading)
        {
            return;
        }

        ApplyFilter();
        SaveFilter();
    }

    /// <summary>For naming each alert's device - best effort, since the alerts still make sense by hostname.</summary>
    private async Task<IReadOnlyDictionary<int, Device>?> DevicesByIdAsync()
    {
        try
        {
            return (await _client.Devices.ListAsync()).ToDictionary(d => d.DeviceId);
        }
        catch (LibreNmsApiException)
        {
            return null;
        }
    }

    private void ApplyFilter()
    {
        Alerts.ReplaceAll(_all.Where(Allows));

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(CriticalCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(OkCount));
        OnPropertyChanged(nameof(AcknowledgedCount));
    }

    private void SaveFilter()
    {
        if (_shortcut)
        {
            return;
        }

        var filter = _settings.Current.Filter;
        filter.ShowCritical = ShowCritical;
        filter.ShowWarning = ShowWarning;
        filter.ShowUnknownSeverity = ShowOk;
        filter.ShowAcknowledged = ShowAcknowledged;
        filter.SearchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
        _settings.Save();
    }

    private static string Plural(int count) =>
        count == 1 ? "alert" : "alerts";
}
