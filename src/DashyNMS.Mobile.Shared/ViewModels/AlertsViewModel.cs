using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>Open alerts, most severe first, with acknowledge and unacknowledge.</summary>
public sealed partial class AlertsViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly ISelfActionTracker _selfActions;
    private IReadOnlyList<AlertItem> _all = Array.Empty<AlertItem>();

    /// <summary>Hide alerts someone has already acknowledged.</summary>
    [ObservableProperty]
    private bool _hideAcknowledged;

    public AlertsViewModel(
        ILibreNmsClient client,
        ISettingsStore settings,
        IDialogService dialogs,
        INavigationService navigation,
        ISelfActionTracker selfActions)
    {
        _selfActions = selfActions;
        _client = client;
        _settings = settings;
        _dialogs = dialogs;
        _navigation = navigation;
    }

    public ObservableCollection<AlertItem> Alerts { get; } = new();

    public bool IsEmpty => Alerts.Count == 0 && !IsBusy;

    partial void OnHideAcknowledgedChanged(bool value) => ApplyFilter();

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var alerts = await _client.Alerts.ListAsync(AlertQuery.Open);
        var utc = _settings.Current.ServerTimestampsAreUtc;
        _all = alerts
            .OrderBy(a => a.IsAcknowledged)
            .ThenByDescending(a => a.Severity.SortRank())
            .ThenByDescending(a => a.Timestamp)
            .Select(a => new AlertItem(a, utc))
            .ToList();
        ApplyFilter();
    });

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
            await RefreshAsync();
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
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private Task OpenDeviceAsync(AlertItem? item) => item is null
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = item.Alert.DeviceId });

    private void ApplyFilter()
    {
        Alerts.Clear();
        foreach (var item in _all.Where(a => !HideAcknowledged || !a.IsAcknowledged))
        {
            Alerts.Add(item);
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
}
