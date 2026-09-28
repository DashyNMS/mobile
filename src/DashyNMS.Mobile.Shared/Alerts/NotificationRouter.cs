using DashyNMS.Mobile.Services;
using DesktopNMS.Services;

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
    private readonly object _gate = new();
    private NotificationTarget? _pending;
    private bool _mainShown;

    public NotificationRouter(ISessionService session, INavigationService navigation)
    {
        _session = session;
        _navigation = navigation;
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

    private Task NavigateAsync(NotificationTarget target)
    {
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
