using System.Runtime.CompilerServices;
using System.Text.Json;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Diagnostics;

/// <summary>
/// Opt-in error reports (#217) and usage measurements (#218), as desktop's
/// (DashyNMS/desktop#62, #498): each is saved to a small queue first - at
/// most ten reports, of 32 KiB each, for seven days - and sent from there, so
/// a fatal error goes at the next launch and nothing is lost going offline.
/// </summary>
/// <remarks>
/// Nothing is captured until <see cref="Start"/>, which the app calls only
/// once it's on screen: iOS and Android can launch it in the background for
/// an alert check, and those launches are never reported. Each kind goes only
/// while its own choice is on; turning one off deletes what's queued for it
/// and abandons anything being sent. Measurements go at most once a minute.
/// </remarks>
public sealed class DiagnosticsReporter : IErrorReports, IDisposable
{
    public const int MaximumQueued = 10;

    public static readonly TimeSpan MaximumAge = TimeSpan.FromDays(7);

    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(12);

    private readonly ErrorReportContext _context;
    private readonly IDiagnosticsTransport _transport;
    private readonly IDiagnosticsConsent _consent;
    private readonly string _directory;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _sender = new(1, 1);
    private readonly ConditionalWeakTable<Exception, object> _captured = new();
    private CancellationTokenSource _withdrawn = new();
    private long _generation;
    private bool _started;
    private bool _disposed;
    private DateTimeOffset _nextErrorAttempt;
    private DateTimeOffset _nextUsageAttempt;
    private bool _errorsWereAllowed;
    private bool _usageWasAllowed;
    private int _pumpScheduled;

    public DiagnosticsReporter(ErrorReportContext context, IDiagnosticsTransport transport, IDiagnosticsConsent consent, string directory, TimeProvider? time = null)
    {
        _context = ErrorPayload.SafeContext(context);
        _transport = transport;
        _consent = consent;
        _directory = directory;
        _time = time ?? TimeProvider.System;
        _consent.Changed += (_, _) => ConsentChanged();
        _errorsWereAllowed = ErrorsAllowed;
        _usageWasAllowed = UsageAllowed;

        // Reports left from before a choice was turned off are never sent.
        lock (_gate)
        {
            if (!ErrorsAllowed && !UsageAllowed)
            {
                Purge();
            }
            else
            {
                _ = ReadQueue();
            }
        }
    }

    public ErrorReportContext Context => _context;

    /// <summary>Sends in the background after a capture or <see cref="Start"/>; off in tests, which send themselves.</summary>
    internal bool SendsInBackground { get; init; } = true;

    private bool ErrorsAllowed => _transport.IsAvailable && _consent.ShareErrors;

    private bool UsageAllowed => _transport.IsAvailable && _consent.ShareUsage;

    /// <summary>The app has been on screen, and usage is shared: measurements may be kept.</summary>
    public bool MeasuringAllowed
    {
        get
        {
            lock (_gate)
            {
                return _started && !_disposed && UsageAllowed;
            }
        }
    }

    private bool IsAllowed(DiagnosticReport report) => report is MetricsReport ? UsageAllowed : ErrorsAllowed;

    /// <summary>
    /// The app is on screen: capture from now on, and send what's queued -
    /// last launch's fatal error, say. Called again on each return to the
    /// foreground, which is when anything held back offline goes.
    /// </summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _started = true;
        }

        Pump();
    }

    public void Capture(Exception exception, ErrorOrigin origin)
    {
        if (origin == ErrorOrigin.Caught && ErrorPayload.IsExpected(exception))
        {
            return;
        }

        lock (_gate)
        {
            if (!_started || _disposed || !ErrorsAllowed || _captured.TryGetValue(exception, out _))
            {
                return;
            }

            try
            {
                var report = ErrorPayload.Create(exception, origin, _context, _time.GetUtcNow());
                if (!ErrorPayload.IsValid(report) || !Persist(report))
                {
                    return;
                }

                _captured.Add(exception, new object());
            }
            catch (Exception)
            {
                // Reporting must never be the thing that fails.
                return;
            }
        }

        // The app is closing: the report waits for the next launch.
        if (origin != ErrorOrigin.Unhandled)
        {
            Pump();
        }
    }

    /// <summary>A minute's measurements, from <see cref="AppMetrics"/>: queued if usage is still shared.</summary>
    public bool RecordMetrics(IReadOnlyList<MetricPoint> points)
    {
        lock (_gate)
        {
            if (!_started || _disposed || !UsageAllowed || points.Count == 0)
            {
                return false;
            }

            try
            {
                var report = new MetricsReport(1, Guid.NewGuid(), points.Min(point => point.Timestamp), _context, points);
                return ErrorPayload.IsValid(report) && Persist(report);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>What's waiting, oldest first - for tests and the queue's own upkeep.</summary>
    internal IReadOnlyList<DiagnosticReport> Queued
    {
        get
        {
            lock (_gate)
            {
                return ReadQueue().Select(item => item.Report).ToList();
            }
        }
    }

    /// <summary>Sends the queue in order until it's empty, Sentry asks us to wait, or the choice is withdrawn.</summary>
    public async Task SendPendingAsync(CancellationToken cancellationToken = default)
    {
        var acquired = false;
        try
        {
            await _sender.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
            while (!cancellationToken.IsCancellationRequested)
            {
                string path;
                DiagnosticReport report;
                long generation;
                CancellationTokenSource linked;
                using var timeout = new CancellationTokenSource(SendTimeout, _time);
                lock (_gate)
                {
                    if (!_started || _disposed)
                    {
                        return;
                    }

                    var now = _time.GetUtcNow();
                    var queue = ReadQueue()
                        .Where(item => now >= (item.Report is MetricsReport ? _nextUsageAttempt : _nextErrorAttempt))
                        .ToList();
                    if (queue.Count == 0)
                    {
                        return;
                    }

                    (path, report) = queue[0];
                    generation = _generation;
                    linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _withdrawn.Token, timeout.Token);
                }

                SendResult result;
                using (linked)
                {
                    try
                    {
                        result = await _transport.SendAsync(report, linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!timeout.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception)
                    {
                        result = new SendResult(SendDisposition.Retry);
                    }
                }

                lock (_gate)
                {
                    if (_disposed || generation != _generation)
                    {
                        return;
                    }

                    if (result.Disposition == SendDisposition.Retry)
                    {
                        Wait(report, result.RetryAfter ?? TimeSpan.FromMinutes(1));
                        continue;
                    }

                    Delete(path);
                    if (File.Exists(path))
                    {
                        return;
                    }

                    if (result.RetryAfter is { } backoff && backoff > TimeSpan.Zero)
                    {
                        Wait(report, backoff);
                    }

                    // Measurements go at most once a minute (#218).
                    if (report is MetricsReport)
                    {
                        Wait(report, TimeSpan.FromMinutes(1));
                    }
                }
            }
        }
        catch (Exception)
        {
            // Offline, a full disk: the queue is still there for next time.
        }
        finally
        {
            if (acquired)
            {
                _sender.Release();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _generation++;
            _withdrawn.Cancel();
            _withdrawn.Dispose();
        }
    }

    private void ConsentChanged()
    {
        lock (_gate)
        {
            var errors = ErrorsAllowed;
            var usage = UsageAllowed;
            var withdrawn = (_errorsWereAllowed && !errors) || (_usageWasAllowed && !usage);
            _errorsWereAllowed = errors;
            _usageWasAllowed = usage;
            if (_disposed || !withdrawn)
            {
                return;
            }

            // Withdrawn: what's being sent is abandoned, and what's queued for it goes.
            _generation++;
            _withdrawn.Cancel();
            _withdrawn.Dispose();
            _withdrawn = new CancellationTokenSource();
            if (!errors)
            {
                _nextErrorAttempt = default;
            }

            if (!usage)
            {
                _nextUsageAttempt = default;
            }

            _ = ReadQueue();
        }
    }

    private void Pump()
    {
        if (!SendsInBackground || Interlocked.CompareExchange(ref _pumpScheduled, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await SendPendingAsync().ConfigureAwait(false);
            }
            finally
            {
                Volatile.Write(ref _pumpScheduled, 0);
            }
        });
    }

    /// <summary>Holds back this report's kind; Sentry's limits are per kind (error or metric).</summary>
    private void Wait(DiagnosticReport report, TimeSpan delay)
    {
        var next = _time.GetUtcNow() + TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds, 1, 86400));
        if (report is MetricsReport)
        {
            _nextUsageAttempt = next > _nextUsageAttempt ? next : _nextUsageAttempt;
        }
        else
        {
            _nextErrorAttempt = next > _nextErrorAttempt ? next : _nextErrorAttempt;
        }
    }

    private bool Persist(DiagnosticReport report)
    {
        Directory.CreateDirectory(_directory);
        var queue = ReadQueue();
        if (queue.Count >= MaximumQueued)
        {
            // The oldest makes room: the latest error is the likeliest still to matter.
            Delete(queue[0].Path);
            if (File.Exists(queue[0].Path))
            {
                return false;
            }
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(report, ErrorPayload.JsonOptions);
        if (bytes.Length > ErrorPayload.MaximumBytes)
        {
            return false;
        }

        var path = Path.Combine(_directory, report.EventId.ToString("N") + ".json");
        var temporary = path + ".tmp";
        try
        {
            // Written whole, then renamed: a crash mid-write leaves no half report.
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path);
            return true;
        }
        finally
        {
            Delete(temporary);
        }
    }

    /// <summary>
    /// The queue, oldest first, with anything no longer allowed, stale,
    /// oversized, malformed or renamed deleted on the way.
    /// </summary>
    private List<(string Path, DiagnosticReport Report)> ReadQueue()
    {
        var queue = new List<(string Path, DiagnosticReport Report)>();
        if (!Directory.Exists(_directory))
        {
            return queue;
        }

        foreach (var partial in Directory.EnumerateFiles(_directory, "*.tmp"))
        {
            Delete(partial);
        }

        var now = _time.GetUtcNow();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                var file = new FileInfo(path);
                var report = file.Length is > 0 and <= ErrorPayload.MaximumBytes
                    ? JsonSerializer.Deserialize<DiagnosticReport>(File.ReadAllBytes(path), ErrorPayload.JsonOptions)
                    : null;
                if (report is null || !ErrorPayload.IsValid(report) || !IsAllowed(report)
                    || report.Timestamp < now - MaximumAge || report.Timestamp > now.AddMinutes(5)
                    || Path.GetFileName(path) != report.EventId.ToString("N") + ".json")
                {
                    Delete(path);
                    continue;
                }

                queue.Add((path, report));
            }
            catch (Exception)
            {
                Delete(path);
            }
        }

        queue.Sort((left, right) => left.Report.Timestamp.CompareTo(right.Report.Timestamp));
        while (queue.Count > MaximumQueued)
        {
            Delete(queue[0].Path);
            queue.RemoveAt(0);
        }

        return queue;
    }

    private void Purge()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                foreach (var path in Directory.EnumerateFiles(_directory))
                {
                    Delete(path);
                }
            }
        }
        catch (Exception)
        {
            // Tried again at the next change or launch.
        }
    }

    private static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
