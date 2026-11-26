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
    private SensorMonitor? _sensors;
    private AlertMonitor? _alerts;
    private bool _cachedHasOpenAlerts;
    private long _alertContextGeneration = -1;
    public bool HasCachedOpenAlerts => !_reset && _alertContextGeneration == Volatile.Read(ref _generation) && _cachedHasOpenAlerts;
    private DeviceCache? _cache;
    private volatile bool _background;
    private volatile bool _reset;
    private long _generation;
    private SensorPollResult? _lastSensors;
    private readonly SensorDefinitions? _definitions;
    private int _definitionsVersion;
    private readonly object _sensorSync = new();
    private Task<SensorPollResult>? _sensorsInFlight;
    private readonly object _deviceSync = new();
    private Task<IReadOnlyList<Device>>? _devicesInFlight;

    /// <summary>Most requests get this long before the page gives up on Core's answer.</summary>
    private static readonly TimeSpan PollWait = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Core allows the fleet sensor list at least two minutes (DashyNMS/desktop#373);
    /// waiting a little longer lets Core's own timeout, with its kept readings, arrive first.
    /// </summary>
    private static readonly TimeSpan SensorWait = TimeSpan.FromSeconds(150);

    public CorePolling(ILibreNmsClient client, ISessionService session, ISettingsStore settings, ILoggerFactory? logs = null, SensorDefinitions? definitions = null)
    {
        _definitions = definitions;
        if (definitions is not null)
        {
            definitions.Store.Changed += (_, _) => Interlocked.Increment(ref _definitionsVersion);
        }

        _client = client;
        _session = session;
        _settings = settings;
        _logs = logs ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        Rules = new AlertRuleCache(client, _logs.CreateLogger<AlertRuleCache>());
        Layout = new DashboardLayoutService(settings);
        session.StateChanged += OnSessionChanged;
    }

    /// <summary>Names already known to the session; notification delivery must never wait for the fleet list.</summary>
    public IReadOnlyDictionary<int, Device> CachedDeviceNames => !_reset && _cache is { IsLoaded: true }
        ? _cache.All.GroupBy(d => d.DeviceId).ToDictionary(g => g.Key, g => g.First())
        : new Dictionary<int, Device>();

    /// <summary>
    /// The latest sensor list for this server: Core's kept readings, and their
    /// time, after a failed refresh. Pages show these while the next one loads.
    /// </summary>
    public SensorPollResult? LastSensors => _reset ? null : _lastSensors;

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
        _sensors = new SensorMonitor(_client, session, _settings, _cache, _alerts, this, _logs.CreateLogger<SensorMonitor>());
        _sensors.Polled += (_, result) => { if (session.IsConnected) _lastSensors = result; };
        var alertGeneration = Volatile.Read(ref _generation);
        _alerts.Polled += (_, result) =>
        {
            if (!result.Succeeded || alertGeneration != Volatile.Read(ref _generation)) return;
            _cachedHasOpenAlerts = result.Alerts.Any(a => a.State is AlertState.Active or AlertState.Acknowledged);
            Volatile.Write(ref _alertContextGeneration, alertGeneration);
        };
    }

    public async Task<IReadOnlyList<Device>?> CachedDevicesAsync(CancellationToken token = default)
    {
        var (cache, generation) = await UnderGateAsync(() => _cache!, token).ConfigureAwait(false);
        await cache.EnsureCurrentAsync(Array.Empty<int>(), token).ConfigureAwait(false);
        if (generation != Interlocked.Read(ref _generation)) throw new OperationCanceledException("The session changed during the refresh");
        return cache.IsLoaded ? cache.All.ToList() : null;
    }

    /// <summary>
    /// Creates the monitors if needed and hands one back, holding the gate only for that (#132).
    /// The waits for a poll happen outside it: a device poll runs the maintenance scan, one
    /// request per device, and on a large server that takes minutes. Alerts and the device
    /// cache mustn't queue behind it.
    /// </summary>
    private async Task<(T Value, long Generation)> UnderGateAsync<T>(Func<T> pick, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            EnsureMonitors();
            return (pick(), Interlocked.Read(ref _generation));
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
        var (monitor, contextGeneration) = await UnderGateAsync(() => _alerts!, token).ConfigureAwait(false);
        var result = await Next<AlertPollResult>(h => monitor.Polled += h, h => monitor.Polled -= h,
            () => { if (monitor.IsRunning) monitor.RequestRefresh(); else monitor.Start(); }, PollWait, token, contextGeneration).ConfigureAwait(false);
        if (!result.Succeeded) throw new InvalidOperationException(result.ErrorMessage);
        if (contextGeneration == Volatile.Read(ref _generation))
        {
            _cachedHasOpenAlerts = result.Alerts.Any(a => a.State is AlertState.Active or AlertState.Acknowledged);
            _alertContextGeneration = contextGeneration;
        }
        return result.Alerts;
    }

    /// <summary>
    /// The device list, fresh from the server. Devices, Health and the Alerts list's names ask
    /// together: a request already on its way is shared, and a caller giving up doesn't cancel it.
    /// </summary>
    /// <remarks>
    /// One request, not Core's device monitor (#132). Once started, that monitor checks every
    /// device for maintenance every poll before handing over the list - a request per device,
    /// which on a server of a thousand devices took minutes and never stopped. Maintenance on
    /// the phone comes from <see cref="MaintenanceScan"/>, which the Devices tab and dashboard share.
    /// </remarks>
    public Task<IReadOnlyList<Device>> DevicesAsync(CancellationToken token = default)
    {
        Task<IReadOnlyList<Device>> shared;
        lock (_deviceSync)
        {
            if (_devicesInFlight is not { IsCompleted: false })
            {
                _devicesInFlight = FetchDevicesAsync();
            }

            shared = _devicesInFlight;
        }

        return shared.WaitAsync(token);
    }

    private async Task<IReadOnlyList<Device>> FetchDevicesAsync()
    {
        var (cache, generation) = await UnderGateAsync(() => _cache!, CancellationToken.None).ConfigureAwait(false);
        var devices = await _client.Devices.ListAsync().WaitAsync(PollWait).ConfigureAwait(false);
        if (generation != Interlocked.Read(ref _generation)) throw new OperationCanceledException("The session changed during the refresh");
        cache.UpdateFrom(devices);
        return devices;
    }

    /// <summary>
    /// The next sensor list. Health, the dashboard's Sensors cards and Device
    /// View all ask for the same fleet list, which can take most of a minute
    /// (DashyNMS/desktop#373): a request already on its way is shared rather
    /// than followed by another, and a caller giving up doesn't cancel it for
    /// the rest. A failure keeps Core's last good readings and their time.
    /// </summary>
    public Task<SensorPollResult> SensorsAsync(CancellationToken token = default)
    {
        Task<SensorPollResult> shared;
        lock (_sensorSync)
        {
            if (_sensorsInFlight is not { IsCompleted: false })
            {
                _sensorsInFlight = PollSensorsAsync();
            }

            shared = _sensorsInFlight;
        }

        return shared.WaitAsync(token);
    }

    // Waits outside the gate, so a slow sensor list never holds up the alert and device polls.
    private async Task<SensorPollResult> PollSensorsAsync()
    {
        SensorMonitor monitor;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            EnsureMonitors();
            monitor = _sensors!;
        }
        finally { _gate.Release(); }

        var result = await Next<SensorPollResult>(h => monitor.Polled += h, h => monitor.Polled -= h,
            () => { if (monitor.IsRunning) monitor.RequestRefresh(); else monitor.Start(); }, SensorWait, CancellationToken.None).ConfigureAwait(false);
        await ApplyDefinitionsAsync(result.Sensors).ConfigureAwait(false);
        return result;
    }

    /// <summary>Bumped whenever the sensor definitions change - turned off, another source, a newer catalogue.</summary>
    public int DefinitionsVersion => Volatile.Read(ref _definitionsVersion);

    /// <summary>
    /// State sensors named and coloured from the sensor definitions (#210).
    /// Only in the foreground: Core may fetch an OS's definitions from GitHub,
    /// which a background alert check must never do. Turned off, it clears
    /// the names again, so the readings go back to their numbers.
    /// </summary>
    public async Task ApplyDefinitionsAsync(IReadOnlyList<Sensor> sensors)
    {
        if (_definitions is null || _background || sensors.Count == 0)
        {
            return;
        }

        var devices = CachedDeviceNames;
        try
        {
            await _definitions.Store.ApplyWithDevicesAsync(sensors, id => devices.GetValueOrDefault(id)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or OperationCanceledException or InvalidDataException)
        {
            // Unnamed readings are still readings; Core's status says why.
        }
    }

    private async Task<T> Next<T>(Action<EventHandler<T>> subscribe, Action<EventHandler<T>> unsubscribe, Action refresh, TimeSpan wait, CancellationToken token, long? since = null)
    {
        // The generation the monitor was picked in: a session change after the gate was let go
        // means the monitor may already be disposed, so its poll isn't waited for.
        var generation = since ?? Interlocked.Read(ref _generation);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnPolled(object? sender, T result) => completion.TrySetResult(result);
        void OnSessionChanged(object? sender, EventArgs e) => completion.TrySetException(new OperationCanceledException("The session changed during the refresh"));
        _session.StateChanged += OnSessionChanged;
        subscribe(OnPolled);
        try
        {
            // Checked once subscribed, so a change in between is caught here or by the handler.
            if (generation != Interlocked.Read(ref _generation)) throw new OperationCanceledException("The session changed during the refresh");
            refresh();
            var result = await completion.Task.WaitAsync(wait, token).ConfigureAwait(false);
            if (generation != _generation) throw new OperationCanceledException("The session changed during the refresh");
            return result;
        }
        finally { unsubscribe(OnPolled); _session.StateChanged -= OnSessionChanged; }
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        Interlocked.Increment(ref _generation);
        // The next server's first caller starts its own list, not this one's.
        lock (_deviceSync) _devicesInFlight = null;
        Rules.Clear();
        _reset = true;
    }

    private void Reset()
    {
        _sensors?.Dispose();
        _alerts?.Dispose();
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
