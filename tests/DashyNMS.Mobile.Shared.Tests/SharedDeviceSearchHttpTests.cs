using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DashyNMS.Agent.Devices;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// The shared device search fixture (#596) served over loopback HTTP, as DashyNMS/mcp's
/// <c>SharedSearchHttpFixtureTests</c> serves it to the MCP server's own services. Here it goes to
/// Intelligence's <c>find_device</c> through the app's per-chat transports
/// (<see cref="IntelligenceDeviceTransports"/>, Core's real <see cref="LibreNmsTransport"/>), and to
/// the package's <see cref="HostDeviceSearch"/> over Core's real client on mobile's Core pin.
/// </summary>
/// <remarks>
/// Nothing in Core is faked: status codes, bodies, retries, the timeout and an address with
/// nothing listening reach each side through Core's own parsing and retry policy, and each
/// side's server counts what reached it. Scope cases run through both adapters.
/// MAC cases below use additional synthetic ARP responses, separate from the shared bundle.
/// </remarks>
public sealed partial class SharedDeviceSearchHttpTests
{
    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DeviceSearch");

    private static JsonElement Load(string file)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDirectory, file)));
        return json.RootElement.Clone();
    }

    private static readonly JsonElement Scopes = Load("scopes.json");
    private static readonly JsonElement Transport = Load("transport.json");
    private static readonly JsonElement Basic = Load("cases.json");

    private static int TimeoutSeconds => Transport.GetProperty("timeoutSeconds").GetInt32();

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static string[] Strings(JsonElement element, string name)
        => element.GetProperty(name).EnumerateArray().Select(r => r.GetString()!).ToArray();

    private static int[] Ints(JsonElement element, string name)
        => element.GetProperty(name).EnumerateArray().Select(i => i.GetInt32()).ToArray();

    public static TheoryData<int> ScopeCases()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < Scopes.GetProperty("cases").GetArrayLength(); i++)
            data.Add(i);
        return data;
    }

    public static TheoryData<string> TransportFailures()
    {
        var data = new TheoryData<string>();
        foreach (var f in Transport.GetProperty("failures").EnumerateArray())
            data.Add(f.GetProperty("name").GetString()!);
        return data;
    }

    public static TheoryData<string> BasicQueries()
    {
        var data = new TheoryData<string>();
        foreach (var c in Basic.GetProperty("cases").EnumerateArray())
            data.Add(c.GetProperty("query").GetString()!);
        return data;
    }

    /// <summary>Every route in <c>scopes.json</c>, as LibreNMS would answer it.</summary>
    private static Dictionary<string, Answer> ScopeRoutes()
    {
        var routes = new Dictionary<string, Answer>(StringComparer.Ordinal);
        foreach (var route in Scopes.GetProperty("routes").EnumerateArray())
        {
            var body = Text(route, "file") is { } file
                ? File.ReadAllText(Path.Combine(FixtureDirectory, file))
                : route.GetProperty("body").GetString()!;
            routes[route.GetProperty("path").GetString()!] = new(route.GetProperty("status").GetInt32(), body);
        }
        return routes;
    }

    private static LibreNmsConnection Connection(Uri root) => new(root, FixtureServer.Token, timeoutSeconds: TimeoutSeconds);

    /// <summary>
    /// Mobile's find_device as the app builds it: a session on this address, the app's per-chat
    /// transports over a real shared transport, and the saved timeout the fixture names.
    /// </summary>
    private sealed class Mobile : IDisposable
    {
        private readonly LibreNmsTransport shared = new(NullLogger<LibreNmsTransport>.Instance);
        private readonly IntelligenceDeviceTransports transports;

        public Mobile(Uri root)
        {
            var settings = Fakes.Settings(new AppSettings { TimeoutSeconds = TimeoutSeconds });
            var session = Fakes.Session();
            session.Connection.Returns(Connection(root));
            transports = new IntelligenceDeviceTransports(shared, settings, session);
            Find = new CompactNetworkLookups(Fakes.Client(), settings, session, deviceTransport: transports.Open)
                .Create(found => Evidence.AddRange(found)).Single(l => l.Name == "find_device");
        }

        public IntelligenceLookup Find { get; }

        public List<IntelligenceEvidence> Evidence { get; } = [];

        public void Dispose()
        {
            transports.Dispose();
            shared.Dispose();
        }
    }

    /// <summary>The package's executor over Core's real client on mobile's Core pin, as DashyNMS/mcp's tests run it.</summary>
    private sealed class Package : IDisposable
    {
        private readonly LibreNmsTransport transport = new(NullLogger<LibreNmsTransport>.Instance);
        private readonly LibreNmsClient client;

        public Package(Uri root)
        {
            client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
            client.Connect(Connection(root));
            Search = HostDeviceSearch.Create(client, TimeProvider.System, serverTimestampsAreUtc: false);
        }

        public HostDeviceSearch Search { get; }

        public void Dispose()
        {
            Search.Dispose();
            client.Dispose();
            transport.Dispose();
        }
    }

    private static int[] ShownIds(string text) => DeviceMarker().Matches(text).Select(m => int.Parse(m.Groups[1].Value)).ToArray();

    [GeneratedRegex(@"\[\[device:(\d+)\]\]")]
    private static partial Regex DeviceMarker();

    [Theory]
    [MemberData(nameof(BasicQueries))]
    public async Task Mobile_and_the_package_rank_the_fixture_identically_over_HTTP(string query)
    {
        var expected = Basic.GetProperty("cases").EnumerateArray().First(c => c.GetProperty("query").GetString() == query);
        var routes = ScopeRoutes();
        using var mobileServer = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var packageServer = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var mobile = new Mobile(mobileServer.Root);
        using var package = new Package(packageServer.Root);

        var text = await mobile.Find.Run(query, default);
        var outcome = await package.Search.FindDevicesAsync(new(query, Limit: DeviceSearch.MaxMatches));

        var total = expected.GetProperty("total").GetInt32();
        Assert.Equal(Ints(expected, "ids"), ShownIds(text));
        Assert.StartsWith($"Matching devices: {total}.", text);
        Assert.Equal(Ints(expected, "ids"), outcome.Result!.Matches.Select(m => m.Device.DeviceId));
        Assert.Equal(total, outcome.Result.Page.Total);
        Assert.Equal(Strings(Basic, "requests"), mobileServer.Requests);
        Assert.Equal(Strings(Basic, "requests"), packageServer.Requests);
    }

    [Theory]
    [MemberData(nameof(ScopeCases))]
    public async Task Mobile_and_the_package_apply_the_shared_scope_boundaries(int index)
    {
        var c = Scopes.GetProperty("cases")[index];
        var routes = ScopeRoutes();
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var package = new Package(server.Root);
        using var mobileServer = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var mobile = new Mobile(mobileServer.Root);
        var payload = JsonSerializer.Serialize(new { query = Text(c, "query"), status = Text(c, "status"), group = Text(c, "group"), location = Text(c, "location") });
        var reply = await mobile.Find.Run(payload, default);

        var outcome = await package.Search.FindDevicesAsync(new(Text(c, "query"), Text(c, "status"), Text(c, "group"), Text(c, "location"),
            Limit: DeviceSearch.MaxMatches));

        if (Text(c, "code") is { } code)
        {
            Assert.Null(outcome.Result);
            Assert.Equal(Enum.Parse<DeviceSearchFailure>(code), outcome.Error!.Code);
            // A status the catalogue doesn't document is refused before any request; an
            // unknown or ambiguous scope name is the search's own refusal.
            Assert.Equal(Text(c, "status") is { } status && status is not ("up" or "down" or "disabled")
                ? "The lookup wasn't run: status must be one of up, down, disabled." : CompactNetworkLookups.InvalidSearch, reply);
            Assert.Empty(mobile.Evidence);
        }
        else
        {
            Assert.Null(outcome.Error);
            Assert.Equal(Ints(c, "ids"), outcome.Result!.Matches.Select(m => m.Device.DeviceId));
            Assert.Equal(c.GetProperty("total").GetInt32(), outcome.Result.Page.Total);
            Assert.Equal(Ints(c, "ids"), ShownIds(reply));
            Assert.StartsWith($"Matching devices: {outcome.Result.Page.Total}.", reply);
        }
        Assert.Equal(Strings(c, "requests"), server.Requests);
        Assert.Equal(Strings(c, "requests"), mobileServer.Requests);
    }

    [Fact]
    public async Task Search_combines_text_status_group_and_location()
    {
        var routes = ScopeRoutes();
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var mobile = new Mobile(server.Root);
        var reply = await mobile.Find.Run("{\"query\":\"sw\",\"status\":\"up\",\"group\":\"Core switches\",\"location\":\"Core\"}", default);
        // All four group members are up and at Core in the shared fixture.
        Assert.StartsWith("Matching devices: 4.", reply);
        Assert.Equal(new[] { 3, 5, 2, 1 }, ShownIds(reply));
        Assert.Contains("devicegroups/1", server.Requests);
    }

    [Theory]
    [InlineData("02:00:00:00:00:01")]
    [InlineData("02-00-00-00-00-01")]
    [InlineData("0200.0000.0001")]
    [InlineData("020000000001")]
    public async Task MAC_search_reports_stored_observations_and_matches_the_package(string query)
    {
        var routes = ScopeRoutes();
        routes["resources/ip/arp/02:00:00:00:00:01"] = new(200,
            """{"arp":[{"device_id":1,"port_id":7,"ipv4_address":"192.0.2.77","mac_address":"020000000001"},{"device_id":1,"port_id":8,"ipv4_address":"192.0.2.78","mac_address":"020000000001"}]}""");
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var packageServer = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var mobile = new Mobile(server.Root);
        using var package = new Package(packageServer.Root);
        var reply = await mobile.Find.Run(JsonSerializer.Serialize(new { query }), default);
        var result = (await package.Search.FindDevicesAsync(new(query))).Result!;
        Assert.True(result.IsMacSearch);
        Assert.Equal(result.Matches.Select(m => m.Device.DeviceId), ShownIds(reply));
        Assert.StartsWith("Devices with stored ARP observations: 1. Observations do not prove ownership.", reply);
        Assert.Contains("192.0.2.77 on port 7", reply);
        Assert.Contains("192.0.2.78 on port 8", reply);
        Assert.Single(mobile.Evidence);
        Assert.True(reply.Length <= CompactNetworkLookups.MaxCharacters);
        Assert.Equal(packageServer.Requests, server.Requests);
    }

    [Theory]
    [InlineData("resources/ip/arp/02:00:00:00:00:01", 403)]
    [InlineData("devicegroups", 403)]
    [InlineData("devicegroups/1", 403)]
    public async Task Denied_search_sources_are_refusals_not_empty_results(string denied, int status)
    {
        var routes = ScopeRoutes();
        routes[denied] = new(status, """{"status":"error","message":"synthetic-secret"}""");
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var mobile = new Mobile(server.Root);
        var payload = denied.StartsWith("resources") ? "{\"query\":\"020000000001\"}" : "{\"group\":\"Core switches\"}";
        Assert.Equal(CompactNetworkLookups.Refused, await mobile.Find.Run(payload, default));
        Assert.Empty(mobile.Evidence);
    }

    [Fact]
    public async Task An_empty_ARP_table_is_an_empty_observation_result()
    {
        var routes = ScopeRoutes();
        routes["resources/ip/arp/02:00:00:00:00:01"] = new(200, """{"arp":[]}""");
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var mobile = new Mobile(server.Root);
        var reply = await mobile.Find.Run("020000000001", default);
        Assert.StartsWith("Devices with stored ARP observations: 0.", reply);
        Assert.Empty(mobile.Evidence);
    }

    /// <summary>
    /// The scope boundary mobile itself has: find_device takes search text only, so text naming a
    /// group or a location is just text. It reads the device list and never a group.
    /// </summary>
    [Theory]
    [InlineData("Core switches")]
    [InlineData("group:Core switches")]
    [InlineData("Lab annexe")]
    public async Task Mobile_find_device_reads_only_the_device_list_whatever_the_text_names(string query)
    {
        var routes = ScopeRoutes();
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var mobile = new Mobile(server.Root);

        var text = await mobile.Find.Run(query, default);

        Assert.Equal("Matching devices: 0.", text);
        Assert.Equal(Strings(Basic, "requests"), server.Requests);
    }

    [Theory]
    [MemberData(nameof(TransportFailures))]
    public async Task Mobile_and_the_package_fail_alike_on_the_shared_transport_failures(string name)
    {
        var failure = Transport.GetProperty("failures").EnumerateArray().Single(f => f.GetProperty("name").GetString() == name);
        var query = Transport.GetProperty("query").GetString()!;
        var code = Enum.Parse<DeviceSearchFailure>(failure.GetProperty("code").GetString()!);
        using var mobileServer = FailingServer(failure);
        using var packageServer = FailingServer(failure);
        using var mobile = new Mobile(mobileServer?.Root ?? FixtureServer.NothingListening());
        using var package = new Package(packageServer?.Root ?? FixtureServer.NothingListening());

        // Side by side: a request that never gets an answer costs the full timeout on each.
        var mobileCall = mobile.Find.Run(query, default);
        var packageCall = package.Search.FindDevicesAsync(new(query));
        await Task.WhenAll(mobileCall, packageCall);
        var text = await mobileCall;
        var outcome = await packageCall;

        // Mobile: its fixed line for the category, no rows, no evidence, no server text.
        Assert.Equal(code == DeviceSearchFailure.Refused ? CompactNetworkLookups.Refused : CompactNetworkLookups.Failed, text);
        Assert.True(CompactNetworkLookups.IsFailure(text));
        Assert.DoesNotContain("synthetic-secret", text);
        Assert.Empty(mobile.Evidence);
        // Package: the shared category, no result.
        Assert.Null(outcome.Result);
        Assert.Equal(code, outcome.Error!.Code);

        var requests = Enumerable.Repeat("devices", failure.GetProperty("requests").GetInt32());
        Assert.Equal(requests, mobileServer?.Requests ?? []);
        Assert.Equal(requests, packageServer?.Requests ?? []);
    }

    /// <summary>A server answering the device list as this failure describes; null when nothing should listen.</summary>
    private static FixtureServer? FailingServer(JsonElement failure)
    {
        switch (Text(failure, "behaviour"))
        {
            case "unreachable":
                return null;
            case "hang":
                return new FixtureServer(_ => Answer.Hang);
            default:
                var answer = new Answer(failure.GetProperty("status").GetInt32(),
                    Transport.GetProperty("bodies").GetProperty(failure.GetProperty("body").GetString()!).GetString()!);
                return new FixtureServer(_ => answer);
        }
    }
}
