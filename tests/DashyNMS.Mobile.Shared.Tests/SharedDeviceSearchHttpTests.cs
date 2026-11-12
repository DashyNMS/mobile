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
/// side's server counts what reached it. Mobile's <c>find_device</c> takes only search text, so
/// the scope cases run through the package's executor alone. That shows the package and mobile's
/// Core pin agree with MCP on scopes, not that mobile offers them. A separate test checks that
/// <c>find_device</c> never reads a group. The fixture README lists what is still not compared.
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
    public async Task The_package_on_mobiles_Core_applies_the_shared_scope_boundaries(int index)
    {
        var c = Scopes.GetProperty("cases")[index];
        var routes = ScopeRoutes();
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var package = new Package(server.Root);

        var outcome = await package.Search.FindDevicesAsync(new(Text(c, "query"), Text(c, "status"), Text(c, "group"), Text(c, "location"),
            Limit: DeviceSearch.MaxMatches));

        if (Text(c, "code") is { } code)
        {
            Assert.Null(outcome.Result);
            Assert.Equal(Enum.Parse<DeviceSearchFailure>(code), outcome.Error!.Code);
        }
        else
        {
            Assert.Null(outcome.Error);
            Assert.Equal(Ints(c, "ids"), outcome.Result!.Matches.Select(m => m.Device.DeviceId));
            Assert.Equal(c.GetProperty("total").GetInt32(), outcome.Result.Page.Total);
        }
        Assert.Equal(Strings(c, "requests"), server.Requests);
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
