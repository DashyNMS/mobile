using DashyNMS.Mobile.Diagnostics;
using DashyNMS.Mobile.Services;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

/// <summary>Feature use, UI stalls, memory and session health (#219), as desktop's (DashyNMS/desktop#502, #503, #506, #507).</summary>
public sealed class UsageHealthTests : IDisposable
{
    private static readonly ErrorReportContext Context = new("2.0.0+412", "ios", "26.1", "arm64");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dashynms-usage-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero));
    private readonly InMemoryPreferences _preferences = new();
    private readonly SentryTransport _transport = new("https://0123456789abcdef@o1.ingest.example.net/42", new NoNetwork());
    private readonly DiagnosticsConsent _consent;
    private DiagnosticsReporter _reporter;
    private AppMetrics _metrics;

    public UsageHealthTests()
    {
        _consent = new DiagnosticsConsent(_preferences, _transport.IsAvailable);
        (_reporter, _metrics) = Launch();
    }

    public void Dispose()
    {
        _metrics.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>A new process: a fresh reporter and metrics over the same folder and choices.</summary>
    private (DiagnosticsReporter, AppMetrics) Launch()
    {
        var reporter = new DiagnosticsReporter(Context, _transport, _consent, Path.Combine(_directory, "queue"), _time) { SendsInBackground = false };
        return (reporter, new AppMetrics(reporter, _consent, _time));
    }

    private SessionHealth Sessions(Func<bool>? sample = null) =>
        new(_reporter, _consent, Path.Combine(_directory, "session"), _time, sample ?? (() => true));

    private void OnScreen(bool errors = true, bool usage = true)
    {
        if (!_consent.HasChosen)
        {
            _consent.Choose(errors, usage);
        }

        _reporter.Start();
        _metrics.Start();
    }

    private List<MetricObservation> Queued(MetricName name)
    {
        _metrics.Flush();
        return _reporter.Queued.OfType<MetricsReport>().SelectMany(r => r.Points)
            .Select(p => p.Observation).Where(o => o.Name == name).ToList();
    }

    // ---- UI stalls

    [Fact]
    public void A_heartbeat_half_a_second_late_is_a_stall_and_a_prompt_one_isnt()
    {
        OnScreen();
        var main = new Queue<Action>();
        using var stalls = new UiStallMonitor(_metrics, main.Enqueue, new HandDrivenTimers(_time));
        stalls.Resume();

        _time.Advance(UiStallMonitor.Interval);
        stalls.Tick();
        _time.Advance(TimeSpan.FromMilliseconds(80));
        main.Dequeue()();

        _time.Advance(UiStallMonitor.Interval);
        stalls.Tick();
        _time.Advance(TimeSpan.FromMilliseconds(750));
        main.Dequeue()();

        Assert.Equal(1, Queued(MetricName.UiStalls).Single().Value);
        Assert.Equal(750, Queued(MetricName.UiStallDuration).Single().Value);
    }

    [Fact]
    public void Time_in_the_background_or_suspended_is_never_a_stall()
    {
        OnScreen();
        var main = new Queue<Action>();
        using var stalls = new UiStallMonitor(_metrics, main.Enqueue, new HandDrivenTimers(_time));
        stalls.Resume();

        _time.Advance(UiStallMonitor.Interval);
        stalls.Tick();
        stalls.Pause(); // to the background with a heartbeat in flight
        _time.Advance(TimeSpan.FromMinutes(10));
        main.Dequeue()();

        stalls.Resume();
        _time.Advance(TimeSpan.FromSeconds(40)); // suspended: the timer itself came late
        stalls.Tick();

        Assert.Empty(main);
        Assert.Empty(Queued(MetricName.UiStalls));
    }

    [Fact]
    public void Stalls_arent_measured_without_usage()
    {
        OnScreen(usage: false);
        var main = new Queue<Action>();
        using var stalls = new UiStallMonitor(_metrics, main.Enqueue, new HandDrivenTimers(_time));
        stalls.Resume();

        _time.Advance(UiStallMonitor.Interval);
        stalls.Tick();

        Assert.Empty(main);
    }

    // ---- Memory

    [Fact]
    public void Memory_is_read_every_five_minutes_on_screen()
    {
        OnScreen();
        var reads = 0;
        using var memory = new MemoryMonitor(_metrics, () =>
        {
            reads++;
            return new MemoryReading(180_000_000, 42_000_000);
        }, _time);

        memory.Resume();
        _time.Advance(MemoryMonitor.Interval);
        memory.Pause();
        _time.Advance(MemoryMonitor.Interval * 3); // in the background
        memory.Sample();

        Assert.Equal(1, reads);
        Assert.Equal(180_000_000, Queued(MetricName.MemoryFootprint).Single().Value);
        Assert.Equal(42_000_000, Queued(MetricName.ManagedHeap).Single().Value);
    }

    [Fact]
    public void Memory_isnt_read_without_usage_and_a_failed_read_is_skipped()
    {
        OnScreen(usage: false);
        var reads = 0;
        using var off = new MemoryMonitor(_metrics, () => { reads++; return new MemoryReading(1, 1); }, _time);
        off.Resume();
        _time.Advance(MemoryMonitor.Interval);
        Assert.Equal(0, reads);

        using var failing = new MemoryMonitor(_metrics, () => throw new PlatformNotSupportedException(), _time);
        failing.Resume();
        failing.Sample();
        Assert.Empty(_reporter.Queued);
    }

    // ---- Feature use

    [Fact]
    public void Feature_use_is_a_fixed_list_counted_once_per_use()
    {
        OnScreen();
        void Use(MetricArea area) => _metrics.Record(new MetricObservation(MetricName.FeatureUse, 1, Area: area));

        Use(MetricArea.Map);
        Use(MetricArea.Map);
        Use(MetricArea.CsvExport);

        var counts = Queued(MetricName.FeatureUse);
        Assert.Equal(2, counts.Single(c => c.Area == MetricArea.Map).Value);
        Assert.Equal(1, counts.Single(c => c.Area == MetricArea.CsvExport).Value);
        Assert.Equal(MetricArea.DashboardEditor, MetricRules.FeatureArea("CustomiseDashboardViewModel"));
        Assert.Null(MetricRules.FeatureArea("DevicesViewModel"));
    }

    [Fact]
    public void Feature_use_isnt_counted_without_usage()
    {
        OnScreen(usage: false);

        _metrics.Record(new MetricObservation(MetricName.FeatureUse, 1, Area: MetricArea.Logs));

        Assert.Empty(_reporter.Queued);
    }

    // ---- Session health

    [Fact]
    public void A_session_that_leaves_the_screen_ends_healthy_with_its_own_id()
    {
        OnScreen();
        var sessions = Sessions();
        sessions.Begin();
        _time.Advance(TimeSpan.FromMinutes(12));
        sessions.End();

        var report = Assert.Single(_reporter.Queued.OfType<MetricsReport>(), r => r.Points.Any(p => p.Observation.Name == MetricName.SessionOutcome));
        Assert.Equal(MetricOutcome.Healthy, report.Points[0].Observation.Outcome);
        Assert.Equal(TimeSpan.FromMinutes(12).TotalMilliseconds, report.Points[1].Observation.Value);

        sessions.Begin(); // back on screen: a fresh id
        sessions.End();
        var ids = _reporter.Queued.OfType<MetricsReport>().Where(r => r.Points.Any(p => p.Observation.Name == MetricName.SessionOutcome)).Select(r => r.EventId);
        Assert.Equal(2, ids.Distinct().Count());
    }

    [Fact]
    public void An_error_in_the_session_makes_it_errored()
    {
        OnScreen();
        var sessions = Sessions();
        sessions.Begin();
        _reporter.Capture(new InvalidOperationException(), ErrorOrigin.UnobservedTask);
        sessions.End();

        Assert.Equal(MetricOutcome.Errored, Queued(MetricName.SessionOutcome).Single().Outcome);
    }

    [Fact]
    public void A_fatal_error_is_a_crash_found_at_the_next_launch()
    {
        OnScreen();
        Sessions().Begin();
        _reporter.Capture(new InvalidOperationException(), ErrorOrigin.Unhandled);

        (_reporter, _metrics) = Launch(); // the process ended; a new one
        OnScreen();
        Sessions().Begin();

        Assert.Equal(MetricOutcome.Crashed, Queued(MetricName.SessionOutcome).Single().Outcome);
        Assert.Empty(Queued(MetricName.SessionDuration));
    }

    [Fact]
    public void A_session_ended_by_the_OS_without_an_error_is_unknown_never_a_crash()
    {
        OnScreen();
        Sessions().Begin();

        (_reporter, _metrics) = Launch(); // gone while on screen, no error seen
        OnScreen();
        Sessions().Begin();

        Assert.Equal(MetricOutcome.Unknown, Queued(MetricName.SessionOutcome).Single().Outcome);
    }

    [Fact]
    public void The_OS_ending_the_app_in_the_background_is_normal()
    {
        OnScreen();
        var sessions = Sessions();
        sessions.Begin();
        sessions.End(); // to the background, where iOS later ends it

        (_reporter, _metrics) = Launch();
        OnScreen();
        Sessions().Begin();

        Assert.DoesNotContain(Queued(MetricName.SessionOutcome), o => o.Outcome is MetricOutcome.Crashed or MetricOutcome.Unknown);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Sessions_need_both_choices(bool errors, bool usage)
    {
        OnScreen(errors, usage);
        var sessions = Sessions();
        sessions.Begin();
        sessions.End();

        Assert.Empty(Queued(MetricName.SessionOutcome));
        Assert.False(File.Exists(Path.Combine(_directory, "session", "session.json")));
    }

    [Fact]
    public void A_background_launch_isnt_a_session()
    {
        _consent.Choose(errors: true, usage: true);
        Sessions().Begin(); // never on screen: the reporter hasn't started

        Assert.False(File.Exists(Path.Combine(_directory, "session", "session.json")));
    }

    /// <summary>The fake clock's time, with timers that never fire: each heartbeat is the test's to make, as a suspended app's would be missed.</summary>
    private sealed class HandDrivenTimers(FakeTimeProvider time) : TimeProvider
    {
        public override long TimestampFrequency => time.TimestampFrequency;

        public override long GetTimestamp() => time.GetTimestamp();

        public override DateTimeOffset GetUtcNow() => time.GetUtcNow();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new Never();

        private sealed class Never : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }
}
