using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// At-a-glance counts (devices by state, open alerts by severity), the worst
/// few alerts, and desktop's pinned and recently viewed device widgets.
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    /// <summary>How many alerts the dashboard lists before "see all".</summary>
    public const int TopAlertCount = 5;

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly DeviceBookmarks _bookmarks;

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

    public DashboardViewModel(ILibreNmsClient client, ISettingsStore settings, INavigationService navigation, DeviceBookmarks bookmarks)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
        _bookmarks = bookmarks;
    }

    /// <summary>Pinned devices with their live state, as desktop's Pinned devices widget.</summary>
    public ObservableCollection<DeviceItem> PinnedDevices { get; } = new();

    /// <summary>As desktop's Recently viewed widget, newest first.</summary>
    public ObservableCollection<RecentlyViewedDevice> RecentlyViewed { get; } = new();

    public bool HasPinnedDevices => PinnedDevices.Count > 0;

    public bool HasRecentlyViewed => RecentlyViewed.Count > 0;

    /// <summary>The most severe, then most recent, open unacknowledged alerts.</summary>
    public ObservableCollection<AlertItem> TopAlerts { get; } = new();

    public bool HasNoAlerts => TopAlerts.Count == 0 && LastUpdated is not null;

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
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
        TopAlerts.Clear();
        foreach (var alert in active
                     .OrderByDescending(a => a.Severity.SortRank())
                     .ThenByDescending(a => a.Timestamp)
                     .Take(TopAlertCount))
        {
            TopAlerts.Add(new AlertItem(alert, utc));
        }

        // Pinned in the order they were pinned, as desktop; a pin whose device
        // has since gone from LibreNMS just doesn't show.
        var style = _settings.Current.DeviceNameStyle;
        var byId = devices.ToDictionary(d => d.DeviceId);
        PinnedDevices.Clear();
        foreach (var pin in _bookmarks.PinningEnabled ? _settings.Current.PinnedDevices : [])
        {
            if (byId.TryGetValue(pin.DeviceId, out var device))
            {
                PinnedDevices.Add(new DeviceItem(device, style) { IsPinned = true });
            }
        }

        RecentlyViewed.Clear();
        foreach (var recent in _bookmarks.RecentlyViewed)
        {
            RecentlyViewed.Add(recent);
        }

        LastUpdated = DateTime.Now;
        OnPropertyChanged(nameof(HasNoAlerts));
        OnPropertyChanged(nameof(HasPinnedDevices));
        OnPropertyChanged(nameof(HasRecentlyViewed));
    });

    [RelayCommand]
    private Task OpenAlertAsync(AlertItem? item) => item is null ? Task.CompletedTask : _navigation.GoToAlertAsync(item.Alert);

    [RelayCommand]
    private Task OpenPinnedAsync(DeviceItem? item) => item is null ? Task.CompletedTask : OpenAsync(item.DeviceId);

    [RelayCommand]
    private Task OpenRecentAsync(RecentlyViewedDevice? recent) => recent is null ? Task.CompletedTask : OpenAsync(recent.DeviceId);

    private Task OpenAsync(int deviceId) =>
        _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = deviceId });
}
