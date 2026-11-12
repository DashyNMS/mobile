using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DashyNMS.Agent;
using DashyNMS.Agent.Alerts;
using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// The shared open alerts fixture (#601) served over loopback HTTP, as DashyNMS/mcp's
/// <c>SharedOpenAlertsHttpFixtureTests</c> serves it to the MCP server's <c>active_alerts</c>. Here it
/// goes to Intelligence's <c>active_alerts</c> through the app's per-chat transports
/// (<see cref="IntelligenceDeviceTransports"/>, Core's real <see cref="LibreNmsTransport"/>), and to the
/// package's <see cref="HostOpenAlerts"/> over Core's real client on mobile's Core pin.
/// </summary>
/// <remarks>
/// Nothing in Core is faked: the <c>/faults</c> probe, its fallback to <c>/alerts</c>, status codes,
/// bodies, retries and the timeout reach each side through Core's own parsing and retry policy, and
/// each side's server counts what reached it. Mobile's <c>active_alerts</c> takes only a device id, so
/// the severity, location and paging cases run through the package alone: that shows the package on
/// mobile's Core pin agrees with MCP, not that mobile offers those filters.
/// </remarks>
public sealed partial class SharedOpenAlertsHttpTests
{
    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OpenAlerts");

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

    private static int? Number(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;

    private static string[] Strings(JsonElement element, string name)
        => element.GetProperty(name).EnumerateArray().Select(r => r.GetString()!).ToArray();

    private static int[] Ints(JsonElement element, string name)
        => element.GetProperty(name).EnumerateArray().Select(i => i.GetInt32()).ToArray();

    private static JsonElement Case(string name) => Cases.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);

    /// <summary>Cases mobile can ask: no severity, location or page size, which its lookup doesn't take.</summary>
    private static bool MobileAsks(JsonElement c) => Text(c, "severity") is null && Text(c, "location") is null && Number(c, "limit") is null;

    public static TheoryData<string> AllCases()
    {
        var data = new TheoryData<string>();
        foreach (var c in Cases.GetProperty("cases").EnumerateArray())
            data.Add(c.GetProperty("name").GetString()!);
        return data;
    }

    public static TheoryData<string> MobileCases()
    {
        var data = new TheoryData<string>();
        foreach (var c in Cases.GetProperty("cases").EnumerateArray().Where(MobileAsks))
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

    private static Dictionary<string, Answer> Routes(string server)
    {
        var routes = new Dictionary<string, Answer>(StringComparer.Ordinal);
        foreach (var route in Cases.GetProperty("routes").GetProperty(server).EnumerateObject())
            routes[route.Name] = new(200, File.ReadAllText(Path.Combine(FixtureDirectory, route.Value.GetString()!)));
        return routes;
    }

    private static string[] Requests(JsonElement c)
        => (c.TryGetProperty("requests", out var own) ? own : Cases.GetProperty("requests").GetProperty(c.GetProperty("server").GetString()!))
            .EnumerateArray().Select(r => r.GetString()!).ToArray();

    private static LibreNmsConnection Connection(Uri root) => new(root, FixtureServer.Token, timeoutSeconds: TimeoutSeconds);

    /// <summary>Mobile's active_alerts as the app builds it: a session on this address and the app's per-chat transports.</summary>
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
            Alerts = new CompactNetworkLookups(Fakes.Client(), settings, session, deviceTransport: transports.Open)
                .Create(found => Evidence.AddRange(found)).Single(l => l.Name == "active_alerts");
        }

        public IntelligenceLookup Alerts { get; }

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
            Alerts = HostOpenAlerts.Create(client, TimeProvider.System, serverTimestampsAreUtc: true);
        }

        public HostOpenAlerts Alerts { get; }

        public void Dispose()
        {
            Alerts.Dispose();
            client.Dispose();
            transport.Dispose();
        }
    }

    private static string StateText(OpenAlertState state) => state switch
    {
        OpenAlertState.PartlyAcknowledged => "partly_acknowledged",
        OpenAlertState.Acknowledged => "acknowledged",
        _ => "active",
    };

    private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";

    /// <summary>One shown group as mobile words it: state, representative and fault count.</summary>
    private sealed record Shown(string State, int Acknowledged, string Representative, int Faults);

    private static List<Shown> Rows(string text) => [.. text.Split('\n').Select(l => Row().Match(l)).Where(m => m.Success).Select(m =>
    {
        var faults = m.Groups["faults"].Success ? int.Parse(m.Groups["faults"].Value, CultureInfo.InvariantCulture) : 1;
        var state = m.Groups["state"].Value;
        return state.StartsWith("partly", StringComparison.Ordinal)
            ? new Shown("partly_acknowledged", int.Parse(m.Groups["ack"].Value, CultureInfo.InvariantCulture), m.Groups["key"].Value, faults)
            : new Shown(state, state == "acknowledged" ? faults : 0, m.Groups["key"].Value, faults);
    })];

    [GeneratedRegex(@"^(?:Critical|Warning|OK|Unknown); (?<state>active|acknowledged|partly acknowledged \((?<ack>\d+) of \d+\)); .*? \[\[alert:\d+\]\] \((?<key>(?:fault|alert):\d+)\) on .*?(?:; (?<faults>\d+) faults(?::.*)?)?(?:; for .*)?$")]
    private static partial Regex Row();

    [Theory]
    [MemberData(nameof(MobileCases))]
    public async Task Mobile_and_the_package_group_the_fixture_identically_over_HTTP(string name)
    {
        var c = Case(name);
        var routes = Routes(c.GetProperty("server").GetString()!);
        using var mobileServer = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var packageServer = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var mobile = new Mobile(mobileServer.Root);
        using var package = new Package(packageServer.Root);
        var device = Number(c, "device");

        var text = await mobile.Alerts.Run(device?.ToString(CultureInfo.InvariantCulture) ?? "", default);
        var outcome = await package.Alerts.ListAsync(new(device));

        var totals = c.GetProperty("totals");
        var groups = totals.GetProperty("alert_groups").GetInt32();
        Assert.StartsWith(groups == 0 ? "Open alerts: 0."
            : $"Open alerts: {groups} ({Count(totals.GetProperty("faults").GetInt32(), "fault")}) on {Count(totals.GetProperty("devices").GetInt32(), "device")}; " +
              $"{totals.GetProperty("critical").GetInt32()} critical, {totals.GetProperty("warning").GetInt32()} warning.", text);

        var rows = Rows(text);
        Assert.Equal(Strings(c, "representatives"), rows.Select(r => r.Representative));
        Assert.Equal(Strings(c, "states"), rows.Select(r => r.State));
        Assert.Equal(Ints(c, "faultCounts"), rows.Select(r => r.Faults));
        Assert.Equal(Ints(c, "acknowledgedCounts"), rows.Select(r => r.Acknowledged));
        Assert.DoesNotContain("more results omitted", text);

        var result = outcome.Result!;
        Assert.Equal(Strings(c, "groups"), result.Groups.Select(g => g.Key.ToString()));
        Assert.Equal(Strings(c, "representatives"), result.Groups.Select(g => g.Representative.ToString()));

        // Evidence only for what was shown: each group's representative under its marker key, typed by source.
        var alertEvidence = mobile.Evidence.Where(e => e.Reference.Key.StartsWith("alert:", StringComparison.Ordinal)).ToList();
        Assert.Equal(Strings(c, "representatives"), alertEvidence.Select(e => $"{e.Reference.Object!.Kind}:{e.Reference.Object.Id}"));

        Assert.Equal(Requests(c), mobileServer.Requests);
        Assert.Equal(Requests(c), packageServer.Requests);
    }

    [Theory]
    [MemberData(nameof(AllCases))]
    public async Task The_package_on_mobiles_Core_gives_the_shared_groups_for_every_case(string name)
    {
        var c = Case(name);
        var routes = Routes(c.GetProperty("server").GetString()!);
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var package = new Package(server.Root);

        var outcome = await package.Alerts.ListAsync(new(Number(c, "device"), Text(c, "severity"), Location: Text(c, "location"),
            Limit: Number(c, "limit") ?? OpenAlerts.DefaultLimit));

        if (Text(c, "code") is { } code)
        {
            Assert.Null(outcome.Result);
            Assert.Equal(Enum.Parse<HostFailure>(code), outcome.Error!.Code);
        }
        else
        {
            var result = outcome.Result!;
            Assert.Null(outcome.Error);
            Assert.Equal(Strings(c, "groups"), result.Groups.Select(g => g.Key.ToString()));
            Assert.Equal(Strings(c, "representatives"), result.Groups.Select(g => g.Representative.ToString()));
            Assert.Equal(Ints(c, "faultCounts"), result.Groups.Select(g => g.FaultCount));
            Assert.Equal(c.GetProperty("faults").EnumerateArray().Select(g => g.EnumerateArray().Select(f => f.GetInt32()).ToArray()),
                result.Groups.Select(g => g.Faults.Select(f => f.Key.Id).ToArray()));
            Assert.Equal(Ints(c, "acknowledgedCounts"), result.Groups.Select(g => g.AcknowledgedCount));
            Assert.Equal(Strings(c, "states"), result.Groups.Select(g => StateText(g.State)));
            var totals = c.GetProperty("totals");
            Assert.Equal(totals.GetProperty("alert_groups").GetInt32(), result.Totals.AlertGroups);
            Assert.Equal(totals.GetProperty("faults").GetInt32(), result.Totals.Faults);
            Assert.Equal(totals.GetProperty("devices").GetInt32(), result.Totals.Devices);
            Assert.Equal(Number(c, "nextOffset"), result.Page.NextOffset);
        }

        Assert.Equal(Requests(c), server.Requests);
    }

    [Theory]
    [MemberData(nameof(TransportFailures))]
    public async Task Mobile_and_the_package_fail_alike_when_the_alert_routes_fail(string name)
    {
        var failure = Transport.GetProperty("failures").EnumerateArray().Single(f => f.GetProperty("name").GetString() == name);
        var code = Enum.Parse<HostFailure>(failure.GetProperty("code").GetString()!);
        var devices = new Answer(200, File.ReadAllText(Path.Combine(FixtureDirectory, "devices.json")));
        var failing = Text(failure, "behaviour") == "hang" ? Answer.Hang
            : new Answer(failure.GetProperty("status").GetInt32(),
                Transport.GetProperty("bodies").GetProperty(failure.GetProperty("body").GetString()!).GetString()!);
        using var mobileServer = new FixtureServer(path => path == "devices" ? devices : failing);
        using var packageServer = new FixtureServer(path => path == "devices" ? devices : failing);
        using var mobile = new Mobile(mobileServer.Root);
        using var package = new Package(packageServer.Root);

        // Side by side: a request that never gets an answer costs the full timeout on each.
        var mobileCall = mobile.Alerts.Run("", default);
        var packageCall = package.Alerts.ListAsync(new());
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

        Assert.Equal(Strings(failure, "requests"), mobileServer.Requests);
        Assert.Equal(Strings(failure, "requests"), packageServer.Requests);
    }

    /// <summary>
    /// Fault 12 on a 26.10 server and alert 12 on an older one are different things. Each answer's
    /// evidence names its own source, so a chip can only ever open the one the model was shown.
    /// </summary>
    [Fact]
    public async Task A_fault_and_a_legacy_alert_numbered_12_keep_their_own_identity()
    {
        var faults = Routes("faults");
        var legacy = Routes("legacy");
        using var faultServer = new FixtureServer(path => faults.GetValueOrDefault(path));
        using var legacyServer = new FixtureServer(path => legacy.GetValueOrDefault(path));
        using var onFaults = new Mobile(faultServer.Root);
        using var onLegacy = new Mobile(legacyServer.Root);

        var faultText = await onFaults.Alerts.Run("", default);
        var legacyText = await onLegacy.Alerts.Run("", default);

        // Both rows read the same; only the evidence tells them apart.
        Assert.Contains("[[alert:12]] on", faultText);
        Assert.Contains("[[alert:12]] on", legacyText);
        Assert.DoesNotContain("fault:12", faultText);
        Assert.DoesNotContain("(alert:12)", legacyText);
        Assert.Equal("fault", onFaults.Evidence.Single(e => e.Reference.Key == "alert:12").Reference.Object!.Kind);
        Assert.Equal("alert", onLegacy.Evidence.Single(e => e.Reference.Key == "alert:12").Reference.Object!.Kind);
        // The group key never stands in for an id.
        Assert.DoesNotContain("device:2/rule:4", faultText);
    }
}
