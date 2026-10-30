using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Diagnostics;

/// <summary>
/// Usage and performance measurements (#218), as desktop batches them
/// (DashyNMS/desktop#498): counts add up, timings are sampled - the first,
/// then every tenth in each series - and once a minute what's gathered goes
/// to the queue, at most <see cref="MetricRules.MaximumPoints"/> points.
/// </summary>
/// <remarks>
/// Nothing is kept unless usage is shared and the app is on screen; turning
/// usage off drops what's gathered as well as what's queued.
/// </remarks>
public sealed class AppMetrics : IAppMetrics, IDisposable
{
    /// <summary>How often what's gathered is queued and sent.</summary>
    public static readonly TimeSpan FlushInterval = TimeSpan.FromMinutes(1);

    private const int SampleEvery = 10;

    private readonly DiagnosticsReporter _reporter;
    private readonly TimeProvider _time;
    private readonly ITimer _timer;
    private readonly object _gate = new();
    private readonly List<MetricPoint> _points = [];
    private readonly Dictionary<(MetricName, MetricOutcome, MetricArea), int> _samples = [];
    private bool _started;
    private bool _disposed;

    public AppMetrics(DiagnosticsReporter reporter, IDiagnosticsConsent consent, TimeProvider? time = null)
    {
        _reporter = reporter;
        _time = time ?? TimeProvider.System;
        _timer = _time.CreateTimer(_ => Flush(send: true), null, FlushInterval, FlushInterval);
        consent.Changed += (_, _) =>
        {
            if (!consent.ShareUsage)
            {
                Clear();
            }
        };
    }

    public bool IsEnabled
    {
        get
        {
            lock (_gate)
            {
                return !_disposed && _reporter.MeasuringAllowed;
            }
        }
    }

    /// <summary>
    /// The app is on screen. The first time, that's the app starting (#218):
    /// counted once a process, so a background wake never counts.
    /// </summary>
    public void Start()
    {
        bool first;
        lock (_gate)
        {
            first = !_started;
            _started = true;
        }

        if (first)
        {
            Record(new MetricObservation(MetricName.AppStart, 1));
        }
    }

    public void Record(MetricObservation observation)
    {
        lock (_gate)
        {
            if (_disposed || !_reporter.MeasuringAllowed || !MetricRules.IsValid(observation))
            {
                return;
            }

            var key = (observation.Name, observation.Outcome, observation.Area);
            if (MetricRules.IsCounter(observation.Name))
            {
                var index = _points.FindIndex(p => (p.Observation.Name, p.Observation.Outcome, p.Observation.Area) == key);
                if (index >= 0)
                {
                    var point = _points[index];
                    _points[index] = point with
                    {
                        Observation = observation with { Value = Math.Min(1_000_000, point.Observation.Value + observation.Value) },
                    };
                    return;
                }
            }
            else
            {
                if (!_samples.TryGetValue(key, out var seen) && _samples.Count >= MetricRules.MaximumPoints)
                {
                    return;
                }

                _samples[key] = (seen + 1) % SampleEvery;
                if (seen != 0)
                {
                    return;
                }
            }

            if (_points.Count < MetricRules.MaximumPoints)
            {
                _points.Add(new MetricPoint(_time.GetUtcNow(), observation));
            }
        }
    }

    /// <summary>Queues what's gathered; with <paramref name="send"/>, sends the queue too.</summary>
    public void Flush(bool send = false)
    {
        MetricPoint[] snapshot;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            snapshot = [.. _points];
            _points.Clear();
        }

        if (snapshot.Length > 0)
        {
            _reporter.RecordMetrics(snapshot);
        }

        if (send)
        {
            _ = _reporter.SendPendingAsync();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _points.Clear();
        }

        _timer.Dispose();
    }

    private void Clear()
    {
        lock (_gate)
        {
            _points.Clear();
            _samples.Clear();
        }
    }
}
