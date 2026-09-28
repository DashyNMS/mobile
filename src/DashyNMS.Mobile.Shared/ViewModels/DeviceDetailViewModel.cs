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
/// actions - pin, rediscover, schedule maintenance, open in the browser.
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
    [NotifyPropertyChangedFor(nameof(Title), nameof(StateText), nameof(State), nameof(UptimeText), nameof(Properties), nameof(PinText), nameof(LastDiscoveredText))]
    [NotifyCanExecuteChangedFor(nameof(OpenInBrowserCommand), nameof(TogglePinCommand), nameof(RediscoverCommand), nameof(ScheduleMaintenanceCommand), nameof(OpenSshCommand), nameof(OpenTelnetCommand))]
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
        Graylog.GraylogSetup? graylog = null)
    {
        _client = client;
        _settings = settings;
        _launcher = launcher;
        _bookmarks = bookmarks;
        _dialogs = dialogs;
        _navigation = navigation;
        _graylog = graylog;
    }

    /// <summary>Desktop's Device View tabs, each opening on its own page - Graylog's too, once it's set up.</summary>
    public IReadOnlyList<DeviceSectionInfo> Sections => _graylog?.IsConfigured == true
        ? [.. DeviceSectionInfo.All, DeviceSectionInfo.Graylog]
        : DeviceSectionInfo.All;

    /// <summary>"Last discovered 3h ago", when LibreNMS says.</summary>
    public string? LastDiscoveredText => Device?.LastDiscovered is { } at
        ? "Last discovered " + Formatting.Age(DesktopNMS.Core.ServerTime.Age(at, _settings.Current.ServerTimestampsAreUtc))
        : null;

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
        OnPropertyChanged(nameof(Sections));

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
        Alerts.ReplaceAll(alertsTask.Result
            .Where(a => a.DeviceId == DeviceId)
            .OrderByDescending(a => a.Severity.SortRank())
            .ThenByDescending(a => a.Timestamp)
            .Select(alert => new AlertItem(alert, utc, Device is null ? null : Title)));

        OnPropertyChanged(nameof(HasAlerts));
    });

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

    private bool CanOpenInBrowser() => Device is not null && _client.Connection is not null;

    [RelayCommand(CanExecute = nameof(CanOpenInBrowser))]
    private Task OpenInBrowserAsync() =>
        _client.Connection is { } connection ? _launcher.OpenAsync(connection.DeviceUrl(DeviceId)) : Task.CompletedTask;
}
