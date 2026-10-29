using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Services;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// Shares Core's polling across phone consumers. Awaiting a result resumes on
/// the caller's context, so Core's worker events never update bound UI state.
/// The platform still owns scheduling background wakes and notifications.
/// </summary>
public sealed class CorePolling : IDisposable, IAppActivity
{
    private readonly ISessionService _session;
    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly ILoggerFactory _logs;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DeviceMonitor? _devices;
    private SensorMonitor? _sensors;
    private AlertMonitor? _alerts;
    private DeviceCache? _cache;
    private volatile bool _background;
    private volatile bool _reset;
    private long _generation;
    private SensorPollResult? _lastSensors;

    public CorePolling(ILibreNmsClient client, ISessionService session, ISettingsStore settings, ILoggerFactory? logs = null)
    {
        _client = client;
        _session = session;
        _settings = settings;
        _logs = logs ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        Rules = new AlertRuleCache(client, _logs.CreateLogger<AlertRuleCache>());
        Layout = new DashboardLayoutService(settings);
        session.StateChanged += OnSessionChanged;
    }

    public IAlertRuleCache Rules { get; }
    public IDashboardLayoutService Layout { get; }
    public bool IsInBackground => _background;
    public event EventHandler? Changed;

    public void SetForeground(bool foreground)
    {
        _background = !foreground;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureMonitors()
    {
        if (!_session.IsConnected) throw new InvalidOperationException("Not signed in");
        if (_reset) { Reset(); _reset = false; }
        if (_alerts is not null) return;
        var session = new PollSession(_session, this, _generation);
        _cache = new DeviceCache(_client, session, _logs.CreateLogger<DeviceCache>());
        _alerts = new AlertMonitor(_client, session, _settings,
            new NotificationStateStore(_logs.CreateLogger<NotificationStateStore>()), _cache, _logs.CreateLogger<AlertMonitor>());
        _devices = new DeviceMonitor(_client, session, _settings, _cache, _alerts, this, _logs.CreateLogger<DeviceMonitor>());
        _sensors = new SensorMonitor(_client, session, _settings, _cache, _alerts, this, _logs.CreateLogger<SensorMonitor>());
        _sensors.Polled += (_, result) => { if (session.IsConnected) _lastSensors = result; };
    }

    public async Task<IReadOnlyList<Device>?> CachedDevicesAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            EnsureMonitors();
            var generation = Interlocked.Read(ref _generation);
            await _cache!.EnsureCurrentAsync(Array.Empty<int>(), token).ConfigureAwait(false);
            if (generation != Interlocked.Read(ref _generation)) throw new OperationCanceledException("The session changed during the refresh");
            return _cache.IsLoaded ? _cache.All.ToList() : null;
        }
        finally { _gate.Release(); }
    }

    public async Task<SensorPollResult> WidgetSensorsAsync(CancellationToken token = default)
    {
        if (!_reset && _lastSensors is { ObservedAt: { } observed } cached
            && DateTimeOffset.UtcNow - observed < TimeSpan.FromMinutes(15)) return cached;
        return await SensorsAsync(token).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Alert>> AlertsAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            EnsureMonitors();
            var monitor = _alerts!;
            var result = await Next<AlertPollResult>(h => monitor.Polled += h, h => monitor.Polled -= h,
                () => { if (monitor.IsRunning) monitor.RequestRefresh(); else monitor.Start(); }, token).ConfigureAwait(false);
            if (!result.Succeeded) throw new InvalidOperationException(result.ErrorMessage);
            return result.Alerts;
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<Sensor>> SensorReadingsAsync(CancellationToken token = default)
    {
        var result = await SensorsAsync(token).ConfigureAwait(false);
        if (!result.Succeeded) throw new InvalidOperationException(result.ErrorMessage);
        return result.Sensors;
    }

    public async Task<IReadOnlyList<Device>> DevicesAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            EnsureMonitors();
            var monitor = _devices!;
            var result = await Next<DevicePollResult>(h => monitor.Polled += h, h => monitor.Polled -= h,
                () => { if (monitor.IsRunning) monitor.RequestRefresh(); else monitor.Start(); }, token).ConfigureAwait(false);
            if (!result.Succeeded) throw new InvalidOperationException(result.ErrorMessage);
            return result.Devices;
        }
        finally { _gate.Release(); }
    }

    public async Task<SensorPollResult> SensorsAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            EnsureMonitors();
            var monitor = _sensors!;
            return await Next<SensorPollResult>(h => monitor.Polled += h, h => monitor.Polled -= h,
                () => { if (monitor.IsRunning) monitor.RequestRefresh(); else monitor.Start(); }, token).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<T> Next<T>(Action<EventHandler<T>> subscribe, Action<EventHandler<T>> unsubscribe, Action refresh, CancellationToken token)
    {
        var generation = _generation;
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnPolled(object? sender, T result) => completion.TrySetResult(result);
        void OnSessionChanged(object? sender, EventArgs e) => completion.TrySetException(new OperationCanceledException("The session changed during the refresh"));
        _session.StateChanged += OnSessionChanged;
        subscribe(OnPolled);
        try
        {
            refresh();
            var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(90), token).ConfigureAwait(false);
            if (generation != _generation) throw new OperationCanceledException("The session changed during the refresh");
            return result;
        }
        finally { unsubscribe(OnPolled); _session.StateChanged -= OnSessionChanged; }
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        Interlocked.Increment(ref _generation);
        Rules.Clear();
        _reset = true;
    }

    private void Reset()
    {
        _devices?.Dispose();
        _sensors?.Dispose();
        _alerts?.Dispose();
        _devices = null;
        _sensors = null;
        _alerts = null;
        _cache = null;
        _lastSensors = null;
    }

    private sealed class PollSession(ISessionService session, CorePolling owner, long generation) : IConnectionState
    {
        public bool IsConnected => session.IsConnected && generation == Interlocked.Read(ref owner._generation);
    }

    public void Dispose()
    {
        _session.StateChanged -= OnSessionChanged;
        Reset();
    }
}
