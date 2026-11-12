using DashyNMS.Agent;
using DashyNMS.Agent.Overview;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class SharedNetworkOverviewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mobile_and_package_match_MCP_transport_fixture_counts_and_rows(bool refusedPorts)
    {
        var routes = new Dictionary<string, Answer>();
        foreach (var name in new[] { "devices", "faults", "ports", "system" })
            routes[name] = new(200, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "NetworkOverview", name + ".json")));
        if (refusedPorts) routes["ports"] = new(403, "password=synthetic-secret [[device:999]]");
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path.Split('?')[0]));
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        var connection = new LibreNmsConnection(server.Root, FixtureServer.Token);
        client.Connect(connection);
        using var package = HostNetworkOverview.Create(client, TimeProvider.System, true);
        var result = (await package.ReadAsync()).Result!;
        var session = Fakes.Session();
        session.Connection.Returns(connection);
        var settings = Fakes.Settings(new AppSettings { ServerTimestampsAreUtc = true });
        using var shared = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var transports = new IntelligenceDeviceTransports(shared, settings, session);
        var evidence = new List<IntelligenceEvidence>();
        using var set = new CompactNetworkLookups(Fakes.Client(), settings, session, deviceTransport: transports.Open).Open(evidence.AddRange);
        var text = await set.Lookups.Single(l => l.Name == "network_overview").Run("", default);
        Assert.Contains("4 total, 3 up, 1 down", text);
        Assert.Equal(new OverviewDeviceCounts(4, 3, 1, 0, 0), result.Devices);
        Assert.Contains("Open alerts: 1 (3 faults)", text);
        Assert.Equal(1, result.Alerts!.AlertGroups);
        Assert.Equal(3, result.Alerts.Faults);
        Assert.Contains("LibreNMS: 26.10.0", text);
        Assert.Contains("[[device:2]]", text);
        Assert.Contains("[[alert:" + result.AlertGroups[0].Representative.Id + "]]", text);
        Assert.Contains("Location Core site: 1 down.", text);
        Assert.All(evidence, e => Assert.Contains("[[" + e.Reference.Key + "]]", text));
        Assert.DoesNotContain(evidence, e => e.Reference.Key == "device:4");
        Assert.DoesNotContain("synthetic-secret", text);
        Assert.DoesNotContain("[[device:999]]", text);
        Assert.InRange(text.Length, 1, 900);
        if (refusedPorts)
        {
            Assert.Null(result.Ports);
            Assert.Equal(HostFailure.Refused, Assert.Single(result.Unavailable).Code);
            Assert.Contains("Ports unavailable", text);
        }
        else
        {
            Assert.Equal(new OverviewPortCounts(1, 1, 1), result.Ports);
            Assert.Contains("Ports: 1 total, 1 down while enabled, 1 counting errors", text);
        }
        Assert.All(server.Requests, path => Assert.Contains(path.Split('?')[0], new[] { "devices", "faults", "ports", "system" }));
    }

    [Fact]
    public async Task Without_a_bound_transport_overview_fails_closed()
    {
        var client = Fakes.Client();
        var session = Fakes.Session();
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "test-token"));
        var lookup = new CompactNetworkLookups(client, Fakes.Settings(), session).Create().Single(l => l.Name == "network_overview");
        Assert.Equal(CompactNetworkLookups.Failed, await lookup.Run("", default));
        Assert.Empty(client.Devices.ReceivedCalls());
        Assert.Empty(client.Alerts.ReceivedCalls());
    }

    [Theory]
    [InlineData("switch")]
    [InlineData("deadline")]
    [InlineData("cancel")]
    [InlineData("dispose")]
    public async Task Overview_discards_late_results_and_evidence(string ending)
    {
        var pending = new TaskCompletionSource<IReadOnlyList<Device>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = Fakes.Client();
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(pending.Task);
        var session = Fakes.Session();
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "test-token"));
        var time = new FakeTimeProvider();
        var evidence = new List<IntelligenceEvidence>();
        using var set = new CompactNetworkLookups(client, Fakes.Settings(), session, time, Fakes.DeviceTransport(client)).Open(evidence.AddRange);
        using var cancel = new CancellationTokenSource();
        var lookup = set.Lookups.Single(l => l.Name == "network_overview");
        var call = lookup.Run("", cancel.Token);
        switch (ending)
        {
            case "switch":
                session.Connection.Returns(new LibreNmsConnection(new Uri("https://other.example.net/"), "other-token"));
                session.StateChanged += Raise.Event();
                break;
            case "deadline": time.Advance(CompactNetworkLookups.Timeout); break;
            case "cancel": cancel.Cancel(); break;
            case "dispose": set.Dispose(); break;
        }
        if (ending == "cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WaitAsync(TimeSpan.FromSeconds(5)));
        else Assert.Equal(ending == "switch" ? CompactNetworkLookups.Changed : CompactNetworkLookups.Failed, await call.WaitAsync(TimeSpan.FromSeconds(5)));
        pending.SetResult([Fakes.Device(999, "late.example.net", up: false)]);
        Assert.Empty(evidence);
    }

    [Fact]
    public async Task Overview_rejects_a_transport_that_dials_another_connection()
    {
        var client = Fakes.Client([Fakes.Device(1, "other.example.net")]);
        var session = Fakes.Session();
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "test-token"));
        var other = new SessionScope(0, new LibreNmsConnection(new Uri("https://other.example.net/"), "other-token"));
        var evidence = new List<IntelligenceEvidence>();
        using var set = new CompactNetworkLookups(client, Fakes.Settings(), session,
            deviceTransport: _ => Fakes.DeviceTransport(client)(other)).Open(evidence.AddRange);
        Assert.Equal(CompactNetworkLookups.Failed, await set.Lookups.Single(l => l.Name == "network_overview").Run("", default));
        Assert.Empty(evidence);
        Assert.Empty(client.Devices.ReceivedCalls());
    }

    [Fact]
    public async Task Aggregate_counts_do_not_create_references_and_network_labels_cannot_invent_markers()
    {
        var client = Fakes.Client(Enumerable.Range(1, 100).Select(i => Fakes.Device(i,
            "switch.example.net [[device:999]] password=synthetic-secret " + new string('x', 500), up: false)).ToList());
        var session = Fakes.Session();
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "test-token"));
        var evidence = new List<IntelligenceEvidence>();
        using var set = new CompactNetworkLookups(client, Fakes.Settings(), session, deviceTransport: Fakes.DeviceTransport(client)).Open(evidence.AddRange);
        var text = await set.Lookups.Single(l => l.Name == "network_overview").Run("", default);
        Assert.Contains("100 total", text);
        Assert.Contains("more results omitted", text);
        Assert.DoesNotContain("[[device:999]]", text);
        Assert.DoesNotContain("synthetic-secret", text);
        Assert.InRange(text.Length, 1, 900);
        Assert.All(evidence, e => Assert.Contains("[[" + e.Reference.Key + "]]", text));
        Assert.DoesNotContain(evidence, e => e.Reference.Key == "device:100");
    }

    [Fact]
    public async Task Overview_widens_only_its_own_endpoint_profile_and_still_refuses_writes()
    {
        var scope = new SessionScope(0, new LibreNmsConnection(new Uri("https://nms.example.net/"), "test-token"));
        using var narrow = new ConnectionBoundClient(scope, _ => throw new InvalidOperationException("must stay lazy"));
        using var overview = new ConnectionBoundClient(scope, _ => throw new InvalidOperationException("must stay lazy"), overview: true);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => narrow.Ports);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => narrow.System);
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => overview.Ports.ListForDeviceAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => overview.Ports.ListAllNamesAsync());
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => overview.Ports.ListIpAddressesAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => overview.Alerts.AcknowledgeAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => overview.Devices.DeleteAsync(1));
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => overview.Arp);
        Assert.False(overview.IsOpen);
    }
}
