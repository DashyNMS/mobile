using System.Globalization;
using System.Text.Json;
using DashyNMS.Agent;
using DashyNMS.Agent.Devices;
using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// The shared device summary fixture (#602) served over loopback HTTP, as DashyNMS/mcp's
/// <c>SharedDeviceSummaryHttpFixtureTests</c> serves it to the MCP server's <c>device_health</c>. Here
/// it goes to Intelligence's <c>device_health</c> through the app's per-chat transports
/// (<see cref="IntelligenceDeviceTransports"/>, Core's real <see cref="LibreNmsTransport"/>), and to the
/// package's <see cref="HostDeviceSummary"/> over Core's real client on mobile's Core pin.
/// </summary>
/// <remarks>
/// Nothing in Core is faked: status codes, bodies, retries and the timeout reach each side through
/// Core's own parsing and retry policy, and each side's server counts what reached it. Unlike MCP's
/// <c>device_health</c>, neither side falls back to the device list when <c>devices/{id}</c> fails.
/// </remarks>
public sealed class SharedDeviceSummaryHttpTests
{
    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DeviceSummary");

    private static JsonElement Load(string file)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDirectory, file)));
        return json.RootElement.Clone();
    }

    private static readonly JsonElement Cases = Load("cases.json");
    private static readonly JsonElement Transport = Load("transport.json");

    /// <summary>The ids the device list holds: mobile resolves the device from it before reading <c>devices/{id}</c>.</summary>
    private static readonly HashSet<int> Listed = Load("devices.json").GetProperty("devices").EnumerateArray()
        .Select(d => d.GetProperty("device_id").GetInt32()).ToHashSet();

    /// <summary>Mobile's requests: the device list for resolution (#635), then the package's own reads if the list holds the device.</summary>
    private static string[] MobileRequests(int id, string[] package) => Listed.Contains(id) ? ["devices", .. package] : ["devices"];

    private static int TimeoutSeconds => Transport.GetProperty("timeoutSeconds").GetInt32();

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? Number(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : null;

    private static string[] Strings(JsonElement element, string name)
        => element.GetProperty(name).EnumerateArray().Select(r => r.GetString()!).ToArray();

    private static JsonElement Case(string name) => Cases.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);

    public static TheoryData<string> AllCases()
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

    private static Dictionary<string, Answer> Routes()
    {
        var routes = new Dictionary<string, Answer>(StringComparer.Ordinal);
        foreach (var route in Cases.GetProperty("routes").EnumerateObject())
            routes[route.Name] = new(200, File.ReadAllText(Path.Combine(FixtureDirectory, route.Value.GetString()!)));
        return routes;
    }

    private static LibreNmsConnection Connection(Uri root) => new(root, FixtureServer.Token, timeoutSeconds: TimeoutSeconds);

    /// <summary>Mobile's device_health as the app builds it: a session on this address and the app's per-chat transports.</summary>
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
            Client = Fakes.Client();
            Summary = new CompactNetworkLookups(Client, settings, session, deviceTransport: transports.Open)
                .Create(found => Evidence.AddRange(found)).Single(l => l.Name == "device_health");
        }

        /// <summary>The session's own client, which device_health must never use.</summary>
        public ILibreNmsClient Client { get; }

        public IntelligenceLookup Summary { get; }

        public List<IntelligenceEvidence> Evidence { get; } = [];

        public void Dispose()
        {
            transports.Dispose();
            shared.Dispose();
        }
    }

    /// <summary>The package's host operation over Core's real client on mobile's Core pin.</summary>
    private sealed class Package : IDisposable
    {
        private readonly LibreNmsTransport transport = new(NullLogger<LibreNmsTransport>.Instance);
        private readonly LibreNmsClient client;

        public Package(Uri root)
        {
            client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
            client.Connect(Connection(root));
            Summary = HostDeviceSummary.Create(client, serverTimestampsAreUtc: true);
        }

        public HostDeviceSummary Summary { get; }

        public void Dispose()
        {
            Summary.Dispose();
            client.Dispose();
            transport.Dispose();
        }
    }

    [Theory]
    [MemberData(nameof(AllCases))]
    public async Task Mobile_and_the_package_read_the_fixture_identically_over_HTTP(string name)
    {
        var c = Case(name);
        var routes = Routes();
        using var mobileServer = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var packageServer = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var mobile = new Mobile(mobileServer.Root);
        using var package = new Package(packageServer.Root);
        var id = c.GetProperty("device").GetInt32();
        var marker = $"[[device:{id.ToString(CultureInfo.InvariantCulture)}]]";

        var text = await mobile.Summary.Run(id.ToString(CultureInfo.InvariantCulture), default);
        var outcome = await package.Summary.ReadAsync(new DeviceSummaryRequest(id));

        // The package reads devices/{id} once; mobile resolves the device from the list first. Neither
        // reads through the session's own client.
        Assert.Equal(MobileRequests(id, Strings(c, "requests")), mobileServer.Requests);
        Assert.Equal(Strings(c, "requests"), packageServer.Requests);
        await mobile.Client.Devices.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await mobile.Client.Devices.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());

        if (Text(c, "code") is { } code)
        {
            // A listed device that devices/{id} doesn't return is not reported from the list.
            Assert.Equal(Enum.Parse<HostFailure>(code), outcome.Error!.Code);
            Assert.Null(outcome.Result);
            // A device the list doesn't hold gets MCP's sentence; a listed device devices/{id} doesn't return is not found.
            Assert.Equal(Listed.Contains(id) ? "Device not found." : CompactNetworkLookups.NoDevice, text);
            Assert.Empty(mobile.Evidence);
            return;
        }

        var expected = c.GetProperty("expected");
        var result = outcome.Result!;
        Assert.Null(outcome.Error);
        Assert.Equal(Text(expected, "hardware"), result.Device.Hardware);
        Assert.Equal(Text(expected, "state"), result.Device.State.ToString());
        Assert.Equal(Number(expected, "uptimeSeconds"), result.UptimeSeconds);
        Assert.Equal(Number(expected, "locationId"), (long?)result.Location?.Id);
        Assert.Equal(Text(expected, "filterName"), result.Location?.FilterName);

        // Mobile words the same values: reported state (not the status word), OS, hardware,
        // the shown location name with its marker, and uptime or "unknown".
        var state = Enum.Parse<DeviceState>(Text(expected, "state")!).ToDisplayString();
        Assert.Contains($"{marker}: {state}; IP {Text(expected, "ip")}", text);
        var locationId = Number(expected, "locationId");
        var location = Text(expected, "locationName") is { } shown
            ? $"{shown} [[location:{locationId!.Value.ToString(CultureInfo.InvariantCulture)}]]"
            : CompactNetworkLookups.Clean(Text(expected, "location"));
        var uptime = Number(expected, "uptimeSeconds") is { } seconds ? $"{seconds.ToString(CultureInfo.InvariantCulture)} seconds" : "unknown";
        Assert.Contains($"OS: {Text(expected, "os")}; hardware: {CompactNetworkLookups.Clean(Text(expected, "hardware"))}; location: {location}; uptime: {uptime}.", text);
        Assert.DoesNotContain("synthetic-secret", text);
        Assert.DoesNotContain("{", text);

        // Evidence for what was shown: the device with its reported state, and the location chip
        // keeping LibreNMS's exact name as its Devices filter.
        var device = Assert.Single(mobile.Evidence, e => e.Reference.Key == "device:" + id.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(state, device.Reference.Status);
        Assert.Equal(new DesktopNMS.Core.Intelligence.IntelligenceObjectReference("device", id), device.Reference.Object);
        if (locationId is { } place)
        {
            var chip = Assert.Single(mobile.Evidence, e => e.Reference.Key == "location:" + place.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(Text(expected, "filterName"), chip.Filter);
            Assert.Equal(Text(expected, "locationName"), chip.Reference.Name);
        }
        else
            Assert.DoesNotContain(mobile.Evidence, e => e.Reference.Key.StartsWith("location:", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(TransportFailures))]
    public async Task Mobile_and_the_package_fail_alike_when_the_device_read_fails(string name)
    {
        var failure = Transport.GetProperty("failures").EnumerateArray().Single(f => f.GetProperty("name").GetString() == name);
        var code = Enum.Parse<HostFailure>(failure.GetProperty("code").GetString()!);
        var id = Transport.GetProperty("device").GetInt32();
        var path = "devices/" + id.ToString(CultureInfo.InvariantCulture);
        var routes = Routes();
        var failing = Text(failure, "behaviour") == "hang" ? Answer.Hang
            : new Answer(failure.GetProperty("status").GetInt32(),
                Transport.GetProperty("bodies").GetProperty(failure.GetProperty("body").GetString()!).GetString()!);
        using var mobileServer = new FixtureServer(p => p == path ? failing : routes.GetValueOrDefault(p));
        using var packageServer = new FixtureServer(p => p == path ? failing : routes.GetValueOrDefault(p));
        using var mobile = new Mobile(mobileServer.Root);
        using var package = new Package(packageServer.Root);

        // Side by side: a request that never gets an answer costs the full timeout on each.
        var mobileCall = mobile.Summary.Run(id.ToString(CultureInfo.InvariantCulture), default);
        var packageCall = package.Summary.ReadAsync(new DeviceSummaryRequest(id));
        await Task.WhenAll(mobileCall, packageCall);
        var text = await mobileCall;
        var outcome = await packageCall;

        // Mobile: its fixed line for the category, no rows, no evidence, no server text.
        Assert.Equal(code switch
        {
            HostFailure.Refused => CompactNetworkLookups.Refused,
            HostFailure.NotFound => "Device not found.",
            _ => CompactNetworkLookups.Failed,
        }, text);
        Assert.DoesNotContain("synthetic-secret", text);
        Assert.Empty(mobile.Evidence);
        // Package: the shared category, no result and no server text.
        Assert.Null(outcome.Result);
        Assert.Equal(code, outcome.Error!.Code);
        Assert.DoesNotContain("synthetic-secret", outcome.Error.Message);

        Assert.Equal(MobileRequests(id, Strings(failure, "requests")), mobileServer.Requests);
        Assert.Equal(Strings(failure, "requests"), packageServer.Requests);
    }
}
