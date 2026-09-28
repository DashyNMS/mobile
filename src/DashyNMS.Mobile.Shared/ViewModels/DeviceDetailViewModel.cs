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
    private readonly DeviceBookmarks _bookmarks;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(StateText), nameof(State), nameof(UptimeText), nameof(Properties), nameof(PinText))]
    [NotifyCanExecuteChangedFor(nameof(OpenInBrowserCommand), nameof(TogglePinCommand))]
    private Device? _device;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinText))]
    private bool _isPinned;

    public DeviceDetailViewModel(ILibreNmsClient client, ISettingsStore settings, ILauncherService launcher, DeviceBookmarks bookmarks)
    {
        _client = client;
        _settings = settings;
        _launcher = launcher;
        _bookmarks = bookmarks;
    }

    public int DeviceId { get; private set; }

    /// <summary>Following desktop's hostname / sysName / display name preference.</summary>
    public string Title => Device is null ? "Device" : _settings.Current.DeviceNameStyle.Resolve(Device, Device.Hostname);

    public string PinText => IsPinned ? "Unpin" : "Pin";

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
        else
        {
            // Onto the Devices tab's recently viewed strip, as desktop's Device View does.
            _bookmarks.RecordViewed(DeviceId, Title);
            IsPinned = _bookmarks.IsPinned(DeviceId);
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

    private bool CanTogglePin() => Device is not null && _bookmarks.PinningEnabled;

    /// <summary>Pinned devices stay at the top of the Devices tab.</summary>
    [RelayCommand(CanExecute = nameof(CanTogglePin))]
    private void TogglePin()
    {
        _bookmarks.SetPinned(DeviceId, Title, !IsPinned);
        IsPinned = _bookmarks.IsPinned(DeviceId);
    }

    private bool CanOpenInBrowser() => Device is not null && _client.Connection is not null;

    [RelayCommand(CanExecute = nameof(CanOpenInBrowser))]
    private Task OpenInBrowserAsync() =>
        _client.Connection is { } connection ? _launcher.OpenAsync(connection.DeviceUrl(DeviceId)) : Task.CompletedTask;
}
