using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DashyNMS.Agent;
using DashyNMS.Agent.Health;
using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// The shared device sensors fixture (#603) served over loopback HTTP, as DashyNMS/mcp's
/// <c>SharedDeviceSensorsHttpFixtureTests</c> serves it to the MCP server's <c>device_sensors</c>.
/// Here it goes to Intelligence's <c>device_sensors</c> through the app's per-chat transports
/// (<see cref="IntelligenceDeviceTransports"/>, Core's real <see cref="LibreNmsTransport"/>) and the
/// connection-bound client's sensor routes, and to the package's <see cref="HostDeviceSensors"/> over
/// Core's real client and transport on mobile's Core pin.
/// </summary>
/// <remarks>
/// Nothing in Core is faked: status codes, bodies, retries and the timeout reach each side through
/// Core's own parsing and retry policy. The device-by-device route reads concurrently, so those
/// requests are compared as a set. Mobile takes the device and the type filter (#635), as the
/// catalogue describes them.
/// </remarks>
public sealed partial class SharedDeviceSensorsHttpTests
{
    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DeviceSensors");

    private static JsonElement Load(string file)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDirectory, file)));
        return json.RootElement.Clone();
    }

    private static readonly JsonElement Cases = Load("cases.json");
    private static readonly JsonElement Transport = Load("transport.json");

    private static int TimeoutSeconds => Transport.GetProperty("timeoutSeconds").GetInt32();

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string[] Strings(JsonElement element, string name)
        => element.GetProperty(name).EnumerateArray().Select(r => r.GetString()!).ToArray();

    private static int[] Ints(JsonElement element, string name)
        => element.GetProperty(name).EnumerateArray().Select(i => i.GetInt32()).ToArray();

    private static double?[] Values(JsonElement element)
        => element.GetProperty("values").EnumerateArray().Select(v => v.ValueKind == JsonValueKind.Null ? (double?)null : v.GetDouble()).ToArray();

    /// <summary>Values rounded to two places, compared as numbers: 3.22 may round to 3.2200000000000002.</summary>
    private static void SameValues(double?[] expected, IEnumerable<double?> actual)
    {
        var values = actual.ToArray();
        Assert.Equal(expected.Length, values.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].HasValue, values[i].HasValue);
            if (expected[i] is { } e) Assert.Equal(e, values[i]!.Value, 9);
        }
    }

    private static string[] Sorted(IEnumerable<string> requests) => requests.Order(StringComparer.Ordinal).ToArray();

    /// <summary>The ids the device list holds: mobile resolves the device from it before reading sensors.</summary>
    private static readonly HashSet<int> Listed = Load("devices.json").GetProperty("devices").EnumerateArray()
        .Select(d => d.GetProperty("device_id").GetInt32()).ToHashSet();

    /// <summary>Mobile's requests: the device list for resolution (#635), then the package's own if the list holds the device.</summary>
    private static string[] MobileRequests(int device, string[] package) => Listed.Contains(device) ? ["devices", .. package] : ["devices"];

    private static JsonElement Case(string name) => Cases.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);

    public static TheoryData<string> AllCases()
    {
        var data = new TheoryData<string>();
        foreach (var c in Cases.GetProperty("cases").EnumerateArray())
            data.Add(c.GetProperty("name").GetString()!);
        return data;
    }

    /// <summary>Cases mobile can ask: no type, which its lookup doesn't take.</summary>
    public static TheoryData<string> MobileCases()
    {
        var data = new TheoryData<string>();
        foreach (var c in Cases.GetProperty("cases").EnumerateArray())
            data.Add(c.GetProperty("name").GetString()!);
        return data;
    }

    public static TheoryData<string> TransportFailures()
    {
        var data = new TheoryData<string>();
        foreach (var f in Transport.GetProperty("failures").EnumerateArray())
            data.Add(f.GetProperty("name").GetString()!);
        return data;
    }

    /// <summary>One server's routes from <c>cases.json</c>, as LibreNMS would answer them; any other path is a 404.</summary>
    private static Dictionary<string, Answer> Routes(string server)
    {
        var routes = new Dictionary<string, Answer>(StringComparer.Ordinal);
        foreach (var route in Cases.GetProperty("servers").GetProperty(server).EnumerateObject())
        {
            if (route.Value.ValueKind == JsonValueKind.Number)
                routes[route.Name] = new(route.Value.GetInt32(), """{"status":"error","message":"Insufficient permissions"}""");
            else if (route.Name == "health")
                foreach (var health in Load(route.Value.GetString()!).EnumerateObject())
                    routes[health.Name] = new(200, health.Value.GetRawText());
            else
                routes[route.Name] = new(200, File.ReadAllText(Path.Combine(FixtureDirectory, route.Value.GetString()!)));
        }
        return routes;
    }

    private static LibreNmsConnection Connection(Uri root) => new(root, FixtureServer.Token, timeoutSeconds: TimeoutSeconds);

    /// <summary>Mobile's device_sensors as the app builds it: a session on this address and the app's per-chat transports.</summary>
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
            Sensors = new CompactNetworkLookups(Fakes.Client(), settings, session, deviceTransport: transports.Open)
                .Create(found => Evidence.AddRange(found)).Single(l => l.Name == "device_sensors");
        }

        public IntelligenceLookup Sensors { get; }

        public List<IntelligenceEvidence> Evidence { get; } = [];

        public void Dispose()
        {
            transports.Dispose();
            shared.Dispose();
        }
    }

    /// <summary>The package's host operation over Core's real client and transport on mobile's Core pin.</summary>
    private sealed class Package : IDisposable
    {
        private readonly LibreNmsTransport transport = new(NullLogger<LibreNmsTransport>.Instance);
        private readonly LibreNmsClient client;

        public Package(Uri root)
        {
            client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
            client.Connect(Connection(root));
            Sensors = HostDeviceSensors.Create(client, transport, TimeProvider.System, serverTimestampsAreUtc: true);
        }

        public HostDeviceSensors Sensors { get; }

        public void Dispose()
        {
            Sensors.Dispose();
            client.Dispose();
            transport.Dispose();
        }
    }

    /// <summary>One shown reading as mobile words it.</summary>
    private sealed record Shown(int Id, double? Value, string State);

    private static List<Shown> Rows(string text) => [.. text.Split('\n').Select(l => Row().Match(l)).Where(m => m.Success).Select(m =>
    {
        var value = m.Groups["value"].Value;
        var state = m.Groups["state"].Value switch
        {
            "critical" => "critical",
            "warning" => "warning",
            "OK" => "ok",
            _ => "unknown",
        };
        return new Shown(int.Parse(m.Groups["id"].Value, CultureInfo.InvariantCulture),
            value == "no reading" ? null : double.Parse(value.Split(' ')[0], CultureInfo.InvariantCulture), state);
    })];

    [GeneratedRegex(@"^.+? \[\[sensor:(?<id>\d+)\]\]: \S+ (?<value>no reading|-?[0-9.]+(?: \S+)?); (?<state>critical|warning|OK|state unknown|not assessed: no limits)(?: \(.*\))?$")]
    private static partial Regex Row();

    [Theory]
    [MemberData(nameof(MobileCases))]
    public async Task Mobile_and_the_package_judge_the_fixture_identically_over_HTTP(string name)
    {
        var c = Case(name);
        var routes = Routes(c.GetProperty("server").GetString()!);
        using var mobileServer = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var packageServer = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var mobile = new Mobile(mobileServer.Root);
        using var package = new Package(packageServer.Root);
        var device = c.GetProperty("device").GetInt32();

        var type = Text(c, "type");
        var arguments = type is null ? $$"""{"device":"{{device}}"}""" : $$"""{"device":"{{device}}","type":"{{type}}"}""";
        var text = await mobile.Sensors.Run(arguments, default);
        var outcome = await package.Sensors.ReadAsync(new(device, type));

        Assert.Equal(Sorted(MobileRequests(device, Strings(c, "requests"))), Sorted(mobileServer.Requests));
        Assert.Equal(Sorted(Strings(c, "requests")), Sorted(packageServer.Requests));

        if (Text(c, "code") is { } code)
        {
            // A device the list doesn't hold gets MCP's sentence from resolution, before any sensor read.
            Assert.Equal(CompactNetworkLookups.NoDevice, text);
            Assert.Empty(mobile.Evidence);
            Assert.Equal(Enum.Parse<HostFailure>(code), outcome.Error!.Code);
            return;
        }

        var totals = c.GetProperty("totals");
        var count = totals.GetProperty("readings").GetInt32();
        Assert.Contains(count == 0 ? $"[[device:{device}]]: 0."
            : $"[[device:{device}]]: {count}; {totals.GetProperty("critical").GetInt32()} critical, {totals.GetProperty("warning").GetInt32()} warning, " +
              $"{totals.GetProperty("ok").GetInt32()} OK, {totals.GetProperty("unknown").GetInt32()} unknown.", text.Split('\n')[0]);

        var rows = Rows(text);
        Assert.Equal(Ints(c, "readings"), rows.Select(r => r.Id));
        Assert.Equal(Strings(c, "states"), rows.Select(r => r.State));
        // A missing reading is "no reading", never a zero; a zero is still 0.
        SameValues(Values(c), rows.Select(r => r.Value));
        Assert.DoesNotContain("more results omitted", text);

        var result = outcome.Result!;
        Assert.Equal(Ints(c, "readings"), result.Readings.Select(r => r.SensorId));
        Assert.Equal(c.GetProperty("deviceByDevice").GetBoolean(), result.DeviceByDevice);

        // Evidence only for what was shown, each chip with the reading's state; unknown is never OK.
        var sensors = mobile.Evidence.Where(e => e.Reference.Key.StartsWith("sensor:", StringComparison.Ordinal)).ToList();
        Assert.Equal(Ints(c, "readings").Select(id => "sensor:" + id), sensors.Select(e => e.Reference.Key));
        Assert.Equal(Strings(c, "states").Select(s => s switch { "critical" => "critical", "warning" => "warning", "ok" => "up", _ => "unknown" }),
            sensors.Select(e => e.Reference.Tone));
        Assert.All(sensors, e => Assert.Equal(new DesktopNMS.Core.Intelligence.IntelligenceObjectReference("sensor", e.Reference.Object!.Id, device), e.Reference.Object));
        Assert.Single(mobile.Evidence, e => e.Reference.Key == $"device:{device}");
    }

    [Theory]
    [MemberData(nameof(AllCases))]
    public async Task The_package_on_mobiles_Core_gives_the_shared_readings_for_every_case(string name)
    {
        var c = Case(name);
        var routes = Routes(c.GetProperty("server").GetString()!);
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var package = new Package(server.Root);

        var outcome = await package.Sensors.ReadAsync(new(c.GetProperty("device").GetInt32(), Text(c, "type")));

        if (Text(c, "code") is { } code)
        {
            Assert.Null(outcome.Result);
            Assert.Equal(Enum.Parse<HostFailure>(code), outcome.Error!.Code);
        }
        else
        {
            var result = outcome.Result!;
            Assert.Null(outcome.Error);
            Assert.Equal(Ints(c, "readings"), result.Readings.Select(r => r.SensorId));
            Assert.Equal(Strings(c, "states"), result.Readings.Select(r => r.State));
            Assert.Equal(Strings(c, "assessments"), result.Readings.Select(r => r.Assessment.ToString()));
            SameValues(Values(c), result.Readings.Select(r => r.Value));
            var totals = c.GetProperty("totals");
            Assert.Equal(totals.GetProperty("readings").GetInt32(), result.Totals.Readings);
            Assert.Equal(totals.GetProperty("critical").GetInt32(), result.Totals.Critical);
            Assert.Equal(totals.GetProperty("warning").GetInt32(), result.Totals.Warning);
            Assert.Equal(totals.GetProperty("ok").GetInt32(), result.Totals.Ok);
            Assert.Equal(totals.GetProperty("unknown").GetInt32(), result.Totals.Unknown);
            Assert.Equal(c.GetProperty("types").EnumerateObject().Select(t => (t.Name, t.Value.GetInt32())), result.Types.Select(t => (t.Type, t.Count)));
            Assert.Equal(c.GetProperty("deviceByDevice").GetBoolean(), result.DeviceByDevice);
        }

        Assert.Equal(Sorted(Strings(c, "requests")), Sorted(server.Requests));
    }

    [Theory]
    [MemberData(nameof(TransportFailures))]
    public async Task Mobile_and_the_package_fail_alike_when_the_sensor_routes_fail(string name)
    {
        var failure = Transport.GetProperty("failures").EnumerateArray().Single(f => f.GetProperty("name").GetString() == name);
        var code = Enum.Parse<HostFailure>(failure.GetProperty("code").GetString()!);
        var route = failure.GetProperty("route").GetString()!;
        var routes = Routes(route == "resources/sensors" ? "fleet" : "deviceByDevice");
        routes[route] = Text(failure, "behaviour") == "hang" ? Answer.Hang
            : new Answer(failure.GetProperty("status").GetInt32(),
                Transport.GetProperty("bodies").GetProperty(failure.GetProperty("body").GetString()!).GetString()!);
        using var mobileServer = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var packageServer = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var mobile = new Mobile(mobileServer.Root);
        using var package = new Package(packageServer.Root);

        // Side by side: a request that never gets an answer costs the full timeout on each.
        var mobileCall = mobile.Sensors.Run("1", default);
        var packageCall = package.Sensors.ReadAsync(new(1));
        await Task.WhenAll(mobileCall, packageCall);
        var text = await mobileCall;
        var outcome = await packageCall;

        // Mobile: its fixed line for the category, no rows, no evidence, no server text.
        Assert.Equal(code == HostFailure.Refused ? CompactNetworkLookups.Refused : CompactNetworkLookups.Failed, text);
        Assert.DoesNotContain("synthetic-secret", text);
        Assert.Empty(mobile.Evidence);
        // Package: the shared category, no result.
        Assert.Null(outcome.Result);
        Assert.Equal(code, outcome.Error!.Code);

        Assert.Equal(MobileRequests(1, Strings(failure, "requests")), mobileServer.Requests);
        Assert.Equal(Strings(failure, "requests"), packageServer.Requests);
    }
}
