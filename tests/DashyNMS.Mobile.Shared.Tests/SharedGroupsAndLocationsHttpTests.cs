using DashyNMS.Agent.Devices;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

public sealed class SharedGroupsAndLocationsHttpTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "GroupsAndLocations");
    private static Dictionary<string, Answer> Routes() => new()
    {
        ["devices"] = new(200, File.ReadAllText(Path.Combine(Folder, "devices.json"))),
        ["devicegroups"] = new(200, File.ReadAllText(Path.Combine(Folder, "groups.json"))),
    };
    private sealed class Mobile : IDisposable
    {
        private readonly LibreNmsTransport shared = new(NullLogger<LibreNmsTransport>.Instance);
        private readonly IntelligenceDeviceTransports transports;
        private readonly IntelligenceLookupSet set;
        internal Mobile(Uri root)
        {
            var session = Fakes.Session();
            session.Connection.Returns(new LibreNmsConnection(root, FixtureServer.Token, timeoutSeconds: 5));
            var settings = Fakes.Settings();
            transports = new(shared, settings, session);
            set = new CompactNetworkLookups(Fakes.Client(), settings, session, deviceTransport: transports.Open).Open(Evidence.AddRange);
            Scopes = set.Lookups.Single(l => l.Name == "list_groups_and_locations");
        }
        internal IntelligenceLookup Scopes { get; }
        internal List<IntelligenceEvidence> Evidence { get; } = [];
        public void Dispose() { set.Dispose(); transports.Dispose(); shared.Dispose(); }
    }
    [Fact]
    public async Task Counts_names_and_the_reviewed_requests_match_the_shared_operation()
    {
        var routes = Routes();
        using var mobileServer = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var hostServer = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(mobileServer.Root);
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(hostServer.Root, FixtureServer.Token));
        using var host = HostGroupsAndLocations.Create(client, TimeProvider.System);
        var result = (await host.ReadAsync()).Result!;
        var text = await mobile.Scopes.Run("{}", default);
        Assert.Equal(3, result.Data.Groups!.Count);
        Assert.Equal(3, result.Data.Locations.Count);
        Assert.Contains("Groups: 3; locations: 3.", text);
        Assert.Contains("Location Lab annexe: 4 devices; 2 down.", text);
        Assert.Contains("Group Core routers [[group:2]]; type static; Synthetic group", text);
        Assert.Equal(new[] { "devicegroups", "devices" }, mobileServer.Requests.Order());
        Assert.Equal(hostServer.Requests.Order(), mobileServer.Requests.Order());
        Assert.All(mobile.Evidence, e => Assert.Equal("group", e.Reference.Object!.Kind));
    }
    [Fact]
    public async Task Exact_names_stay_in_evidence_and_locations_without_ids_never_get_invented_markers()
    {
        var routes = Routes();
        routes["devices"] = new(200, """{"devices":[{"device_id":1,"location":"  Site A  ","location_id":3,"status":0,"disabled":0},{"device_id":2,"location":"Other","status":1,"disabled":0}]}""");
        routes["devicegroups"] = new(200, """{"groups":[{"id":7,"name":"password=synthetic-secret","type":"static"}]}""");
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Scopes.Run("{}", default);
        Assert.DoesNotContain("synthetic-secret", text);
        var group = Assert.Single(mobile.Evidence, e => e.Reference.Key == "group:7");
        Assert.Equal("password=synthetic-secret", group.Filter);
        var location = Assert.Single(mobile.Evidence, e => e.Reference.Key == "location:3");
        Assert.Equal("  Site A  ", location.Filter);
        Assert.Equal("Site A", location.Reference.Name);
        Assert.Contains("Location Site A [[location:3]]: 1 devices; 1 down.", text);
        Assert.Single(mobile.Evidence, e => e.Reference.Object?.Kind == "location");
    }
    [Theory]
    [InlineData("devices", false)]
    [InlineData("devicegroups", true)]
    public async Task Refused_groups_are_partial_but_devices_are_required(string path, bool partial)
    {
        var routes = Routes();
        routes[path] = new(403, """{"message":"password=synthetic-secret"}""");
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Scopes.Run("{}", default);
        if (partial)
        {
            Assert.StartsWith("Groups unavailable; locations: 3.", text);
            Assert.DoesNotContain(mobile.Evidence, e => e.Reference.Object?.Kind == "group");
        }
        else
        {
            Assert.Equal(CompactNetworkLookups.Refused, text);
            Assert.Empty(mobile.Evidence);
        }
        Assert.DoesNotContain("synthetic-secret", text);
    }
    [Fact]
    public async Task Long_catalogues_count_omitted_rows_and_offer_only_displayed_evidence()
    {
        var routes = Routes();
        routes["devicegroups"] = new(200, System.Text.Json.JsonSerializer.Serialize(new
        {
            groups = Enumerable.Range(1, 30).Select(id => new { id, name = $"Group {id:D2}", desc = new string('x', 80), type = "static" }),
        }));
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Scopes.Run("{}", default);
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
        Assert.Contains(" more", text);
        Assert.NotEmpty(mobile.Evidence);
        Assert.True(mobile.Evidence.Count < 30);
        Assert.All(mobile.Evidence, e => Assert.Contains("[[" + e.Reference.Key + "]]", text));
        Assert.DoesNotContain("[[group:30]]", text);
    }

    [Fact]
    public async Task Pending_group_reads_obey_deadline_cancellation_and_retirement()
    {
        var response = new TaskCompletionSource<IReadOnlyList<DeviceGroup>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = Fakes.Client();
        client.DeviceGroups.ListAsync(Arg.Any<CancellationToken>()).Returns(response.Task);
        var session = Fakes.Session();
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token"));
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var lookups = new CompactNetworkLookups(client, Fakes.Settings(), session, time, deviceTransport: Fakes.DeviceTransport(client));
        using var set = lookups.Open(_ => { });
        var lookup = set.Lookups.Single(l => l.Name == "list_groups_and_locations");
        var timed = lookup.Run("{}", default);
        time.Advance(CompactNetworkLookups.Timeout);
        Assert.Equal(CompactNetworkLookups.Failed, await timed);
        using var stopping = new CancellationTokenSource();
        var cancelled = lookup.Run("{}", stopping.Token);
        stopping.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        var evidence = new List<IntelligenceEvidence>();
        using var retired = lookups.Open(evidence.AddRange);
        var pending = retired.Lookups.Single(l => l.Name == "list_groups_and_locations").Run("{}", default);
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://new.example.net/"), "synthetic-token"));
        response.SetResult([new DeviceGroup { Id = 7, Name = "Old group" }]);
        Assert.Equal(CompactNetworkLookups.Changed, await pending);
        Assert.Empty(evidence);
    }

    [Fact]
    public async Task Scope_profile_refuses_memberships_other_resources_and_writes_before_transport()
    {
        var session = new SessionScope(0, new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token"));
        var opened = false;
        using var client = new ConnectionBoundClient(session, _ => { opened = true; throw new InvalidOperationException(); }, scopes: true);
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.GetAsync("1"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.DeviceGroups.GetMemberDeviceIdsAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.DeviceGroups.DeleteAsync("Example"));
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Alerts);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Arp);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Locations);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Ports);
        Assert.False(opened);
    }
}
