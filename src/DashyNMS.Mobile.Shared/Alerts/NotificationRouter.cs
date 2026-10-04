using DashyNMS.Mobile.Services;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Alerts;

/// <summary>
/// Where a tapped notification leads: the alert, else the device, else (a
/// summary of several) the alert list.
/// </summary>
public sealed record NotificationTarget(int? DeviceId, int? AlertId = null);

/// <summary>
/// Opens the screen a tapped notification points at. A tap can also be what
/// launches the app, before anyone is signed in; then the target waits until
/// sign-in reaches the main tabs (<see cref="MainShownAsync"/>).
/// </summary>
public sealed class NotificationRouter
{
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private NotificationTarget? _pending;
    private bool _mainShown;

    public NotificationRouter(ISessionService session, INavigationService navigation, ILogger<NotificationRouter>? logger = null)
    {
        _session = session;
        _navigation = navigation;
        _logger = logger ?? NullLogger<NotificationRouter>.Instance;
        _session.StateChanged += (_, _) =>
        {
            if (!_session.IsConnected)
            {
                lock (_gate)
                {
                    _mainShown = false;
                }
            }
        };
    }

    public Task OpenAsync(NotificationTarget target)
    {
        lock (_gate)
        {
            if (!_mainShown || !_session.IsConnected)
            {
                _pending = target;
                _logger.LogInformation("Notification tapped: {Target}, once signed in", Describe(target));
                return Task.CompletedTask;
            }
        }

        return NavigateAsync(target);
    }

    /// <summary>Sign-in has shown the main tabs: go on to whatever a notification asked for.</summary>
    public Task MainShownAsync()
    {
        NotificationTarget? pending;
        lock (_gate)
        {
            _mainShown = true;
            pending = _pending;
            _pending = null;
        }

        return pending is null ? Task.CompletedTask : NavigateAsync(pending);
    }

    /// <summary>"alert #4821 on device 7" - ids only, for the diagnostics (#126).</summary>
    private static string Describe(NotificationTarget target) => target switch
    {
        { AlertId: { } alert, DeviceId: { } device } => $"alert #{alert} on device {device}",
        { AlertId: { } alert } => $"alert #{alert}",
        { DeviceId: { } device } => $"device {device}",
        _ => "the alert list",
    };

    private Task NavigateAsync(NotificationTarget target)
    {
        _logger.LogInformation("Notification tapped: opening {Target}", Describe(target));
        if (target.AlertId is { } alertId)
        {
            var parameters = new Dictionary<string, object> { [Routes.AlertIdParameter] = alertId };
            if (target.DeviceId is { } device)
            {
                parameters[Routes.DeviceIdParameter] = device;
            }

            return _navigation.GoToAsync(Routes.AlertDetail, parameters);
        }

        return target.DeviceId is { } deviceId
            ? _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = deviceId })
            : _navigation.GoToAsync(Routes.Alerts);
    }
}
