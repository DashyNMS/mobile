using System.Text.Json;
using DashyNMS.Agent.Devices;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

public sealed class SharedDeviceDetailsHttpTests
{
    private static Dictionary<string, Answer> Routes()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "DeviceDetails", "routes.json")));
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
            Details = set.Lookups.Single(l => l.Name == "device_details");
        }
        internal List<IntelligenceEvidence> Evidence { get; } = [];
        internal IntelligenceLookup Details { get; }
        public void Dispose() { set.Dispose(); transports.Dispose(); transport.Dispose(); }
    }
    [Theory]
    [InlineData("inventory", "SG00TEST01")]
    [InlineData("vlans", "VLAN 120")]
    [InlineData("fdb", "aa:bb:cc:dd:01:07")]
    [InlineData("arp", "ARP")]
    [InlineData("poe", "used 120.5 W; total 370 W")]
    [InlineData("wireless", "wireless 0")]
    [InlineData("routing", "state idle")]
    [InlineData("availability", "99.941%")]
    [InlineData("addresses", "192.0.2.1/24")]
    public async Task Each_section_uses_the_same_shared_HTTP_facts_and_reads(string section, string expected)
    {
        var routes = Routes();
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var packageServer = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(packageServer.Root, FixtureServer.Token));
        using var host = HostDeviceDetails.Create(client, transport, TimeProvider.System);
        var result = (await host.ReadAsync(new(1, [section], Limit: 20))).Result!;
        var text = await mobile.Details.Run(JsonSerializer.Serialize(new { device = "1", sections = new[] { section } }), default);
        Assert.Contains(section, result.ReturnedSections);
        Assert.Contains(expected, text);
        Assert.Contains("[[device:1]]", text);
        Assert.Equal(packageServer.Requests.Concat(["devices"]).Order(), server.Requests.Order());
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
        Assert.Equal("device:1", Assert.Single(mobile.Evidence).Reference.Key);
    }
    [Fact]
    public async Task Failed_vlan_read_is_unknown_instead_of_a_successful_empty_section()
    {
        var routes = Routes();
        routes["resources/vlans"] = new(403, """{"message":"password=synthetic-secret"}""");
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Details.Run("""{"device":"1","sections":["vlans"]}""", default);
        Assert.StartsWith("Unavailable: vlans.", text);
        Assert.DoesNotContain("vlans 0", text);
        Assert.DoesNotContain("synthetic-secret", text);
    }
    [Fact]
    public async Task Paging_counts_unfetched_rows_and_does_not_fetch_other_sections()
    {
        var routes = Routes();
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Details.Run("""{"device":"1","sections":["inventory"],"limit":1}""", default);
        Assert.Contains("inventory 2", text);
        Assert.Contains("next offset 1", text);
        Assert.Contains("1 more results omitted.", text);
        Assert.Equal(new[] { "devices", "devices", "inventory/1/all" }.Order(), server.Requests.Order());
    }
    [Fact]
    public async Task Filled_wireless_rows_keep_class_description_and_value()
    {
        var routes = Routes();
        routes["devices/1/wireless-sensors"] = new(200, """{"wireless_sensors":[{"sensor_id":17,"device_id":1,"sensor_class":"clients","sensor_descr":"Lab radio","sensor_current":12}]}""");
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Details.Run("""{"device":"1","sections":["wireless"]}""", default);
        Assert.Contains("wireless 1", text);
        Assert.Contains("Wireless clients Lab radio; value 12", text);
        Assert.Contains("[[device:1]]", text);
    }

    [Fact]
    public async Task Page_metadata_precedes_detail_rows_when_the_page_exceeds_the_text_budget()
    {
        var routes = Routes();
        routes["inventory/1/all"] = new(200, JsonSerializer.Serialize(new
        {
            inventory = Enumerable.Range(1, 30).Select(id => new
            {
                entPhysical_id = id, entPhysicalIndex = id, entPhysicalClass = "chassis", entPhysicalName = "Module " + id,
                entPhysicalModelName = new string('x', 40), entPhysicalSerialNum = "TEST" + id,
            }),
        }));
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Details.Run("""{"device":"1","sections":["inventory"],"limit":20}""", default);
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
        Assert.Contains("inventory page: offset 0; limit 20; returned 20; total 30; next offset 20.", text);
        Assert.Contains("more results omitted", text);
        Assert.True(text.IndexOf("inventory page:", StringComparison.Ordinal) < text.IndexOf("Inventory Module", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invalid_section_and_negative_offset_are_rejected_before_transport()
    {
        using var server = new FixtureServer(_ => throw new InvalidOperationException("No reads expected"));
        using var mobile = new Mobile(server.Root);
        Assert.StartsWith("The lookup wasn't run:", await mobile.Details.Run("""{"device":"1","sections":["secrets"]}""", default));
        Assert.StartsWith("The lookup wasn't run:", await mobile.Details.Run("""{"device":"1","offset":-1}""", default));
        Assert.Empty(server.Requests);
        Assert.Empty(mobile.Evidence);
    }
    [Fact]
    public async Task Pending_inventory_reads_obey_deadline_caller_cancellation_and_retirement()
    {
        var session = Fakes.Session();
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token"));
        var time = new FakeTimeProvider();
        var transport = new PendingInventory();
        var evidence = new List<IntelligenceEvidence>();
        using var set = new CompactNetworkLookups(Fakes.Client(), Fakes.Settings(), session, time, deviceTransport: _ => transport).Open(evidence.AddRange);
        var lookup = set.Lookups.Single(l => l.Name == "device_details");
        const string input = "{\"device\":\"1\",\"sections\":[\"inventory\"]}";
        var timed = lookup.Run(input, default);
        await transport.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        time.Advance(CompactNetworkLookups.Timeout);
        Assert.Equal(CompactNetworkLookups.Failed, await timed);
        using var stopping = new CancellationTokenSource();
        var cancelled = lookup.Run(input, stopping.Token);
        stopping.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        var retired = lookup.Run(input, default);
        session.Connection.Returns((LibreNmsConnection?)null);
        session.StateChanged += Raise.Event();
        Assert.Equal(CompactNetworkLookups.Changed, await retired.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Empty(evidence);
    }
    private sealed class PendingInventory : ILibreNmsTransport
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LibreNmsConnection? Connection => null;
        public Task<IReadOnlyList<T>> GetCollectionAsync<T>(string relativeUrl, string collectionProperty, CancellationToken cancellationToken = default)
        {
            if (relativeUrl == "devices" && typeof(T) == typeof(Device))
                return Task.FromResult((IReadOnlyList<T>)(object)new[] { Fakes.Device(1, "switch.example.net") });
            Assert.Equal("inventory/1/all", relativeUrl);
            Started.TrySetResult();
            return new TaskCompletionSource<IReadOnlyList<T>>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        }
        public Task<JsonDocument> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<string> SendRawAsync(string relativeUrl, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    [Fact]
    public async Task Detail_profile_refuses_other_profiles_and_writes_before_transport()
    {
        var opened = false;
        using var client = new ConnectionBoundClient(new SessionScope(0, new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token")),
            _ => { opened = true; throw new InvalidOperationException(); }, deviceDetails: true);
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.GetAsync("1"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.IsUnderMaintenanceAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.DeleteAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.GetInventoryAsync(0));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Routing.ListBgpSessionsAsync(-1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Arp.FindByMacAsync("aa:bb:cc:00:00:05"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Arp.ListAllAsync());
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Ports.ListAllStatusAsync());
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.SensorTransport.SendRawAsync("devices/1/health"));
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Alerts);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.DeviceGroups);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Links);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.TraceTransport);
        Assert.False(opened);
    }
}
