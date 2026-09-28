using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>One device: its state, identity and open alerts.</summary>
public sealed partial class DeviceDetailViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly ILauncherService _launcher;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(StateText), nameof(State), nameof(UptimeText), nameof(Properties))]
    [NotifyCanExecuteChangedFor(nameof(OpenInBrowserCommand))]
    private Device? _device;

    public DeviceDetailViewModel(ILibreNmsClient client, ISettingsStore settings, ILauncherService launcher)
    {
        _client = client;
        _settings = settings;
        _launcher = launcher;
    }

    public int DeviceId { get; private set; }

    public string Title => Device?.BestName ?? "Device";

    public DeviceState? State => Device?.State;

    public string StateText => Device?.State.ToDisplayString() ?? string.Empty;

    public string UptimeText => Device is null ? string.Empty : Formatting.Uptime(Device.Uptime);

    /// <summary>Label/value pairs for whatever LibreNMS knows about the device.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Properties => Device is null
        ? Array.Empty<KeyValuePair<string, string>>()
        : new (string Label, string? Value)[]
            {
                ("Hostname", Device.Hostname),
                ("sysName", Device.SysName),
                ("IP address", Device.Ip),
                ("OS", Device.Os),
                ("Hardware", Device.Hardware),
                ("Version", Device.Version),
                ("Serial", Device.Serial),
                ("Location", Device.Location),
                ("Type", Device.Type),
                ("Purpose", Device.Purpose),
                ("Contact", Device.Contact),
                ("Uptime", UptimeText),
            }
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => new KeyValuePair<string, string>(p.Label, p.Value!))
            .ToList();

    public ObservableCollection<AlertItem> Alerts { get; } = new();

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
        await Task.WhenAll(deviceTask, alertsTask);

        Device = deviceTask.Result;
        if (Device is null)
        {
            ErrorMessage = "LibreNMS no longer has this device.";
        }

        var utc = _settings.Current.ServerTimestampsAreUtc;
        Alerts.Clear();
        foreach (var alert in alertsTask.Result
                     .Where(a => a.DeviceId == DeviceId)
                     .OrderByDescending(a => a.Severity.SortRank())
                     .ThenByDescending(a => a.Timestamp))
        {
            Alerts.Add(new AlertItem(alert, utc));
        }

        OnPropertyChanged(nameof(HasAlerts));
    });

    private bool CanOpenInBrowser() => Device is not null && _client.Connection is not null;

    [RelayCommand(CanExecute = nameof(CanOpenInBrowser))]
    private Task OpenInBrowserAsync() =>
        _client.Connection is { } connection ? _launcher.OpenAsync(connection.DeviceUrl(DeviceId)) : Task.CompletedTask;
}
