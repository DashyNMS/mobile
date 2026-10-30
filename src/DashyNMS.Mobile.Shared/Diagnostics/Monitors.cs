using System.Text.Json;
using System.Text.Json.Serialization;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Diagnostics;

/// <summary>
/// Main-thread stalls (#219), as desktop finds them (DashyNMS/desktop#502):
/// once a second, a no-op is posted to the main thread, and one that runs
/// half a second or more late is a stall. It looks at nothing but the time.
/// </summary>
/// <remarks>
/// Runs only on screen, between <see cref="Resume"/> and <see cref="Pause"/>.
/// A heartbeat that itself came late - the app suspended, the clock changed -
/// says nothing about the main thread, so it's dropped rather than counted.
/// </remarks>
public sealed class UiStallMonitor : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan StallThreshold = TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan MaximumGap = TimeSpan.FromSeconds(3);

    private readonly object _gate = new();
    private readonly IAppMetrics _metrics;
    private readonly Action<Action> _post;
    private readonly TimeProvider _time;
    private readonly ITimer _timer;
    private bool _running;
    private bool _pending;
    private bool _disposed;
    private long _lastTick;
    private long _sent;
    private int _epoch;

    /// <param name="post">Runs an action on the main thread.</param>
    public UiStallMonitor(IAppMetrics metrics, Action<Action> post, TimeProvider? time = null)
    {
        _metrics = metrics;
        _post = post;
        _time = time ?? TimeProvider.System;
        _timer = _time.CreateTimer(_ => Tick(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>On screen: heartbeats start.</summary>
    public void Resume()
    {
        lock (_gate)
        {
            if (_disposed || _running)
            {
                return;
            }

            _running = true;
            _pending = false;
            _epoch++;
            _lastTick = _time.GetTimestamp();
            _timer.Change(Interval, Interval);
        }
    }

    /// <summary>In the background: no heartbeats, and one in flight doesn't count.</summary>
    public void Pause()
    {
        lock (_gate)
        {
            _running = false;
            _pending = false;
            _epoch++;
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    internal void Tick()
    {
        int epoch;
        lock (_gate)
        {
            var gap = _time.GetElapsedTime(_lastTick);
            _lastTick = _time.GetTimestamp();
            if (!_running || _disposed || _pending || gap > MaximumGap || !_metrics.IsEnabled)
            {
                return;
            }

            _pending = true;
            _sent = _time.GetTimestamp();
            epoch = _epoch;
        }

        try
        {
            _post(() => Acknowledge(epoch));
        }
        catch (Exception)
        {
            lock (_gate)
            {
                _pending = false;
            }
        }
    }

    private void Acknowledge(int epoch)
    {
        TimeSpan late;
        lock (_gate)
        {
            if (!_pending || epoch != _epoch || !_running)
            {
                return;
            }

            _pending = false;
            late = _time.GetElapsedTime(_sent);
        }

        if (late >= StallThreshold && late <= TimeSpan.FromMinutes(5))
        {
            _metrics.Record(new MetricObservation(MetricName.UiStalls, 1));
            _metrics.Record(new MetricObservation(MetricName.UiStallDuration, late.TotalMilliseconds));
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _running = false;
        }

        _timer.Dispose();
    }
}

/// <summary>The app's memory as the OS counts it, and the .NET heap; null where the OS wouldn't say.</summary>
public sealed record MemoryReading(long? FootprintBytes, long ManagedHeapBytes);

/// <summary>
/// Memory (#219, DashyNMS/desktop#506): the footprint (iOS) or PSS (Android)
/// and the managed heap, every five minutes on screen. Totals only - never
/// what's in memory.
/// </summary>
public sealed class MemoryMonitor : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private const long MaximumBytes = 1_099_511_627_776;

    private readonly object _gate = new();
    private readonly IAppMetrics _metrics;
    private readonly Func<MemoryReading> _read;
    private readonly TimeProvider _time;
    private readonly ITimer _timer;
    private bool _running;
    private bool _disposed;

    /// <param name="read">The platform's reading: iOS's or Android's, from the app head.</param>
    public MemoryMonitor(IAppMetrics metrics, Func<MemoryReading> read, TimeProvider? time = null)
    {
        _metrics = metrics;
        _read = read;
        _time = time ?? TimeProvider.System;
        _timer = _time.CreateTimer(_ => Sample(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>On screen: read every five minutes, starting five minutes from now.</summary>
    public void Resume()
    {
        lock (_gate)
        {
            if (_disposed || _running)
            {
                return;
            }

            _running = true;
            _timer.Change(Interval, Interval);
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            _running = false;
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    internal void Sample()
    {
        lock (_gate)
        {
            if (!_running || _disposed || !_metrics.IsEnabled)
            {
                return;
            }
        }

        MemoryReading reading;
        try
        {
            reading = _read();
        }
        catch (Exception)
        {
            // The OS wouldn't say this time: skipped, never an error report.
            return;
        }

        if (reading.FootprintBytes is { } footprint && footprint is > 0 and <= MaximumBytes)
        {
            _metrics.Record(new MetricObservation(MetricName.MemoryFootprint, footprint));
        }

        if (reading.ManagedHeapBytes is > 0 and <= MaximumBytes)
        {
            _metrics.Record(new MetricObservation(MetricName.ManagedHeap, reading.ManagedHeapBytes));
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _running = false;
        }

        _timer.Dispose();
    }
}

/// <summary>
/// Session health (#219, DashyNMS/desktop#507): each time the app comes on
/// screen is a session with a fresh random id, ending healthy, errored,
/// crashed or unknown. It needs both choices - error reports say whether it
/// crashed - and background alert checks aren't sessions.
/// </summary>
/// <remarks>
/// A session ends when the app leaves the screen, healthy or errored. One
/// whose end was never seen - the process gone while on screen - is found at
/// the next launch: crashed if a fatal error was reported in it, otherwise
/// unknown. iOS and Android ending the app in the background is normal and
/// happens after its session ended, so it's never a crash.
/// </remarks>
public sealed class SessionHealth
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 4,
    };

    private readonly object _gate = new();
    private readonly DiagnosticsReporter _reporter;
    private readonly IDiagnosticsConsent _consent;
    private readonly string _marker;
    private readonly TimeProvider _time;
    private readonly Func<bool> _sampleDuration;
    private Session? _session;
    private long _started;
    private bool _recovered;

    public SessionHealth(DiagnosticsReporter reporter, IDiagnosticsConsent consent, string directory,
        TimeProvider? time = null, Func<bool>? sampleDuration = null)
    {
        _reporter = reporter;
        _consent = consent;
        _marker = Path.Combine(directory, "session.json");
        _time = time ?? TimeProvider.System;
        _sampleDuration = sampleDuration ?? (() => Random.Shared.Next(10) == 0);
        _reporter.ErrorCaptured += (_, origin) => ErrorSeen(origin);
        _consent.Changed += (_, _) =>
        {
            if (!Allowed)
            {
                lock (_gate)
                {
                    _session = null;
                    Delete();
                }
            }
        };
    }

    private bool Allowed => _consent.ShareErrors && _consent.ShareUsage;

    /// <summary>
    /// On screen: last launch's unfinished session is settled, then a new one
    /// begins, unless one is already running.
    /// </summary>
    public void Begin()
    {
        lock (_gate)
        {
            if (!Allowed || !_reporter.MeasuringAllowed)
            {
                Delete();
                return;
            }

            if (!_recovered)
            {
                _recovered = true;
                Recover();
            }

            if (_session is not null)
            {
                return;
            }

            _session = new Session(1, Guid.NewGuid(), _time.GetUtcNow(), Errored: false, Crashed: false);
            _started = _time.GetTimestamp();
            Save();
        }
    }

    /// <summary>Off screen: the session ended on its own.</summary>
    public void End()
    {
        lock (_gate)
        {
            if (_session is not { } session)
            {
                return;
            }

            _session = null;
            var took = _time.GetElapsedTime(_started);
            if (Allowed && Send(session.Id, session.Errored ? MetricOutcome.Errored : MetricOutcome.Healthy,
                _sampleDuration() ? took : null))
            {
                Delete();
            }
        }
    }

    private void ErrorSeen(ErrorOrigin origin)
    {
        lock (_gate)
        {
            if (_session is null)
            {
                return;
            }

            // Saved straight away: after a fatal error there may be no later chance.
            _session = origin == ErrorOrigin.Unhandled ? _session with { Crashed = true } : _session with { Errored = true };
            Save();
        }
    }

    private void Recover()
    {
        try
        {
            if (!File.Exists(_marker) || new FileInfo(_marker).Length is <= 0 or > 1024)
            {
                Delete();
                return;
            }

            var session = JsonSerializer.Deserialize<Session>(File.ReadAllBytes(_marker), Options);
            var now = _time.GetUtcNow();
            if (session is { Schema: 1 } && session.Id != Guid.Empty
                && session.Started > now - DiagnosticsReporter.MaximumAge && session.Started <= now.AddMinutes(5))
            {
                Send(session.Id, session.Crashed ? MetricOutcome.Crashed : MetricOutcome.Unknown, took: null);
            }
        }
        catch (Exception)
        {
            // An unreadable marker is dropped, not guessed at.
        }

        Delete();
    }

    /// <summary>The outcome, and how long if sampled, as their own report: its id is the session's.</summary>
    private bool Send(Guid id, MetricOutcome outcome, TimeSpan? took)
    {
        var now = _time.GetUtcNow();
        var points = new List<MetricPoint> { new(now, new MetricObservation(MetricName.SessionOutcome, 1, outcome)) };
        if (took is { } duration && duration >= TimeSpan.Zero)
        {
            points.Add(new MetricPoint(now, new MetricObservation(MetricName.SessionDuration, duration.TotalMilliseconds, outcome)));
        }

        return _reporter.RecordMetrics(points, id);
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_marker)!);
            var temporary = _marker + ".tmp";
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(_session, Options));
            File.Move(temporary, _marker, overwrite: true);
        }
        catch (Exception)
        {
            // Without a marker the session's end can't be recovered; it's simply not counted.
        }
    }

    private void Delete()
    {
        try
        {
            File.Delete(_marker);
            File.Delete(_marker + ".tmp");
        }
        catch (Exception)
        {
        }
    }

    private sealed record Session(int Schema, Guid Id, DateTimeOffset Started, bool Errored, bool Crashed);
}
