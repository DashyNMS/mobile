using System.Net;
using System.Text;
using System.Text.Json;
using DashyNMS.Mobile.Diagnostics;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

/// <summary>Opt-in error reports (#217), as desktop's (DashyNMS/desktop#62): sanitised, queued, sent only when allowed.</summary>
public sealed class ErrorReportTests : IDisposable
{
    private const string Dsn = "https://0123456789abcdef@o1.ingest.example.net/42";

    private static readonly ErrorReportContext Context = new("2.0.0+412", "ios", "26.1", "arm64");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dashynms-errors-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero));
    private readonly InMemoryPreferences _preferences = new();
    private readonly RecordingHandler _handler = new();

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>A real exception thrown inside DashyNMS.Mobile.Shared, so it has one of our frames.</summary>
    private static Exception Thrown(string message = "Lost https://nms.example.net/ for core-sw1 with token abc123")
    {
        try
        {
            ErrorPayload.Create(null!, ErrorOrigin.Caught, Context, DateTimeOffset.UnixEpoch);
        }
        catch (ArgumentNullException inner)
        {
            return new InvalidOperationException(message, inner);
        }

        throw new InvalidOperationException("Expected a throw");
    }

    private (DiagnosticsReporter Reporter, DiagnosticsConsent Consent) NewReporter(bool errors = true, bool started = true, string? dsn = Dsn)
    {
        var transport = new SentryTransport(dsn, _handler);
        var consent = new DiagnosticsConsent(_preferences, transport.IsAvailable);
        if (!consent.HasChosen)
        {
            consent.Choose(errors, usage: false);
        }

        var reporter = new DiagnosticsReporter(Context, transport, consent, _directory, _time) { SendsInBackground = false };
        if (started)
        {
            reporter.Start();
        }

        return (reporter, consent);
    }

    [Fact]
    public void A_report_has_the_types_and_our_frames_and_none_of_the_message()
    {
        var report = ErrorPayload.Create(Thrown(), ErrorOrigin.Caught, Context, _time.GetUtcNow());
        var json = Encoding.UTF8.GetString(SentryTransport.Event(report));

        Assert.True(ErrorPayload.IsValid(report));
        Assert.Equal(["System.InvalidOperationException", "System.ArgumentNullException"], report.Exceptions.Select(e => e.Type));
        var frame = Assert.Single(report.Exceptions[1].Frames);
        Assert.Equal(("DashyNMS.Mobile.Shared", "DashyNMS.Mobile.Diagnostics.ErrorPayload", "Create"), (frame.Module, frame.Type, frame.Function));
        Assert.Equal("src/DashyNMS.Mobile.Shared/Diagnostics/ErrorPayload.cs", frame.FileName);
        Assert.True(frame.LineNumber > 0);

        foreach (var excluded in new[] { "nms.example.net", "core-sw1", "abc123", "Lost", Environment.UserName, "/home/", "Tests" })
        {
            Assert.DoesNotContain(excluded, json, StringComparison.OrdinalIgnoreCase);
        }

        using var parsed = JsonDocument.Parse(json);
        Assert.Equal("warning", parsed.RootElement.GetProperty("level").GetString());
        Assert.Equal("2.0.0+412", parsed.RootElement.GetProperty("release").GetString());
        Assert.Equal("ios", parsed.RootElement.GetProperty("tags").GetProperty("operating_system").GetString());
        Assert.Equal("26.1", parsed.RootElement.GetProperty("tags").GetProperty("os_version").GetString());
        Assert.Equal(["event_id", "timestamp", "platform", "level", "release", "tags", "exception"],
            parsed.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void Anything_outside_the_fixed_lists_is_unknown()
    {
        var context = ErrorPayload.SafeContext(new ErrorReportContext("Tom's build", "iPhone of Tom", "26.1.2 (23B85)", "riscv"));

        Assert.Equal(new ErrorReportContext("unknown", "unknown", "unknown", "unknown"), context);
        Assert.Equal("26.1", ErrorPayload.OsVersionBand(new Version(26, 1, 2)));
        Assert.Equal("15", ErrorPayload.OsVersionBand(new Version(15, 0)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Expected_states_are_never_reported_as_caught(bool wrapped)
    {
        Exception[] expected =
        [
            new HttpRequestException("offline"),
            new TaskCanceledException(),
            new TimeoutException(),
            new LibreNmsApiException("Unauthorised", HttpStatusCode.Unauthorized),
        ];

        foreach (var ex in expected)
        {
            Assert.True(ErrorPayload.IsExpected(wrapped ? new InvalidOperationException("x", ex) : ex));
        }

        Assert.False(ErrorPayload.IsExpected(Thrown()));
    }

    [Fact]
    public async Task Nothing_is_captured_or_sent_before_a_choice()
    {
        var transport = new SentryTransport(Dsn, _handler);
        var consent = new DiagnosticsConsent(_preferences, transport.IsAvailable);
        var reporter = new DiagnosticsReporter(Context, transport, consent, _directory, _time) { SendsInBackground = false };
        reporter.Start();

        reporter.Capture(Thrown(), ErrorOrigin.UnobservedTask);
        await reporter.SendPendingAsync();

        Assert.Empty(reporter.Queued);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task Nothing_is_sent_when_only_usage_is_shared()
    {
        var (reporter, _) = NewReporter(errors: false);

        reporter.Capture(Thrown(), ErrorOrigin.UnobservedTask);
        await reporter.SendPendingAsync();

        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task A_build_without_a_key_never_sends()
    {
        var (reporter, consent) = NewReporter(dsn: null);

        reporter.Capture(Thrown(), ErrorOrigin.UnobservedTask);
        await reporter.SendPendingAsync();

        Assert.False(consent.IsAvailable);
        Assert.Empty(reporter.Queued);
        Assert.Empty(_handler.Requests);
        Assert.False(new SentryTransport("http://key@o1.ingest.example.net/42").IsAvailable);
        Assert.False(new SentryTransport("https://key:secret@o1.ingest.example.net/42").IsAvailable);
    }

    [Fact]
    public async Task A_caught_error_is_sent_as_an_envelope_and_leaves_the_queue()
    {
        var (reporter, _) = NewReporter();

        reporter.Capture(Thrown(), ErrorOrigin.Caught);
        Assert.Single(reporter.Queued);
        await reporter.SendPendingAsync();

        var request = Assert.Single(_handler.Requests);
        Assert.Equal("https://o1.ingest.example.net/api/42/envelope/", request.Uri.ToString());
        Assert.Equal("Sentry sentry_version=7,sentry_key=0123456789abcdef", request.Auth);
        var lines = request.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Contains("\"type\":\"event\"", lines[1]);
        Assert.Empty(reporter.Queued);
    }

    [Fact]
    public async Task An_expected_state_or_a_background_launch_is_never_queued()
    {
        var (reporter, _) = NewReporter(started: false);
        reporter.Capture(Thrown(), ErrorOrigin.Unhandled); // a background wake: never Started

        reporter.Start();
        reporter.Capture(new HttpRequestException("offline"), ErrorOrigin.Caught);
        await reporter.SendPendingAsync();

        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task A_fatal_error_is_kept_and_sent_at_the_next_launch()
    {
        var (crashing, _) = NewReporter();
        crashing.Capture(Thrown(), ErrorOrigin.Unhandled);
        Assert.Empty(_handler.Requests);

        var (next, _) = NewReporter(started: false);
        Assert.Single(next.Queued);
        next.Start();
        await next.SendPendingAsync();

        using var parsed = JsonDocument.Parse(Assert.Single(_handler.Requests).Body.Split('\n')[2]);
        Assert.Equal("fatal", parsed.RootElement.GetProperty("level").GetString());
    }

    [Fact]
    public void The_queue_holds_ten_drops_the_oldest_and_forgets_after_seven_days()
    {
        _handler.Status = HttpStatusCode.ServiceUnavailable;
        var (reporter, _) = NewReporter(started: false);
        reporter.Start();
        for (var i = 0; i < 12; i++)
        {
            _time.Advance(TimeSpan.FromMinutes(1));
            reporter.Capture(Thrown(), ErrorOrigin.Unhandled);
        }

        Assert.Equal(DiagnosticsReporter.MaximumQueued, reporter.Queued.Count);
        Assert.Equal(_time.GetUtcNow() - TimeSpan.FromMinutes(9), reporter.Queued[0].Timestamp);

        _time.Advance(DiagnosticsReporter.MaximumAge + TimeSpan.FromMinutes(1));
        Assert.Empty(reporter.Queued);
    }

    [Fact]
    public void A_tampered_report_on_disk_is_deleted_not_sent()
    {
        var (reporter, _) = NewReporter();
        reporter.Capture(Thrown(), ErrorOrigin.Unhandled);
        var path = Directory.GetFiles(_directory, "*.json").Single();
        File.WriteAllText(path, File.ReadAllText(path).Replace("System.InvalidOperationException", "Lost nms.example.net"));

        Assert.Empty(reporter.Queued);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Turning_error_reports_off_deletes_the_queue()
    {
        _handler.Status = HttpStatusCode.ServiceUnavailable;
        var (reporter, consent) = NewReporter();
        reporter.Capture(Thrown(), ErrorOrigin.Unhandled);
        reporter.Capture(Thrown(), ErrorOrigin.Unhandled);
        Assert.Equal(2, reporter.Queued.Count);

        consent.SetShareErrors(false);

        Assert.Empty(Directory.GetFiles(_directory));
        _handler.Status = HttpStatusCode.OK;
        await reporter.SendPendingAsync();
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task A_rate_limit_holds_the_queue_until_Sentry_says()
    {
        _handler.Status = HttpStatusCode.TooManyRequests;
        _handler.RateLimit = "120:error:organization";
        var (reporter, _) = NewReporter();
        reporter.Capture(Thrown(), ErrorOrigin.Unhandled);

        await reporter.SendPendingAsync();
        await reporter.SendPendingAsync(); // still waiting
        Assert.Single(_handler.Requests);

        _handler.Status = HttpStatusCode.OK;
        _time.Advance(TimeSpan.FromMinutes(2));
        await reporter.SendPendingAsync();
        Assert.Equal(2, _handler.Requests.Count);
        Assert.Empty(reporter.Queued);
    }

    [Fact]
    public async Task A_rejected_report_is_dropped_rather_than_retried()
    {
        _handler.Status = HttpStatusCode.BadRequest;
        var (reporter, _) = NewReporter();
        reporter.Capture(Thrown(), ErrorOrigin.Unhandled);

        await reporter.SendPendingAsync();

        Assert.Empty(reporter.Queued);
    }

    [Fact]
    public void The_same_exception_is_reported_once()
    {
        _handler.Status = HttpStatusCode.ServiceUnavailable;
        var (reporter, _) = NewReporter();
        var ex = Thrown();

        // .NET's handler and iOS's or Android's can both see one crash.
        reporter.Capture(ex, ErrorOrigin.Unhandled);
        reporter.Capture(ex, ErrorOrigin.Unhandled);

        Assert.Single(reporter.Queued);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(Uri Uri, string? Auth, string Body)> Requests { get; } = [];

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public string? RateLimit { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!, request.Headers.TryGetValues("X-Sentry-Auth", out var auth) ? auth.Single() : null,
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            var response = new HttpResponseMessage(Status);
            if (RateLimit is not null)
            {
                response.Headers.TryAddWithoutValidation("X-Sentry-Rate-Limits", RateLimit);
            }

            return response;
        }
    }
}
