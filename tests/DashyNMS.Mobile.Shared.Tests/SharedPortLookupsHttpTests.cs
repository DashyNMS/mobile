using System.Text.Json;
using DashyNMS.Agent.Ports;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class SharedPortLookupsHttpTests
{
    private static Dictionary<string, Answer> Routes()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "PortLookups", "routes.json")));
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
            set = new CompactNetworkLookups(Fakes.Client(), settings, session, deviceTransport: transports.Open).Open(Evidence.AddRange);
        }
        internal List<IntelligenceEvidence> Evidence { get; } = [];
        internal IntelligenceLookup Tool(string name) => set.Lookups.Single(l => l.Name == name);
        public void Dispose() { set.Dispose(); transports.Dispose(); transport.Dispose(); }
    }
    [Theory]
    [InlineData("port_status", "{\"device\":\"1\",\"port\":\"1/1/1\"}")]
    [InlineData("device_neighbours", "{\"device\":\"1\"}")]
    [InlineData("device_neighbours", "{\"device\":\"2\"}")]
    [InlineData("trace_endpoint", "{\"address\":\"aa:bb:cc:00:00:05\"}")]
    public async Task Port_neighbour_and_trace_facts_match_the_shared_operation(string tool, string arguments)
    {
        var routes = Routes();
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var packageServer = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(packageServer.Root, FixtureServer.Token));
        using var host = HostPortLookups.Create(client, transport, TimeProvider.System, new());
        var text = await mobile.Tool(tool).Run(arguments, default);
        if (tool == "port_status")
        {
            var result = (await host.ReadStatusAsync(new(1, "1/1/1"))).Result!;
            Assert.Equal(101, result.Data.Port.PortId);
            Assert.Equal(10000, result.Data.Port.SpeedMbps);
            Assert.Equal(100, result.Data.Port.InMbps);
            Assert.Contains("Port 101 1/1/1", text);
            Assert.Contains("speed 10000 Mbps", text);
            Assert.Contains("Traffic in 100 Mbps", text);
        }
        else if (tool == "device_neighbours")
        {
            var id = arguments.Contains("2", StringComparison.Ordinal) ? 2 : 1;
            var result = (await host.ReadNeighboursAsync(new(id))).Result!;
            Assert.Equal(id == 1 ? 2 : 1, result.Data.Neighbours.Count);
            if (id == 2) Assert.Contains("seen from neighbour", text);
            else
            {
                Assert.Contains("[[device:2]]", text);
                Assert.Contains("Camera", text, StringComparison.OrdinalIgnoreCase);
                // The fixture's unmonitored camera has no invented device reference.
                Assert.Equal(new[] { "device:1", "device:2" }, mobile.Evidence.Select(e => e.Reference.Key));
            }
        }
        else
        {
            var result = (await host.TraceAsync(new("aa:bb:cc:00:00:05"))).Result!;
            Assert.Equal(107, result.Data.Edge!.PortId);
            Assert.Equal(1, result.Data.Edge.DeviceId);
            Assert.Equal(1, result.Data.Edge.MacsOnPort);
            Assert.False(result.Data.Ambiguous);
            Assert.Contains("Stored ARP/FDB observations, not proof of ownership", text);
            Assert.Contains("Likely edge:", text);
            Assert.Contains("port 107", text);
            Assert.Contains("MACs 1", text);
            Assert.Contains("ambiguous no", text);
        }
        var expected = tool == "trace_endpoint" ? packageServer.Requests : packageServer.Requests.Concat(["devices"]);
        Assert.Equal(expected.Order(), server.Requests.Order());
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
        Assert.All(mobile.Evidence, e => Assert.Contains("[[" + e.Reference.Key + "]]", text));
    }
    [Theory]
    [InlineData("port_status", "devices/1/links", "neighbours", "{\"device\":\"1\",\"port\":\"1/1/1\"}")]
    [InlineData("device_neighbours", "devices/1/ports", "ports", "{\"device\":\"1\"}")]
    [InlineData("trace_endpoint", "resources/links", "links", "{\"address\":\"aa:bb:cc:00:00:05\"}")]
    public async Task Optional_source_failures_lead_the_result(string tool, string path, string source, string arguments)
    {
        var routes = Routes();
        routes[path] = new(403, """{"message":"password=synthetic-secret"}""");
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Tool(tool).Run(arguments, default);
        Assert.StartsWith("Unavailable: " + source, text);
        Assert.DoesNotContain("synthetic-secret", text);
    }
    [Fact]
    public async Task Invalid_and_ambiguous_inputs_never_invent_a_port_or_device()
    {
        var routes = Routes();
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Tool("port_status").Run("""{"device":"1","port":"1/1"}""", default);
        Assert.StartsWith("The port or address is invalid, absent or ambiguous.", text);
        Assert.Empty(mobile.Evidence);
        var count = server.Requests.Length;
        Assert.StartsWith("The port or address is invalid", await mobile.Tool("trace_endpoint").Run("example.net", default));
        Assert.Equal(count, server.Requests.Length);
        Assert.Empty(mobile.Evidence);
    }
    [Fact]
    public void Many_failed_device_sources_stay_bounded_and_single_trace_caps_are_counted_once()
    {
        var failures = Enumerable.Range(1, 25).SelectMany(id => new[]
        {
            new DashyNMS.Agent.Unavailable($"ports on device {id}", "This source could not be read."),
            new DashyNMS.Agent.Unavailable($"MAC table on device {id}", "This source could not be read."),
        }).ToList();
        var branch = new TraceEndpoint.MacTrace("aa:bb:cc:00:00:05", false, false, "No edge established.", null, [], [], [], 5, failures, "No edge established.");
        var result = new TraceEndpointResult("No edge established.", new() { Mac = branch.Mac, Found = false, Unavailable = failures }, 5, [branch]);
        var reply = CompactNetworkLookups.TraceRows(result, DateTimeOffset.UtcNow, TimeProvider.System);
        Assert.InRange(reply.Text.Length, 1, CompactNetworkLookups.MaxCharacters);
        Assert.StartsWith("Unavailable: ports on devices (25), MAC tables on devices (25).", reply.Text);
        Assert.Contains("5 more results omitted.", reply.Text);
        Assert.DoesNotContain("10 more results omitted", reply.Text);
        Assert.Empty(reply.References);
    }

    [Theory]
    [InlineData("port_status", "{\"device\":\"1\",\"port\":\"1/1/1\"}")]
    [InlineData("device_neighbours", "{\"device\":\"1\"}")]
    [InlineData("trace_endpoint", "{\"address\":\"aa:bb:cc:00:00:05\"}")]
    public async Task Pending_topology_reads_obey_deadline_cancellation_and_retirement(string name, string arguments)
    {
        var session = Fakes.Session();
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token"));
        var time = new FakeTimeProvider();
        var evidence = new List<IntelligenceEvidence>();
        using var set = new CompactNetworkLookups(Fakes.Client(), Fakes.Settings(), session, time,
            deviceTransport: _ => new PendingTransport()).Open(evidence.AddRange);
        var lookup = set.Lookups.Single(l => l.Name == name);
        var timed = lookup.Run(arguments, default);
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
    private sealed class PendingTransport : ILibreNmsTransport
    {
        public LibreNmsConnection? Connection => null;
        public Task<IReadOnlyList<T>> GetCollectionAsync<T>(string relativeUrl, string collectionProperty, CancellationToken cancellationToken = default)
            => new TaskCompletionSource<IReadOnlyList<T>>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        public Task<JsonDocument> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException();
        public Task<string> SendRawAsync(string relativeUrl, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    [Fact]
    public async Task Topology_profile_rejects_other_reads_writes_and_raw_path_escapes_before_transport()
    {
        var opened = false;
        using var client = new ConnectionBoundClient(new SessionScope(0, new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token")),
            _ => { opened = true; throw new InvalidOperationException(); }, portLookups: true);
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Ports.ListAllStatusAsync());
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Ports.ListForDeviceAsync(0));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Links.ListForDeviceAsync(-1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Fdb.ListForDeviceAsync(0));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.IsUnderMaintenanceAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.GetAsync("1"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.DeleteAsync(1));
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Alerts);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Rules);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.DeviceGroups);
        foreach (var path in new[] { "resources/fdb/aabbcc000005?all=1", "resources/fdb/aabbcc000005\n", "resources/ip/arp/../all", "resources/ip/arp/192.0.2.1?all=1", "devices/1/ports" })
            await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.TraceTransport.GetCollectionAsync<FdbEntry>(path, "ports_fdb"));
        foreach (var path in new[] { "resources/ip/arp/../all", "resources/ip/arp/192.0.2.1?all=1", "resources/ip/arp/127.1", "resources/ip/arp/192.0.2.1\n" })
            await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.TraceTransport.GetCollectionAsync<ArpEntry>(path, "arp"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.TraceTransport.GetCollectionAsync<ArpEntry>("resources/ip/arp/192.0.2.1", "wrong"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.TraceTransport.SendAsync(HttpMethod.Post, "resources/fdb/aabbcc000005"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.TraceTransport.SendRawAsync("resources/links"));
        Assert.False(opened);
    }
}
