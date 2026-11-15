using System.Text.Json;
using DashyNMS.Agent;
using DashyNMS.Agent.Unimus;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Unimus;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class SharedConfigLookupsHttpTests
{
    internal static readonly DateTimeOffset Now = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);
    internal static Dictionary<string, Answer> Routes()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ConfigLookups", "routes.json")));
        return json.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => new Answer(200, p.Value.GetRawText()));
    }
    internal static FixtureServer Server(Func<string, Answer?>? change = null)
    {
        var routes = Routes();
        return new(path => change?.Invoke(path) ?? routes.GetValueOrDefault(path), r => r.Headers["Authorization"] == "Bearer synthetic-fixture-token");
    }
    internal static FixtureServer NmsServer() => new(path => path switch
    {
        "devices" => new(200, """{"devices":[{"device_id":1,"hostname":"core-sw1.example.net","display":"Core switch","ip":"192.0.2.1","status":1,"disabled":0,"location":"Lab"}]}"""),
        "devicegroups" => new(200, """{"groups":[{"id":7,"name":"Core"}]}"""),
        "devicegroups/7" => new(200, """{"devices":[{"device_id":1}]}"""),
        _ => null,
    });
    internal sealed class Mobile : IDisposable
    {
        private readonly LibreNmsTransport transport = new(NullLogger<LibreNmsTransport>.Instance);
        private readonly IntelligenceDeviceTransports transports;
        private readonly IntelligenceLookupSet set;
        private readonly IntelligenceIntegrations integrations;
        private readonly ISettingsStore settingsStore;
        internal Mobile(Uri nms, Uri? configs, string mode = AppSettings.IntelligenceConfigTextSummary)
        {
            Settings = new() { Unimus = new() { Enabled = configs is not null, Url = configs?.ToString() }, IntelligenceConfigText = mode };
            var settings = settingsStore = Fakes.Settings(Settings);
            var secrets = Fakes.Secrets();
            var logs = new GraylogSetup(Substitute.For<IGraylogApi>(), new SecureGraylogPasswordProtector(secrets), settings, secrets, NullLogger<GraylogSetup>.Instance);
            Configs = new UnimusSetup(Substitute.For<IUnimusApi>(), new SecureUnimusTokenProtector(secrets), settings, secrets, TimeProvider.System, NullLogger<UnimusSetup>.Instance);
            logs.Apply(null); Configs.Apply("synthetic-fixture-token");
            var session = Fakes.Session();
            session.Connection.Returns(new LibreNmsConnection(nms, FixtureServer.Token));
            transports = new(transport, settings, session);
            integrations = new(logs, Configs, settings);
            set = new CompactNetworkLookups(Fakes.Client(), settings, session, new FakeTimeProvider(Now), deviceTransport: transports.Open,
                integrations: integrations).Open(Evidence.AddRange);
        }
        internal AppSettings Settings { get; }
        internal UnimusSetup Configs { get; }
        internal List<IntelligenceEvidence> Evidence { get; } = [];
        internal IReadOnlyList<IntelligenceLookup> Tools => set.Lookups;
        internal IntelligenceLookup Tool(string name) => Tools.Single(l => l.Name == name);
        internal void SaveSettings() => settingsStore.Changed += Raise.Event<EventHandler<AppSettings>>(settingsStore, Settings);
        public void Dispose() { set.Dispose(); integrations.Dispose(); transports.Dispose(); transport.Dispose(); }
    }

    [Theory]
    [InlineData("config_backup", AppSettings.IntelligenceConfigTextFull)]
    [InlineData("config_backup", AppSettings.IntelligenceConfigTextSummary)]
    [InlineData("config_diff", AppSettings.IntelligenceConfigTextFull)]
    [InlineData("config_diff", AppSettings.IntelligenceConfigTextSummary)]
    [InlineData("recent_config_changes", AppSettings.IntelligenceConfigTextFull)]
    public async Task Config_reads_use_the_shared_HTTP_operation_and_saved_text_policy(string tool, string mode)
    {
        using var nms = NmsServer();
        using var expectedNms = NmsServer();
        using var configs = Server();
        using var expectedConfigs = Server();
        using var mobile = new Mobile(nms.Root, configs.Root, mode);
        using var api = new UnimusApi(NullLogger<UnimusApi>.Instance);
        api.Configure(new UnimusConnection(expectedConfigs.Root, "synthetic-fixture-token"));
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(expectedNms.Root, FixtureServer.Token));
        using var host = HostConfigLookups.Create(client, api, new FakeTimeProvider(Now), new() { ConfigText = mode == AppSettings.IntelligenceConfigTextFull ? ConfigText.Full : ConfigText.Summary });
        switch (tool)
        {
            case "config_backup":
                var backup = (await host.ReadBackupAsync(new(1))).Result!;
                Assert.Equal(901, backup.Data.Backup.Id);
                Assert.Equal(4, backup.Data.Lines.Total);
                break;
            case "config_diff":
                var diff = (await host.ReadDiffAsync(new(1))).Result!;
                Assert.Equal(900, diff.Data.From.Id);
                Assert.Equal(901, diff.Data.To.Id);
                Assert.Equal(2, diff.Data.ChangedLines);
                break;
            default:
                var changes = (await host.ReadChangesAsync(new())).Result!;
                Assert.Equal(1, changes.DevicesChecked);
                Assert.Equal(901, Assert.Single(changes.Data.Changes).NewestBackupId);
                break;
        }
        var text = await mobile.Tool(tool).Run(tool == "recent_config_changes" ? "{}" : "1", default);
        if (tool == "config_backup") Assert.Contains("Backup 901", text);
        if (tool == "config_diff") Assert.Contains("changed lines 2; removed 1; added 1", text);
        if (tool == "recent_config_changes") Assert.Contains("Unimus devices checked 1; unreadable 0", text);
        Assert.Equal(expectedConfigs.HttpRequests.Order(), configs.HttpRequests.Order());
        Assert.All(nms.Requests, path => Assert.Equal("devices", path));
        Assert.Contains(mobile.Evidence, e => e.Reference.Object is { Kind: "device", Id: 1 });
        Assert.DoesNotContain("SyntheticCommunity", text);
        Assert.DoesNotContain("SyntheticOld", text);
        Assert.DoesNotContain("SyntheticNew", text);
        Assert.DoesNotContain("synthetic-key-material", text);
        if (tool != "recent_config_changes")
        {
            Assert.Equal(mode == AppSettings.IntelligenceConfigTextFull, text.Contains(DashyNMS.Agent.Redaction.Redactor.Marker, StringComparison.Ordinal));
            if (mode == AppSettings.IntelligenceConfigTextSummary) Assert.Contains("saved Summary choice", text);
        }
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
        Assert.Equal(DashyNMS.Agent.Catalogue.ToolCatalogue.Find(tool)!.Description, mobile.Tool(tool).Purpose);
    }
    [Theory]
    [InlineData("config_backup")]
    [InlineData("config_diff")]
    [InlineData("recent_config_changes")]
    public async Task Off_stops_before_either_service_and_unconfigured_tools_are_not_offered(string tool)
    {
        using var nms = NmsServer();
        using var configs = Server();
        using var disabled = new Mobile(nms.Root, configs.Root, AppSettings.IntelligenceConfigTextOff);
        Assert.Equal(CompactNetworkLookups.ConfigOff, await disabled.Tool(tool).Run(tool == "recent_config_changes" ? "{}" : "1", default));
        Assert.Empty(nms.Requests);
        Assert.Empty(configs.Requests);
        using var absent = new Mobile(nms.Root, null);
        Assert.DoesNotContain(absent.Tools, l => l.Name == tool);
    }
    [Fact]
    public async Task Backup_line_paging_keeps_its_cursor_and_redacts_before_slicing()
    {
        using var nms = NmsServer();
        using var configs = Server();
        using var mobile = new Mobile(nms.Root, configs.Root, AppSettings.IntelligenceConfigTextFull);
        var text = await mobile.Tool("config_backup").Run("""{"device":"1","offset":1,"limit":1}""", default);
        Assert.Contains("Lines total 4; offset 1; returned 1. Next offset 2.", text);
        Assert.DoesNotContain("SyntheticCommunity", text);
        Assert.Contains("Config line 2:", text);
    }
    [Fact]
    public async Task Partial_fleet_reads_keep_unknown_coverage_and_scope_membership()
    {
        using var nms = NmsServer();
        using var configs = Server(p => p == "/api/v2/devices/40/backups" ? new(403, """{"message":"password=synthetic-secret"}""") : null);
        using var mobile = new Mobile(nms.Root, configs.Root);
        var text = await mobile.Tool("recent_config_changes").Run("""{"group":"Core"}""", default);
        Assert.StartsWith("Unimus devices checked 1; unreadable 1.", text);
        Assert.Contains("devicegroups/7", nms.Requests);
        Assert.DoesNotContain("synthetic-secret", text);
        Assert.Empty(mobile.Evidence);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_privacy_change_during_reading_discards_the_config_and_evidence(bool restoreOriginal)
    {
        using var nms = NmsServer();
        Mobile? mobile = null;
        using var configs = Server(p =>
        {
            if (p == "/api/v2/backups/diff")
            {
                mobile!.Settings.IntelligenceConfigText = AppSettings.IntelligenceConfigTextOff;
                mobile.SaveSettings();
                if (restoreOriginal)
                {
                    mobile.Settings.IntelligenceConfigText = AppSettings.IntelligenceConfigTextFull;
                    mobile.SaveSettings();
                }
            }
            return null;
        });
        using var owned = mobile = new Mobile(nms.Root, configs.Root, AppSettings.IntelligenceConfigTextFull);
        Assert.Equal(CompactNetworkLookups.Failed, await mobile.Tool("config_diff").Run("1", default));
        Assert.Empty(mobile.Evidence);
    }
    [Fact]
    public async Task A_denied_read_uses_the_fixed_refusal_without_server_text()
    {
        using var nms = NmsServer();
        using var configs = Server(_ => new(403, """{"message":"password=synthetic-secret"}"""));
        using var mobile = new Mobile(nms.Root, configs.Root);
        Assert.Equal(CompactNetworkLookups.Refused, await mobile.Tool("config_backup").Run("1", default));
        Assert.Empty(mobile.Evidence);
    }
    [Fact]
    public async Task Cancelling_a_pending_backup_releases_the_wait_without_evidence()
    {
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var nms = NmsServer();
        using var configs = Server(p =>
        {
            if (p != "/api/v2/devices/40/backups/latest") return null;
            requested.TrySetResult();
            return Answer.Hang;
        });
        using var mobile = new Mobile(nms.Root, configs.Root, AppSettings.IntelligenceConfigTextFull);
        using var cancel = new CancellationTokenSource();
        var pending = mobile.Tool("config_backup").Run("1", cancel.Token);
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Empty(mobile.Evidence);
    }
    [Fact]
    public async Task Forgetting_the_token_retires_existing_tools_before_further_requests()
    {
        using var nms = NmsServer();
        using var configs = Server();
        using var mobile = new Mobile(nms.Root, configs.Root);
        mobile.Configs.ForgetToken();
        Assert.Equal(CompactNetworkLookups.Failed, await mobile.Tool("config_diff").Run("1", default));
        Assert.Empty(nms.Requests);
        Assert.Empty(configs.Requests);
        Assert.Empty(mobile.Evidence);
    }
    [Fact]
    public async Task Config_profile_refuses_unrelated_reads_and_actions_without_opening_transport()
    {
        var opened = false;
        using var client = new ConnectionBoundClient(new SessionScope(0, new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token")),
            _ => { opened = true; throw new InvalidOperationException(); }, configs: true);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Alerts);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Ports);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Arp);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Logs);
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.GetAsync("1"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.IsUnderMaintenanceAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.DeviceGroups.DeleteAsync("Core"));
        Assert.False(opened);
    }
}
