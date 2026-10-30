using System.Net;
using System.Text;
using System.Text.Json;
using DashyNMS.Mobile.Diagnostics;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

/// <summary>Start-up, screen-load and request measurements (#218), as desktop's (DashyNMS/desktop#498 to #501).</summary>
public sealed class MetricsTests : IDisposable
{
    private static readonly ErrorReportContext Context = new("2.0.0+412", "android", "16", "arm64");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dashynms-metrics-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero));
    private readonly InMemoryPreferences _preferences = new();
    private readonly RecordingHandler _handler = new();
    private readonly DiagnosticsConsent _consent;
    private readonly DiagnosticsReporter _reporter;
    private readonly AppMetrics _metrics;

    public MetricsTests()
    {
        var transport = new SentryTransport("https://0123456789abcdef@o1.ingest.example.net/42", _handler);
        _consent = new DiagnosticsConsent(_preferences, transport.IsAvailable);
        _reporter = new DiagnosticsReporter(Context, transport, _consent, _directory, _time) { SendsInBackground = false };
        _metrics = new AppMetrics(_reporter, _consent, _time);
    }

    public void Dispose()
    {
        _metrics.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private void OnScreen(bool usage = true, bool errors = false)
    {
        _consent.Choose(errors, usage);
        _reporter.Start();
        _metrics.Start();
    }

    private IReadOnlyList<MetricPoint> Queued() =>
        _reporter.Queued.OfType<MetricsReport>().SelectMany(r => r.Points).ToList();

    [Fact]
    public void Nothing_is_measured_before_a_choice_or_off_screen()
    {
        _reporter.Start();
        _metrics.Start();
        _metrics.Record(new MetricObservation(MetricName.ApiRequests, 1));
        _metrics.Flush();
        Assert.Empty(_reporter.Queued);

        _consent.Choose(errors: true, usage: false); // error reports only
        _metrics.Record(new MetricObservation(MetricName.ApiRequests, 1));
        _metrics.Flush();
        Assert.Empty(_reporter.Queued);
    }

    [Fact]
    public void A_background_launch_is_never_counted_or_measured()
    {
        _consent.Choose(errors: false, usage: true);

        // Never on screen: an alert check's launch.
        _metrics.Record(new MetricObservation(MetricName.ApiRequests, 1));
        _metrics.Flush();

        Assert.False(_metrics.IsEnabled);
        Assert.Empty(_reporter.Queued);
    }

    [Fact]
    public void The_app_start_is_counted_once_a_process()
    {
        OnScreen();
        _metrics.Start(); // back from the background
        _metrics.Flush();

        var start = Assert.Single(Queued());
        Assert.Equal((MetricName.AppStart, 1d), (start.Observation.Name, start.Observation.Value));
    }

    [Fact]
    public void Counts_add_up_and_timings_are_sampled_one_in_ten()
    {
        OnScreen();
        for (var i = 0; i < 25; i++)
        {
            Measurements.Request(_metrics, new ApiRequestMeasurement(ApiOperation.Alerts, TimeSpan.FromMilliseconds(120), ApiRequestOutcome.Success, ApiResponseClass.Success));
        }

        _metrics.Flush();

        var points = Queued();
        var count = Assert.Single(points, p => p.Observation.Name == MetricName.ApiRequests);
        Assert.Equal((25d, MetricArea.ApiAlerts, MetricOutcome.Success), (count.Observation.Value, count.Observation.Area, count.Observation.Outcome));
        Assert.Equal(3, points.Count(p => p.Observation.Name == MetricName.ApiDuration)); // the 1st, 11th and 21st
    }

    [Fact]
    public void A_minutes_points_are_capped()
    {
        OnScreen();
        foreach (var area in Enum.GetValues<MetricArea>())
        {
            foreach (var outcome in Enum.GetValues<MetricOutcome>())
            {
                _metrics.Record(new MetricObservation(MetricName.ScreenLoadDuration, 10, outcome, area));
            }
        }

        _metrics.Flush();

        Assert.Equal(MetricRules.MaximumPoints, Queued().Count);
    }

    [Fact]
    public async Task Measurements_go_once_a_minute_at_most()
    {
        OnScreen();
        _metrics.Flush();
        await _reporter.SendPendingAsync();
        Assert.Single(_handler.Requests);

        _metrics.Record(new MetricObservation(MetricName.ApiRequests, 1));
        _metrics.Flush();
        await _reporter.SendPendingAsync();
        Assert.Single(_handler.Requests); // held back

        _time.Advance(AppMetrics.FlushInterval);
        await _reporter.SendPendingAsync();
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task Turning_usage_off_drops_what_is_gathered_and_queued_but_keeps_error_reports()
    {
        _handler.Status = HttpStatusCode.ServiceUnavailable;
        OnScreen(usage: true, errors: true);
        _reporter.Capture(new InvalidOperationException(), ErrorOrigin.Unhandled);
        _metrics.Flush();
        _metrics.Record(new MetricObservation(MetricName.ApiRequests, 1));
        Assert.Equal(2, _reporter.Queued.Count);

        _consent.SetShareUsage(false);
        _metrics.Flush();

        Assert.IsType<ErrorReport>(Assert.Single(_reporter.Queued));
        _handler.Status = HttpStatusCode.OK;
        _time.Advance(TimeSpan.FromHours(1));
        await _reporter.SendPendingAsync();
        Assert.DoesNotContain(_handler.Requests, r => r.Body.Contains("trace_metric"));
    }

    [Fact]
    public void The_envelope_has_only_the_fixed_dimensions()
    {
        OnScreen();
        Measurements.PageLoaded(_metrics, new NetworkMapViewModel(), new FirstLoad(TimeSpan.FromMilliseconds(850), LoadOutcome.Failed));
        _metrics.Flush();

        var report = _reporter.Queued.OfType<MetricsReport>().Single();
        var transport = new SentryTransport("https://0123456789abcdef@o1.ingest.example.net/42", _handler);
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(transport.Metrics(report)));

        Assert.Equal("never", json.RootElement.GetProperty("ingest_settings").GetProperty("infer_ip").GetString());
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        foreach (var item in items)
        {
            Assert.Equal(
                ["sentry.release", "operating_system", "os_version", "architecture", "outcome", "area", "sentry.sdk.name", "sentry.sdk.version", "sentry.timestamp.sequence"],
                item.GetProperty("attributes").EnumerateObject().Select(a => a.Name));
        }

        Assert.Equal(["dashynms.app_start", "dashynms.screen_load.duration"], items.Select(i => i.GetProperty("name").GetString()));
        var load = items[1];
        Assert.Equal("millisecond", load.GetProperty("unit").GetString());
        Assert.Equal("failed", load.GetProperty("attributes").GetProperty("outcome").GetProperty("value").GetString());
    }

    [Fact]
    public void Only_the_listed_pages_are_measured()
    {
        OnScreen();
        Measurements.PageLoaded(_metrics, new SettingsProbe(), new FirstLoad(TimeSpan.FromMilliseconds(5), LoadOutcome.Loaded));
        _metrics.Flush();

        Assert.DoesNotContain(Queued(), p => p.Observation.Name == MetricName.ScreenLoadDuration);
        Assert.Equal(MetricArea.Neighbours, MetricRules.PageArea("NeighboursViewModel"));
        Assert.Null(MetricRules.PageArea("SettingsViewModel"));
    }

    [Fact]
    public void No_dimension_outside_the_fixed_lists_can_be_kept()
    {
        Assert.False(MetricRules.IsValid(new MetricObservation((MetricName)99, 1)));
        Assert.False(MetricRules.IsValid(new MetricObservation(MetricName.ApiDuration, 1, (MetricOutcome)99)));
        Assert.False(MetricRules.IsValid(new MetricObservation(MetricName.ApiDuration, 1, Area: (MetricArea)99)));
        Assert.False(MetricRules.IsValid(new MetricObservation(MetricName.ApiDuration, double.NaN)));
        Assert.False(MetricRules.IsValid(new MetricObservation(MetricName.ApiRequests, 1.5)));
        Assert.All(Enum.GetValues<MetricName>(), name => Assert.StartsWith("dashynms.", SentryTransport.MetricKey(name)));
    }

    [Fact]
    public void A_failing_measurement_never_reaches_the_request()
    {
        using var transport = new LibreNmsTransport(Microsoft.Extensions.Logging.Abstractions.NullLogger<LibreNmsTransport>.Instance);
        Measurements.Observe(transport, new ThrowingMetrics());

        transport.RequestObserver!(new ApiRequestMeasurement(ApiOperation.Other, TimeSpan.FromSeconds(1), ApiRequestOutcome.Success, ApiResponseClass.Success));
    }

    [Fact]
    public void Start_up_is_timed_to_the_first_usable_page_and_sampled()
    {
        OnScreen();
        var startup = new StartupTiming(_time, sample: () => true);
        _time.Advance(TimeSpan.FromMilliseconds(1800));
        startup.WindowShown();
        _time.Advance(TimeSpan.FromMilliseconds(700));
        startup.Ready(_metrics);
        startup.Ready(_metrics); // a second page
        _metrics.Flush();

        var point = Assert.Single(Queued(), p => p.Observation.Name == MetricName.StartupDuration);
        Assert.Equal(2500, point.Observation.Value);

        var unsampled = new StartupTiming(_time, sample: () => false);
        unsampled.WindowShown();
        unsampled.Ready(_metrics);
        _metrics.Flush();
        Assert.Single(Queued(), p => p.Observation.Name == MetricName.StartupDuration);
    }

    [Fact]
    public void A_process_started_in_the_background_and_opened_later_isnt_timed()
    {
        OnScreen();
        var startup = new StartupTiming(_time, sample: () => true);
        _time.Advance(TimeSpan.FromMinutes(3)); // an alert check's launch, opened later
        startup.WindowShown();
        startup.Ready(_metrics);
        _metrics.Flush();

        Assert.DoesNotContain(Queued(), p => p.Observation.Name == MetricName.StartupDuration);
    }

    /// <summary>Named as the page is, which is all the measurement looks at.</summary>
    private sealed class NetworkMapViewModel;

    private sealed class SettingsProbe;

    private sealed class ThrowingMetrics : IAppMetrics
    {
        public bool IsEnabled => true;

        public void Record(MetricObservation observation) => throw new InvalidOperationException();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(Uri Uri, string Body)> Requests { get; } = [];

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!, await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(Status);
        }
    }
}
