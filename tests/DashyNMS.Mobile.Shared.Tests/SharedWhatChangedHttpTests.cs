using System.Text;
using System.Text.Json;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Unimus;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class SharedWhatChangedHttpTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);
    private static Dictionary<string, Answer> NmsRoutes(bool emptyHistory = false)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EventHistory", "routes.json")));
        var routes = json.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => new Answer(200, p.Value.GetRawText()));
        if (emptyHistory)
        {
            routes["logs/eventlog/1"] = new(200, """{"logs":[]}""");
            routes["logs/alertlog/1"] = new(200, """{"logs":[]}""");
            routes["devices/1/outages"] = new(200, """{"outages":[]}""");
        }
        return routes;
    }
    private static Answer? UnimusAnswer(string path, bool baseline = true)
    {
        if (path == "/api/v2/devices") return new(200, """{"data":[{"id":42,"address":"192.0.2.1"}]}""");
        if (path == "/api/v2/devices/42/backups") return new(200, JsonSerializer.Serialize(new
        {
            data = baseline ? new[] { new { id = 102, validSince = Now.AddMinutes(-30).ToUnixTimeSeconds() }, new { id = 101, validSince = Now.AddHours(-25).ToUnixTimeSeconds() } }
                : new[] { new { id = 102, validSince = Now.AddMinutes(-30).ToUnixTimeSeconds() } },
        }));
        if (path == "/api/v2/backups/diff") return new(200, """{"data":{"lineGroups":[{"type":"CHANGED","originalLines":[{"number":1,"text":"description before"}],"revisedLines":[{"number":1,"text":"description TEST_FULL_TEXT"},{"number":2,"text":"password=synthetic-secret"}]}]}}""");
        return null;
    }
    private static readonly Answer Logs = new(200, """{"total_results":4,"messages":[{"message":{"timestamp":"2026-10-17T09:50:00Z","source":"core-sw1.example.net","level":4,"message":"Synthetic log sample"}}]}""");
    private static FixtureServer LogServer(Func<string, Answer?> answer) => new(answer,
        r => r.Headers["Authorization"] == "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("reader:synthetic-password")));
    private static FixtureServer ConfigServer(Func<string, Answer?> answer) => new(answer, r => r.Headers["Authorization"] == "Bearer synthetic-token");

    private sealed class Harness : IDisposable
    {
        private readonly LibreNmsTransport transport = new(NullLogger<LibreNmsTransport>.Instance);
        private readonly IntelligenceDeviceTransports transports;
        private readonly IntelligenceLookupSet set;
        internal Harness(Uri nms, Uri? logs = null, Uri? configs = null, string mode = AppSettings.IntelligenceConfigTextSummary, bool allAddresses = false)
        {
            Settings = new()
            {
                Graylog = new() { Enabled = logs is not null, Server = logs?.ToString(), Username = "reader", MatchAnyAddress = allAddresses },
                Unimus = new() { Enabled = configs is not null, Url = configs?.ToString() }, IntelligenceConfigText = mode,
            };
            var settings = Fakes.Settings(Settings);
            var secrets = Fakes.Secrets();
            var logSetup = new GraylogSetup(Substitute.For<IGraylogApi>(), new SecureGraylogPasswordProtector(secrets), settings, secrets, NullLogger<GraylogSetup>.Instance);
            var configSetup = new UnimusSetup(Substitute.For<IUnimusApi>(), new SecureUnimusTokenProtector(secrets), settings, secrets, TimeProvider.System, NullLogger<UnimusSetup>.Instance);
            logSetup.Apply("synthetic-password"); configSetup.Apply("synthetic-token");
            var session = Fakes.Session();
            session.Connection.Returns(new LibreNmsConnection(nms, FixtureServer.Token));
            transports = new(transport, settings, session);
            set = new CompactNetworkLookups(Fakes.Client(), settings, session, new FakeTimeProvider(Now), deviceTransport: transports.Open,
                integrations: new(logSetup, configSetup, settings)).Open(Evidence.AddRange);
            Changes = set.Lookups.Single(l => l.Name == "what_changed");
        }
        internal AppSettings Settings { get; }
        internal List<IntelligenceEvidence> Evidence { get; } = [];
        internal IntelligenceLookup Changes { get; }
        public void Dispose() { set.Dispose(); transports.Dispose(); transport.Dispose(); }
    }
    [Fact]
    public async Task Unconfigured_services_are_explicit_and_do_not_hide_the_shared_timeline()
    {
        var routes = NmsRoutes();
        using var nms = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var mobile = new Harness(nms.Root);
        var text = await mobile.Changes.Run("1", default);
        Assert.StartsWith("Unimus not configured. Graylog not configured.", text);
        Assert.Contains("Interface 1/1/7 went down", text);
        Assert.Contains("[[device:1]]", text);
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
    }
    [Theory]
    [InlineData(AppSettings.IntelligenceConfigTextSummary, false)]
    [InlineData(AppSettings.IntelligenceConfigTextFull, true)]
    public async Task Config_changes_respect_the_saved_text_choice_and_redact_full_text(string mode, bool full)
    {
        var routes = NmsRoutes(emptyHistory: true);
        using var nms = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var logs = LogServer(_ => Logs);
        using var configs = ConfigServer(p => UnimusAnswer(p));
        using var mobile = new Harness(nms.Root, logs.Root, configs.Root, mode);
        var text = await mobile.Changes.Run("1", default);
        Assert.StartsWith("Unimus config changed. Graylog 4 messages.", text);
        Assert.Contains("Config changed lines 3; removed 1; added 2.", text);
        Assert.Equal(full, text.Contains("TEST_FULL_TEXT", StringComparison.Ordinal));
        Assert.DoesNotContain("synthetic-secret", text);
        if (!full) Assert.Contains("Config text left out by the saved Summary choice", text);
        Assert.Contains("/api/v2/backups/diff", configs.Requests);
        Assert.Contains("/api/search/universal/relative", logs.Requests);
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
    }
    [Fact]
    public async Task A_config_version_without_a_baseline_stays_unknown_while_logs_remain_usable()
    {
        var routes = NmsRoutes(emptyHistory: true);
        using var nms = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var logs = LogServer(_ => Logs);
        using var configs = ConfigServer(p => UnimusAnswer(p, baseline: false));
        using var mobile = new Harness(nms.Root, logs.Root, configs.Root);
        var text = await mobile.Changes.Run("1", default);
        Assert.StartsWith("Unimus config change unknown. Graylog 4 messages.", text);
        Assert.Contains("no earlier backup", text);
        Assert.DoesNotContain("no config change", text);
        Assert.DoesNotContain("/api/v2/backups/diff", configs.Requests);
    }
    [Fact]
    public async Task Config_off_makes_no_Unimus_request_and_missing_address_coverage_skips_Graylog()
    {
        var routes = NmsRoutes(emptyHistory: true);
        routes["devices/1/ip"] = new(403, """{"message":"password=synthetic-secret"}""");
        using var nms = new FixtureServer(p => routes.GetValueOrDefault(p));
        using var logs = LogServer(_ => Logs);
        using var configs = ConfigServer(p => UnimusAnswer(p));
        using var mobile = new Harness(nms.Root, logs.Root, configs.Root, AppSettings.IntelligenceConfigTextOff, allAddresses: true);
        var text = await mobile.Changes.Run("1", default);
        Assert.StartsWith("Log source unavailable. Unimus config data off. Graylog unavailable.", text);
        Assert.Empty(configs.Requests);
        Assert.Empty(logs.Requests);
        Assert.Contains("devices/1/ip", nms.Requests);
        Assert.DoesNotContain("synthetic-secret", text);
    }
    [Fact]
    public async Task Changing_config_privacy_during_an_inflight_read_discards_its_result_and_evidence()
    {
        var routes = NmsRoutes(emptyHistory: true);
        using var nms = new FixtureServer(p => routes.GetValueOrDefault(p));
        Harness? mobile = null;
        using var logs = LogServer(_ => { mobile!.Settings.IntelligenceConfigText = AppSettings.IntelligenceConfigTextOff; return Logs; });
        using var owned = mobile = new Harness(nms.Root, logs.Root);
        Assert.Equal(CompactNetworkLookups.Failed, await mobile.Changes.Run("1", default));
        Assert.Empty(mobile.Evidence);
    }
    [Fact]
    public async Task Change_profile_permits_only_interface_address_reads_of_ports()
    {
        var opened = false;
        using var client = new ConnectionBoundClient(new SessionScope(0, new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token")),
            _ => { opened = true; throw new InvalidOperationException(); }, changes: true);
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Ports.ListForDeviceAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Ports.ListAllStatusAsync());
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Ports.ListIpAddressesAsync(0));
        Assert.False(opened);
    }
}
