using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>At-a-glance counts: devices by state and open alerts by severity, plus the worst few alerts.</summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    /// <summary>How many alerts the dashboard lists before "see all".</summary>
    public const int TopAlertCount = 5;

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;

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

    public DashboardViewModel(ILibreNmsClient client, ISettingsStore settings, INavigationService navigation)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
    }

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

        LastUpdated = DateTime.Now;
        OnPropertyChanged(nameof(HasNoAlerts));
    });

    [RelayCommand]
    private Task OpenDeviceAsync(AlertItem? item) => item is null
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = item.Alert.DeviceId });
}
