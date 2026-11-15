using System.Text.Json;
using DashyNMS.Agent.Health;
using DashyNMS.Agent.Rankings;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

public sealed class SharedFleetRankingsHttpTests
{
    private static Dictionary<string, Answer> Routes()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "FleetRankings", "routes.json")));
        return json.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => new Answer(200, p.Value.GetRawText()));
    }
    private sealed class Mobile : IDisposable
    {
        private readonly LibreNmsTransport transport = new(NullLogger<LibreNmsTransport>.Instance);
        private readonly IntelligenceDeviceTransports transports;
        private readonly IntelligenceLookupSet set;
        internal Mobile(Uri root, AppSettings? saved = null)
        {
            var session = Fakes.Session();
            session.Connection.Returns(new LibreNmsConnection(root, FixtureServer.Token));
            var settings = Fakes.Settings(saved);
            transports = new(transport, settings, session);
            set = new CompactNetworkLookups(Fakes.Client(), settings, session, deviceTransport: transports.Open).Open(Evidence.AddRange);
        }
        internal List<IntelligenceEvidence> Evidence { get; } = [];
        internal IntelligenceLookup Tool(string name) => set.Lookups.Single(l => l.Name == name);
        public void Dispose() { set.Dispose(); transports.Dispose(); transport.Dispose(); }
    }
    [Theory]
    [InlineData("top_interfaces", "total", 101)]
    [InlineData("top_errors", "total", 108)]
    [InlineData("top_devices", "traffic", 1)]
    [InlineData("top_devices", "out", 2)]
    public async Task Rankings_keep_the_shared_order_and_exact_reads(string tool, string by, int firstId)
    {
        var routes = Routes();
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var packageServer = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(packageServer.Root, FixtureServer.Token));
        using var host = HostFleetRankings.Create(client, TimeProvider.System);
        var text = await mobile.Tool(tool).Run(JsonSerializer.Serialize(new { by }), default);
        if (tool == "top_devices")
        {
            var result = (await host.ReadDevicesAsync(new(by))).Result!;
            Assert.Equal(firstId, result.Data.Devices[0].Device!.DeviceId);
            Assert.Equal("device:" + firstId, mobile.Evidence[0].Reference.Key);
        }
        else
        {
            var result = (await host.ReadPortsAsync(new(tool == "top_errors", by))).Result!;
            Assert.Equal(firstId, result.Data.Ports[0].PortId);
            Assert.Contains("port " + firstId, text);
        }
        Assert.Equal(new[] { "devices", "ports" }.Order(), server.Requests.Order());
        Assert.Equal(packageServer.Requests.Order(), server.Requests.Order());
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
        Assert.All(mobile.Evidence, e => Assert.Contains("[[" + e.Reference.Key + "]]", text));
    }
    [Fact]
    public async Task Group_filters_and_limits_are_owned_by_the_shared_ranker()
    {
        var routes = Routes();
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Tool("top_interfaces").Run("""{"group":"Core switches","limit":1}""", default);
        Assert.Contains("port 101", text);
        Assert.Single(mobile.Evidence);
        Assert.Equal(new[] { "devices", "ports", "devicegroups", "devicegroups/1" }.Order(), server.Requests.Order());
    }
    [Fact]
    public async Task Health_overview_uses_the_same_default_assessments_and_keeps_sensor_device_evidence()
    {
        var routes = Routes();
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var packageServer = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(packageServer.Root, FixtureServer.Token));
        using var host = HostHealthOverview.Create(client, transport, TimeProvider.System, new());
        var result = (await host.ReadAsync(new(Limit: 20))).Result!;
        var text = await mobile.Tool("health_overview").Run("{}", default);
        Assert.Equal(9, result.Totals.Readings);
        Assert.Equal(3, result.Totals.Critical);
        Assert.Equal(0, result.Totals.Warning);
        Assert.Contains("Sensors checked 9; critical 3; warning 0", text);
        Assert.Contains(mobile.Evidence, e => e.Reference.Object?.Kind == "sensor");
        Assert.All(mobile.Evidence.Where(e => e.Reference.Object?.Kind == "sensor"), e => Assert.True(e.Reference.Object!.DeviceId > 0));
        Assert.Equal(new[] { "devices", "resources/sensors" }.Order(), server.Requests.Order());
        Assert.Equal(packageServer.Requests.Order(), server.Requests.Order());
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
    }
    [Theory]
    [InlineData("top_interfaces", "ports")]
    [InlineData("health_overview", "resources/sensors")]
    public async Task Denied_required_fleet_reads_are_refused_not_healthy_empty_results(string tool, string path)
    {
        var routes = Routes();
        routes[path] = new(403, """{"message":"password=synthetic-secret"}""");
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        Assert.Equal(CompactNetworkLookups.Refused, await mobile.Tool(tool).Run("{}", default));
        Assert.Empty(mobile.Evidence);
        if (tool == "health_overview") Assert.DoesNotContain(server.Requests, p => p.StartsWith("devices/", StringComparison.Ordinal));
    }
    [Fact]
    public async Task Health_thresholds_are_captured_for_the_attempt_and_updated_on_the_next_question()
    {
        var routes = Routes();
        routes["resources/sensors"] = new(200, """{"sensors":[{"sensor_id":31,"device_id":1,"sensor_class":"temperature","sensor_descr":"Lab inlet","sensor_current":50,"sensor_limit":90,"sensor_limit_warn":80}]}""");
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        var saved = new AppSettings { OverrideSensorLimitsWithAppThresholds = true,
            TemperatureThresholds = new() { LowCritical = -10, LowWarning = 0, HighWarning = 30, HighCritical = 40 } };
        using var first = new Mobile(server.Root, saved);
        saved.TemperatureThresholds.HighWarning = 70;
        saved.TemperatureThresholds.HighCritical = 80;
        var text = await first.Tool("health_overview").Run("{}", default);
        Assert.Contains("Sensors checked 1; critical 1; warning 0", text);
        Assert.Equal("critical", Assert.Single(first.Evidence, e => e.Reference.Key == "sensor:31").Reference.Tone);
        using var next = new Mobile(server.Root, saved);
        text = await next.Tool("health_overview").Run("""{"type":"temperature","only_problems":false}""", default);
        Assert.Contains("Sensors checked 1; critical 0; warning 0; OK 1", text);
        Assert.Contains("; OK", text);
    }

    [Fact]
    public async Task Denied_scoped_fallback_sensors_are_named_and_excluded_from_counts()
    {
        var routes = Routes();
        var denied = new Answer(403, """{"message":"password=synthetic-secret"}""");
        routes["resources/sensors"] = denied;
        routes["devices/1/health"] = denied;
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Tool("health_overview").Run("""{"group":"Core switches"}""", default);
        Assert.StartsWith("Unavailable sensors on 1 device; excluded from counts.", text);
        Assert.Contains("Sensors checked 0", text);
        Assert.DoesNotContain("synthetic-secret", text);
        Assert.Empty(mobile.Evidence);
        Assert.Contains("devices/1/health", server.Requests);
        Assert.DoesNotContain("devices/2/health", server.Requests);
    }

    [Theory]
    [InlineData("top_interfaces")]
    [InlineData("health_overview")]
    public async Task Pending_fleet_reads_obey_deadline_cancellation_and_retirement(string name)
    {
        var session = Fakes.Session();
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token"));
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var evidence = new List<IntelligenceEvidence>();
        using var set = new CompactNetworkLookups(Fakes.Client(), Fakes.Settings(), session, time,
            deviceTransport: _ => new PendingTransport()).Open(evidence.AddRange);
        var lookup = set.Lookups.Single(l => l.Name == name);
        var timed = lookup.Run("{}", default);
        time.Advance(CompactNetworkLookups.Timeout);
        Assert.Equal(CompactNetworkLookups.Failed, await timed);
        using var stopping = new CancellationTokenSource();
        var cancelled = lookup.Run("{}", stopping.Token);
        stopping.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        var retired = lookup.Run("{}", default);
        session.Connection.Returns((LibreNmsConnection?)null);
        session.StateChanged += Raise.Event();
        Assert.Equal(CompactNetworkLookups.Changed, await retired.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Empty(evidence);
    }
    private sealed class PendingTransport : ILibreNmsTransport
    {
        public LibreNmsConnection? Connection => null;
        public Task<IReadOnlyList<T>> GetCollectionAsync<T>(string relativeUrl, string collectionProperty, CancellationToken cancellationToken = default)
            => new TaskCompletionSource<IReadOnlyList<T>>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        public Task<JsonDocument> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<string> SendRawAsync(string relativeUrl, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    [Fact]
    public async Task Fleet_profiles_refuse_other_reads_and_writes_before_transport()
    {
        var opened = false;
        var scope = new SessionScope(0, new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token"));
        using var rank = new ConnectionBoundClient(scope, _ => { opened = true; throw new InvalidOperationException(); }, fleetRankings: true);
        using var health = new ConnectionBoundClient(scope, _ => { opened = true; throw new InvalidOperationException(); }, healthOverview: true);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => rank.Sensors);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => rank.SensorTransport);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => health.Ports);
        foreach (var client in new[] { rank, health })
        {
            Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Alerts);
            Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Arp);
            Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Routing);
            await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.GetAsync("1"));
            await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.IsUnderMaintenanceAsync(1));
            await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.DeviceGroups.DeleteAsync("Example"));
        }
        Assert.False(opened);
    }
}
