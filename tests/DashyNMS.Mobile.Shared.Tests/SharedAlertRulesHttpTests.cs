using System.Text.Json;
using DashyNMS.Agent;
using DashyNMS.Agent.Alerts;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class SharedAlertRulesHttpTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);
    private static Dictionary<string, Answer> Routes()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "AlertRules", "routes.json")));
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
            var settings = Fakes.Settings(new AppSettings { ServerTimestampsAreUtc = true });
            transports = new(transport, settings, session);
            set = new CompactNetworkLookups(Fakes.Client(), settings, session, new FakeTimeProvider(Now), deviceTransport: transports.Open).Open(Evidence.AddRange);
        }
        internal List<IntelligenceEvidence> Evidence { get; } = [];
        internal IntelligenceLookup Tool(string name) => set.Lookups.Single(l => l.Name == name);
        public void Dispose() { set.Dispose(); transports.Dispose(); transport.Dispose(); }
    }
    private sealed class Package : IDisposable
    {
        private readonly LibreNmsTransport transport = new(NullLogger<LibreNmsTransport>.Instance);
        private readonly LibreNmsClient client;
        internal Package(Uri root)
        {
            client = new(transport, NullLogger<LibreNmsClient>.Instance);
            client.Connect(new LibreNmsConnection(root, FixtureServer.Token));
            Host = HostAlertRules.Create(client, new FakeTimeProvider(Now), new AgentOptions { ServerTimestampsAreUtc = true });
        }
        internal HostAlertRules Host { get; }
        public void Dispose() { Host.Dispose(); client.Dispose(); transport.Dispose(); }
    }
    [Fact]
    public async Task Details_keep_the_shared_trigger_history_and_source_qualified_identity()
    {
        var routes = Routes();
        using var mobileServer = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var packageServer = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(mobileServer.Root);
        using var package = new Package(packageServer.Root);
        var result = (await package.Host.ReadDetailsAsync(new(10))).Result!;
        var text = await mobile.Tool("alert_details").Run("""{"alert_id":10}""", default);
        Assert.Equal(1, result.Data.Why.Matched);
        Assert.Equal(1, result.Data.History.FiredBeforeIn30Days);
        Assert.Contains("alert 10 [[alert:10]]: critical active", text);
        Assert.Contains("Previous firings in 30 days: 1", text);
        Assert.Contains("Rule 3: Port down", text);
        Assert.Contains("ifOperStatus=down", text);
        Assert.Equal(new[] { "alerts/10", "alerts", "devices", "faults", "logs/alertlog/1", "rules" }.Order(), mobileServer.Requests.Order());
        Assert.Equal(packageServer.Requests.Order(), mobileServer.Requests.Order());
        var alert = Assert.Single(mobile.Evidence, e => e.Reference.Key == "alert:10");
        Assert.Equal("alert", alert.Reference.Object!.Kind);
        Assert.Equal(1, alert.Reference.Object.DeviceId);
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
    }
    [Theory]
    [InlineData("{}", 4)]
    [InlineData("""{"name":"Port","severity":"critical","sort":"open"}""", 3)]
    [InlineData("""{"include_disabled":true,"sort":"raised","offset":3,"limit":1,"verbose":true}""", 6)]
    public async Task Rule_filters_sorts_and_paging_use_the_shared_operation(string arguments, int expectedRule)
    {
        var routes = Routes();
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var packageServer = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        using var package = new Package(packageServer.Root);
        using var json = JsonDocument.Parse(arguments);
        var args = json.RootElement;
        var request = new AlertRulesRequest(
            args.TryGetProperty("name", out var name) ? name.GetString() : null,
            args.TryGetProperty("severity", out var severity) ? severity.GetString() : null,
            args.TryGetProperty("include_disabled", out var disabled) && disabled.GetBoolean(),
            args.TryGetProperty("sort", out var sort) ? sort.GetString() : null,
            args.TryGetProperty("offset", out var offset) ? offset.GetInt32() : 0,
            args.TryGetProperty("limit", out var limit) ? limit.GetInt32() : 20,
            args.TryGetProperty("verbose", out var verbose) && verbose.GetBoolean());
        var result = (await package.Host.ReadRulesAsync(request)).Result!;
        var text = await mobile.Tool("alert_rules").Run(arguments, default);
        Assert.Equal(expectedRule, result.Data.Rules[0].RuleId);
        Assert.Equal(packageServer.Requests.Order(), server.Requests.Order());
        Assert.Contains($"Rule {expectedRule}:", text);
        Assert.Contains("open ", text);
        Assert.Contains("raised in 7 days ", text);
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
        Assert.Empty(mobile.Evidence); // Rule IDs are facts; they are not navigable alert IDs.
        Assert.Equal(new[] { "rules", "alert_templates", "faults", "alerts", "logs/alertlog", "devicegroups", "resources/locations", "system", "devices" }.Order(), server.Requests.Order());
    }
    [Fact]
    public async Task Fault_and_legacy_alerts_with_the_same_number_cannot_change_identity_in_a_follow_up()
    {
        var routes = Routes();
        // The ordinary alerts call supplies legacy alert 10 first. The dedicated details
        // client then discovers a faults endpoint. The source expectation must reject it.
        var fault = routes["alerts/10"].Body;
        var faultProbe = 0;
        using var server = new FixtureServer(p => p == "faults"
            ? Interlocked.Increment(ref faultProbe) == 1 ? null : new Answer(200, """{"alerts":[]}""")
            : p == "faults/10" ? new Answer(200, fault) : routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var open = await mobile.Tool("active_alerts").Run("{}", default);
        Assert.Contains("[[alert:10]]", open);
        Assert.Equal("alert", mobile.Evidence.Single(e => e.Reference.Key == "alert:10").Reference.Object!.Kind);
        mobile.Evidence.Clear();
        Assert.Equal(CompactNetworkLookups.Failed, await mobile.Tool("alert_details").Run("""{"alert_id":10}""", default));
        Assert.Empty(mobile.Evidence);
    }

    [Fact]
    public async Task Detail_reads_from_faults_keep_their_navigation_source()
    {
        var routes = Routes();
        routes["faults"] = new(200, """{"alerts":[]}""");
        routes["faults/10"] = routes["alerts/10"];
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Tool("alert_details").Run("""{"alert_id":10}""", default);
        Assert.Contains("fault 10 [[alert:10]]", text);
        Assert.Equal("fault", Assert.Single(mobile.Evidence, e => e.Reference.Key == "alert:10").Reference.Object!.Kind);
    }

    [Theory]
    [InlineData("logs/alertlog/1", "history")]
    [InlineData("rules", "rule")]
    public async Task Optional_detail_failures_are_named_and_history_is_never_invented(string path, string source)
    {
        var routes = Routes();
        routes[path] = new(403, """{"message":"password=synthetic-secret"}""");
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Tool("alert_details").Run("""{"alert_id":10}""", default);
        Assert.StartsWith("Unavailable: " + source, text);
        if (source == "history") Assert.Contains("Previous firings unknown", text);
        Assert.DoesNotContain("synthetic-secret", text);
    }
    [Fact]
    public async Task Required_sources_and_invalid_arguments_fail_without_inventing_rows_or_evidence()
    {
        var routes = Routes();
        routes["rules"] = new(403, """{"message":"password=synthetic-secret"}""");
        using var server = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Mobile(server.Root);
        Assert.Equal(CompactNetworkLookups.Refused, await mobile.Tool("alert_rules").Run("{}", default));
        Assert.Empty(mobile.Evidence);
        var before = server.Requests.Length;
        Assert.StartsWith("The lookup wasn't run:", await mobile.Tool("alert_details").Run("""{"alert_id":0}""", default));
        Assert.StartsWith("The lookup wasn't run:", await mobile.Tool("alert_rules").Run("""{"sort":"invented"}""", default));
        Assert.Equal(before, server.Requests.Length);
    }
    [Theory]
    [InlineData("alert_details", "{\"alert_id\":10}")]
    [InlineData("alert_rules", "{}")]
    public async Task Pending_alert_operations_obey_deadline_cancellation_and_retirement(string name, string arguments)
    {
        var time = new FakeTimeProvider(Now);
        var session = Fakes.Session();
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token"));
        var lookups = new CompactNetworkLookups(Fakes.Client(), Fakes.Settings(), session, time, deviceTransport: _ => new PendingTransport());
        var evidence = new List<IntelligenceEvidence>();
        using var set = lookups.Open(evidence.AddRange);
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
            => new TaskCompletionSource<JsonDocument>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        public Task<string> SendRawAsync(string relativeUrl, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    [Fact]
    public async Task Alert_rule_profile_refuses_unreviewed_reads_and_all_writes_before_transport()
    {
        var opened = false;
        using var client = new ConnectionBoundClient(new SessionScope(0, new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token")),
            _ => { opened = true; throw new InvalidOperationException(); }, alertRules: true);
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Alerts.ListAsync(AlertQuery.All));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Alerts.GetAsync(10));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Alerts.AcknowledgeAsync(10));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Rules.GetAsync(3));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Rules.DeleteAsync(3));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.AlertTemplates.CreateAsync(null!));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Locations.DeleteAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Logs.ListAlertLogAsync(null, 5000));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Logs.ListEventLogAsync(null));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.GetAsync("1"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.IsUnderMaintenanceAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.DeviceGroups.GetMemberDeviceIdsAsync(1));
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Arp);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Health);
        Assert.False(opened);
    }
}
