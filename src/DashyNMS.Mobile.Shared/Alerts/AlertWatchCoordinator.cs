using DesktopNMS.Core.Configuration;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Alerts;

/// <summary>
/// Keeps alert checks running when they should be: in the background whenever
/// someone is signed in with notifications on, and on a timer (desktop's poll
/// interval) while the app is open.
/// </summary>
public sealed class AlertWatchCoordinator : IDisposable
{
    /// <summary>The fastest the in-app timer checks, whatever the poll interval says.</summary>
    public static readonly TimeSpan MinimumForegroundInterval = TimeSpan.FromSeconds(30);

    private readonly ISessionService _session;
    private readonly ISettingsStore _settings;
    private readonly AlertWatcher _watcher;
    private readonly IBackgroundAlertScheduler _scheduler;
    private readonly IAlertWatchStore _store;
    private readonly TimeProvider _time;
    private readonly ILogger<AlertWatchCoordinator> _logger;
    private readonly object _gate = new();
    private CancellationTokenSource? _loop;
    private bool _foreground;
    private bool _started;

    public AlertWatchCoordinator(
        ISessionService session,
        ISettingsStore settings,
        AlertWatcher watcher,
        IBackgroundAlertScheduler scheduler,
        IAlertWatchStore store,
        TimeProvider time,
        ILogger<AlertWatchCoordinator> logger)
    {
        _session = session;
        _settings = settings;
        _watcher = watcher;
        _scheduler = scheduler;
        _store = store;
        _time = time;
        _logger = logger;
    }

    /// <summary>True while the in-app timer is running.</summary>
    public bool IsForegroundLoopRunning
    {
        get
        {
            lock (_gate)
            {
                return _loop is not null;
            }
        }
    }

    /// <summary>
    /// Starts listening for sign-in and sign-out. Deliberately changes nothing
    /// yet: at launch nobody is signed in until the saved session is restored,
    /// and cancelling background checks then would undo them on every launch.
    /// </summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _session.StateChanged += OnSessionChanged;
    }

    /// <summary>The app came to the front (true) or went to the background (false).</summary>
    public void SetForeground(bool foreground)
    {
        _foreground = foreground;
        Apply();
    }

    /// <summary>Call after changing notification settings.</summary>
    public void SettingsChanged() => Apply();

    public void Dispose()
    {
        _session.StateChanged -= OnSessionChanged;
        StopLoop();
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        if (!_session.IsConnected)
        {
            // Signed out: nothing to check with, and the next sign-in (maybe
            // to another server) should start from a fresh baseline.
            _scheduler.Cancel();
            _store.Clear();
            StopLoop();
            return;
        }

        Apply();
    }

    private void Apply()
    {
        if (!_session.IsConnected)
        {
            StopLoop();
            return;
        }

        if (!_settings.Current.Notifications.Enabled)
        {
            _scheduler.Cancel();
            StopLoop();
            return;
        }

        _scheduler.Schedule();

        if (_foreground)
        {
            StartLoop();
        }
        else
        {
            StopLoop();
        }
    }

    private void StartLoop()
    {
        lock (_gate)
        {
            if (_loop is not null)
            {
                return;
            }

            _loop = new CancellationTokenSource();
            var token = _loop.Token;
            _ = Task.Run(() => RunLoopAsync(token), CancellationToken.None);
        }
    }

    private void StopLoop()
    {
        lock (_gate)
        {
            _loop?.Cancel();
            _loop?.Dispose();
            _loop = null;
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _watcher.CheckAsync(cancellationToken).ConfigureAwait(false);

            var interval = TimeSpan.FromSeconds(_settings.Current.PollIntervalSeconds);
            if (interval < MinimumForegroundInterval)
            {
                interval = MinimumForegroundInterval;
            }

            try
            {
                await Task.Delay(interval, _time, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        _logger.LogDebug("In-app alert timer stopped");
    }
}
