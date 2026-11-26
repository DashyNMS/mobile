using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Demo;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Demo;

/// <summary>
/// The demo (#160): the whole app against Core's example network, answered in
/// the same process with no server and no listener, for App Store review and
/// screenshots.
/// </summary>
/// <remarks>
/// <para>
/// It's offered only on the sign-in page, so nobody is signed in when it
/// starts. While it runs it has its own settings file, its preferences are kept
/// in memory over the phone's, nothing is saved for offline use, no alert is
/// checked or notified, no widget is updated, Intelligence keeps no chat, and
/// diagnostics share nothing. The saved token is never read or written.
/// </para>
/// <para>
/// Leaving puts the real settings and preferences back, deletes the demo's
/// folder and returns to sign-in exactly as it was. A demo that was running
/// when the app was closed is cleared at the next launch.
/// </para>
/// </remarks>
public sealed class DemoSession
{
    private readonly DemoState _state;
    private readonly SessionService _session;
    private readonly SwitchableSettingsStore _settings;
    private readonly SwitchablePreferences? _preferences;
    private readonly INavigationService _navigation;
    private readonly ILoggerFactory? _loggers;
    private readonly ILogger<DemoSession>? _logger;
    private readonly string _folder;
    private DemoResponder? _responder;

    public DemoSession(
        DemoState state,
        SessionService session,
        SwitchableSettingsStore settings,
        INavigationService navigation,
        SwitchablePreferences? preferences = null,
        ILoggerFactory? loggers = null,
        string? folder = null)
    {
        _state = state;
        _session = session;
        _settings = settings;
        _navigation = navigation;
        _preferences = preferences;
        _loggers = loggers;
        _logger = loggers?.CreateLogger<DemoSession>();
        _folder = folder ?? Path.Combine(AppPaths.DataDirectory, "demo");
    }

    public bool IsActive => _state.IsActive;

    /// <summary>The example network while the demo runs, for what shows it (#234's sample chat).</summary>
    public DemoResponder? Responder => _responder;

    /// <summary>Starts the demo and opens the dashboard. False when someone's signed in, or it couldn't start.</summary>
    public async Task<bool> EnterAsync(CancellationToken cancellationToken = default)
    {
        if (_state.IsActive || _session.IsConnected)
        {
            return false;
        }

        DeleteFolder();
        Directory.CreateDirectory(_folder);
        var responder = new DemoResponder();
        var store = new SettingsStore(_loggers?.CreateLogger<SettingsStore>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsStore>.Instance,
            Path.Combine(_folder, "settings.json"));
        Seed(store, responder);

        _responder = responder;
        _state.Set(true);
        _settings.UseDemo(store);
        try
        {
            var result = await _session.ConnectDemoAsync(responder.Connection(store.Current.TimeoutSeconds), cancellationToken);
            if (!result.Succeeded)
            {
                _logger?.LogWarning("The demo didn't start: {Message}", result.ErrorMessage);
                Restore();
                return false;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger?.LogError(ex, "The demo didn't start");
            Restore();
            return false;
        }

        await _navigation.GoToAsync(Routes.Main);
        return true;
    }

    /// <summary>Ends the demo and goes back to sign-in, with nothing of it left behind.</summary>
    public async Task LeaveAsync()
    {
        if (!_state.IsActive)
        {
            return;
        }

        _session.LeaveDemo();
        Restore();
        await _navigation.GoToAsync(Routes.SignIn);
    }

    /// <summary>At launch: a demo the app was closed in leaves nothing behind.</summary>
    public void ClearLeftovers()
    {
        if (!_state.IsActive)
        {
            DeleteFolder();
        }
    }

    private void Restore()
    {
        _settings.UseReal();
        _preferences?.ClearDemo();
        _responder = null;
        _state.Set(false);
        DeleteFolder();
    }

    private void DeleteFolder()
    {
        try
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Tried again at the next start or launch.
            _logger?.LogWarning(ex, "Couldn't delete the demo's folder");
        }
    }

    /// <summary>A demo worth looking at: Desktop's pins, recently viewed and cards, without notifications or saved chats.</summary>
    internal static void Seed(ISettingsStore store, DemoResponder responder)
    {
        var settings = store.Current;
        settings.ServerUrl = DemoResponder.Address.ToString();
        settings.RememberToken = false;
        settings.Theme = AppTheme.System;
        settings.WelcomeDismissed = true;
        settings.Notifications.Enabled = false;
        settings.Notifications.SuppressOnFirstPoll = true;
        settings.ShowAlertTabBadge = false;
        settings.IntelligenceSingleChatMode = true;
        settings.PinnedDevices = responder.PinnedDevices
            .Select((d, i) => new PinnedDevice { DeviceId = d.Id, DisplayName = d.Name, PinnedAt = DateTimeOffset.Now.AddDays(-10 + i) })
            .ToList();
        settings.RecentlyViewedDevices = responder.RecentlyViewed
            .Select((d, i) => new RecentlyViewedDevice { DeviceId = d.Id, DisplayName = d.Name, ViewedAt = DateTimeOffset.Now.AddMinutes(-3 - i * 7) })
            .ToList();

        var sensors = new DashboardWidget { WidgetType = DashboardWidgetTypes.Sensors, Title = "Pinned sensors" };
        sensors.Sensors.AddRange(responder.PinnedSensors.Select(s => new PinnedSensor
        {
            SensorId = s.SensorId,
            DeviceId = s.DeviceId,
            SensorClass = s.SensorClass,
            DeviceName = s.DeviceName,
            Description = s.Description,
        }));

        settings.DashboardWidgets =
        [
            new() { WidgetType = DashboardWidgetTypes.AlertsGauge, Title = "Alerts" },
            new() { WidgetType = DashboardWidgetTypes.DeviceStatus, Title = "Devices" },
            new() { WidgetType = DashboardWidgetTypes.Alerts, Title = "Live alerts" },
            sensors,
            new() { WidgetType = DashboardWidgetTypes.Graph, Title = "core-sw-01 traffic", GraphDeviceId = responder.FeaturedDeviceId, GraphName = "device_bits" },
            new() { WidgetType = DashboardWidgetTypes.PinnedDevices, Title = "Pinned devices" },
            new() { WidgetType = DashboardWidgetTypes.TopInterfaces, Title = "Top interfaces", TopCount = 4 },
            new() { WidgetType = DashboardWidgetTypes.EventLog, Title = "Event log", LogCount = 8 },
        ];

        store.Save();
    }
}
