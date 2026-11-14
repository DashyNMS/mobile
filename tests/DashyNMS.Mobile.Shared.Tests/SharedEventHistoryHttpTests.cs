using System.Text.Json;
using DashyNMS.Agent.Events;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class SharedEventHistoryHttpTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);
    private static Dictionary<string, Answer> Routes()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EventHistory", "routes.json")));
        return json.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => new Answer(200, p.Value.GetRawText()));
    }
    private sealed class Mobile : IDisposable
    {
        private readonly LibreNmsTransport transport = new(NullLogger<LibreNmsTransport>.Instance);
        private readonly IntelligenceDeviceTransports transports;
        private readonly IntelligenceLookupSet set;
        internal Mobile(Uri root)
        {
            var session = Fakes.Session();
            session.Connection.Returns(new LibreNmsConnection(root, FixtureServer.Token));
            var settings = Fakes.Settings();
            transports = new(transport, settings, session);
            set = new CompactNetworkLookups(Fakes.Client(), settings, session, new FakeTimeProvider(Now), deviceTransport: transports.Open).Open(Evidence.AddRange);
        }
        internal List<IntelligenceEvidence> Evidence { get; } = [];
        internal IntelligenceLookup Tool(string name) => set.Lookups.Single(l => l.Name == name);
        public void Dispose() { set.Dispose(); transports.Dispose(); transport.Dispose(); }
    }
    [Theory]
    [InlineData("device_events", "{\"device\":\"1\"}")]
    [InlineData("network_events", "{\"log\":\"both\"}")]
    [InlineData("active_alerts", "{\"hours\":24}")]
    public async Task Historical_facts_and_reads_match_the_shared_operation(string tool, string arguments)
    {
        var routes = Routes();
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var packageServer = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(packageServer.Root, FixtureServer.Token));
        using var unimus = new UnimusApi(NullLogger<UnimusApi>.Instance);
        using var graylog = new GraylogApi(NullLogger<GraylogApi>.Instance);
        using var host = HostEventLookups.Create(client, unimus, graylog, new FakeTimeProvider(Now), new());
        var text = await mobile.Tool(tool).Run(arguments, default);
        if (tool == "device_events")
        {
            var result = (await host.ReadDeviceAsync(new(1, Limit: 20))).Result!;
            Assert.True(result.Data.Window.WindowComplete);
            Assert.Contains(result.Data.Entries, e => e.Source == "outage");
            Assert.Contains("Interface 1/1/7 went down", text);
            Assert.Contains("source outage", text);
        }
        else if (tool == "network_events")
        {
            var result = (await host.ReadNetworkAsync(new(Log: "both", Limit: 20))).Result!;
            Assert.True(result.Data.Window.WindowComplete);
            Assert.Contains(result.Data.Entries, e => e.Source == "eventlog");
            Assert.Contains("source eventlog", text);
            Assert.Contains("source alertlog", text);
        }
        else
        {
            var result = (await host.ReadRaisedAlertsAsync(new(Hours: 24, Limit: 20))).Result!;
            Assert.Equal(2, result.Data.Page.Total);
            Assert.Contains(result.Data.Devices.SelectMany(d => d.Alerts), a => a.RecoveredAt is not null && a.StillOpen == false);
            Assert.Contains(result.Data.Devices.SelectMany(d => d.Alerts), a => a.StillOpen == true);
            Assert.Contains("still open yes", text);
            Assert.Contains("still open no", text);
            Assert.DoesNotContain("[[alert:", text);
        }
        Assert.Contains("; complete.", text);
        var expected = tool == "device_events" ? packageServer.Requests.Concat(["devices"]) : packageServer.Requests;
        Assert.Equal(expected.Order(), server.Requests.Order());
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
        Assert.All(mobile.Evidence, e => Assert.Equal("device", e.Reference.Object!.Kind));
    }
    [Theory]
    [InlineData("logs/eventlog/1", "event log")]
    [InlineData("logs/alertlog/1", "alert log")]
    [InlineData("devices/1/outages", "outages")]
    public async Task A_failed_device_history_source_never_marks_the_window_complete(string path, string source)
    {
        var routes = Routes();
        routes[path] = new(403, """{"message":"password=synthetic-secret"}""");
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Tool("device_events").Run("1", default);
        Assert.StartsWith("Unavailable: " + source, text);
        Assert.Contains("coverage unknown", text);
        Assert.DoesNotContain("; complete.", text);
        Assert.DoesNotContain("synthetic-secret", text);
    }
    [Fact]
    public async Task Failed_severities_make_a_filtered_historical_alert_request_fail()
    {
        var routes = Routes();
        routes["rules"] = new(403, """{"message":"password=synthetic-secret"}""");
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        Assert.Equal(CompactNetworkLookups.Failed, await mobile.Tool("active_alerts").Run("""{"hours":24,"severity":"critical"}""", default));
        Assert.Empty(mobile.Evidence);
    }
    [Fact]
    public async Task Unknown_open_status_and_severity_are_disclosed_in_unfiltered_history()
    {
        var routes = Routes();
        routes["rules"] = new(403, """{"message":"password=synthetic-secret"}""");
        routes["alerts"] = new(403, """{"message":"password=synthetic-secret"}""");
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Tool("active_alerts").Run("""{"hours":24}""", default);
        Assert.StartsWith("Rule severities unavailable. Current open status unavailable.", text);
        Assert.Contains("still open unknown", text);
        Assert.DoesNotContain("still open no", text);
        Assert.DoesNotContain("synthetic-secret", text);
    }
    [Fact]
    public async Task Busy_history_at_the_shared_cap_remains_incomplete()
    {
        var routes = Routes();
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EventHistory", "incomplete.json")));
        var count = fixture.RootElement.GetProperty("events").GetInt32();
        var stamp = fixture.RootElement.GetProperty("timestamp").GetString();
        routes["logs/eventlog/1"] = new(200, JsonSerializer.Serialize(new
        {
            logs = Enumerable.Range(1, count).Select(id => new { event_id = id, device_id = 1, datetime = stamp, message = "Synthetic event", type = "test", severity = 3 }),
        }));
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Tool("device_events").Run("1", default);
        Assert.Contains("; incomplete; covered since", text);
        Assert.DoesNotContain("; complete.", text);
        Assert.Equal(3, server.Requests.Count(p => p == "logs/eventlog/1"));
        Assert.Contains("more results omitted", text);
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
    }

    [Theory]
    [InlineData("device_events", "{\"device\":\"1\"}")]
    [InlineData("network_events", "{}")]
    [InlineData("active_alerts", "{\"hours\":24}")]
    public async Task Pending_history_reads_obey_deadline_cancellation_and_retirement(string name, string arguments)
    {
        var session = Fakes.Session();
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token"));
        var time = new FakeTimeProvider(Now);
        var pending = new PendingHistory();
        var evidence = new List<IntelligenceEvidence>();
        using var set = new CompactNetworkLookups(Fakes.Client(), Fakes.Settings(), session, time, deviceTransport: _ => pending).Open(evidence.AddRange);
        var lookup = set.Lookups.Single(l => l.Name == name);
        var timed = lookup.Run(arguments, default);
        await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        time.Advance(CompactNetworkLookups.Timeout);
        Assert.Equal(CompactNetworkLookups.Failed, await timed);
        using var stopping = new CancellationTokenSource();
        var cancelled = lookup.Run(arguments, stopping.Token);
        stopping.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        var retired = lookup.Run(arguments, default);
        session.Connection.Returns((LibreNmsConnection?)null);
        session.StateChanged += Raise.Event();
        Assert.Equal(CompactNetworkLookups.Changed, await retired.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Empty(evidence);
    }
    private sealed class PendingHistory : ILibreNmsTransport
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LibreNmsConnection? Connection => null;
        public Task<IReadOnlyList<T>> GetCollectionAsync<T>(string relativeUrl, string collectionProperty, CancellationToken cancellationToken = default)
        {
            if (relativeUrl == "devices" && typeof(T) == typeof(Device))
                return Task.FromResult((IReadOnlyList<T>)(object)new[] { Fakes.Device(1, "switch.example.net") });
            Started.TrySetResult();
            return new TaskCompletionSource<IReadOnlyList<T>>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        }
        public Task<JsonDocument> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<string> SendRawAsync(string relativeUrl, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    [Fact]
    public async Task History_profile_rejects_unbounded_reads_and_writes_before_transport()
    {
        var opened = false;
        using var client = new ConnectionBoundClient(new SessionScope(0, new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token")),
            _ => { opened = true; throw new InvalidOperationException(); }, history: true);
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Logs.ListEventLogAsync(null, 5000));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Logs.ListAlertLogAsync(-1, 200));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.GetOutagesAsync(0));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.GetAsync("1"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.IsUnderMaintenanceAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Alerts.ListAsync(AlertQuery.All));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Alerts.AcknowledgeAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Rules.DeleteAsync(1));
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Ports);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Sensors);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Arp);
        Assert.False(opened);
    }
}
