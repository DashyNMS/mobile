using DashyNMS.Agent;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Unimus;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

public sealed class IntelligenceIntegrationTests
{
    [Fact]
    public async Task Configured_clients_use_the_captured_service_addresses_and_credentials_and_retire_on_change()
    {
        var basicA = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("reader:synthetic-a"));
        var basicB = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("reader:synthetic-b"));
        static Answer? AnswerFor(string path) => path switch
        {
            "/api/streams" => new(200, """{"streams":[]}"""),
            "/api/v2/devices" => new(200, """{"data":[],"paginator":{"totalPages":1}}"""),
            _ => null,
        };
        using var a = new FixtureServer(AnswerFor, r => r.Headers["Authorization"] is { } auth && (auth == basicA || auth == "Bearer synthetic-a"));
        using var b = new FixtureServer(AnswerFor, r => r.Headers["Authorization"] is { } auth && (auth == basicB || auth == "Bearer synthetic-b"));
        var saved = new AppSettings
        {
            Graylog = new() { Enabled = true, Server = a.Root.ToString(), Username = "reader", QueryField = "source" },
            Unimus = new() { Enabled = true, Url = a.Root.ToString(), MyDomain = "example.net" },
            IntelligenceConfigText = AppSettings.IntelligenceConfigTextSummary,
        };
        var settings = Fakes.Settings(saved);
        var secrets = Fakes.Secrets();
        var logs = new GraylogSetup(Substitute.For<IGraylogApi>(), new SecureGraylogPasswordProtector(secrets), settings, secrets, NullLogger<GraylogSetup>.Instance);
        var configs = new UnimusSetup(Substitute.For<IUnimusApi>(), new SecureUnimusTokenProtector(secrets), settings, secrets, TimeProvider.System, NullLogger<UnimusSetup>.Instance);
        logs.Apply("synthetic-a"); configs.Apply("synthetic-a");
        var factory = new IntelligenceIntegrations(logs, configs, settings);
        await factory.PrepareAsync();
        using var first = factory.Capture();
        Assert.Empty(await first.Graylog.GetStreamsAsync());
        Assert.Empty(await first.Unimus.ListAllDevicesAsync());
        Assert.Empty(b.Requests);
        saved.Graylog.Server = b.Root.ToString(); saved.Unimus.Url = b.Root.ToString();
        saved.Unimus.MyDomain = "changed.example.net";
        saved.Graylog.QueryField = "host"; saved.Graylog.MatchAnyAddress = true;
        saved.IntelligenceConfigText = AppSettings.IntelligenceConfigTextFull;
        logs.Apply("synthetic-b"); configs.Apply("synthetic-b");
        Assert.Equal("example.net", first.Options.UnimusMyDomain);
        Assert.Equal(ConfigText.Summary, first.Options.ConfigText);
        Assert.Equal("source", first.Options.GraylogQueryField);
        Assert.False(first.Options.GraylogMatchAnyAddress);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.Graylog.GetStreamsAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.Unimus.ListAllDevicesAsync());
        using var next = factory.Capture();
        Assert.Equal("changed.example.net", next.Options.UnimusMyDomain);
        Assert.Empty(await next.Graylog.GetStreamsAsync());
        Assert.Empty(await next.Unimus.ListAllDevicesAsync());
        Assert.Equal(new[] { "/api/streams", "/api/streams", "/api/v2/devices" }, a.Requests);
        Assert.Equal(new[] { "/api/streams", "/api/v2/devices" }, b.Requests);
        configs.ForgetToken();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next.Unimus.ListAllDevicesAsync());
        Assert.Equal(2, b.Requests.Length);
    }

    [Theory]
    [InlineData("config")]
    [InlineData("field")]
    [InlineData("addresses")]
    [InlineData("domain")]
    public void Saved_settings_transitions_retire_the_attempt_even_after_restoring_the_original_value(string setting)
    {
        var saved = new AppSettings { IntelligenceConfigText = AppSettings.IntelligenceConfigTextFull };
        var settings = Fakes.Settings(saved);
        var secrets = Fakes.Secrets();
        var logs = new GraylogSetup(Substitute.For<IGraylogApi>(), new SecureGraylogPasswordProtector(secrets), settings, secrets, NullLogger<GraylogSetup>.Instance);
        var configs = new UnimusSetup(Substitute.For<IUnimusApi>(), new SecureUnimusTokenProtector(secrets), settings, secrets, TimeProvider.System, NullLogger<UnimusSetup>.Instance);
        using var factory = new IntelligenceIntegrations(logs, configs, settings);
        using var first = factory.Capture();
        var originalField = saved.Graylog.QueryField;
        var originalAddresses = saved.Graylog.MatchAnyAddress;
        if (setting == "domain") saved.Unimus.MyDomain = "example.net";
        if (setting == "config") saved.IntelligenceConfigText = AppSettings.IntelligenceConfigTextOff;
        if (setting == "field") saved.Graylog.QueryField = "changed_source";
        if (setting == "addresses") saved.Graylog.MatchAnyAddress = !originalAddresses;
        settings.Changed += Raise.Event<EventHandler<AppSettings>>(settings, saved);
        saved.Unimus.MyDomain = null;
        saved.IntelligenceConfigText = AppSettings.IntelligenceConfigTextFull;
        saved.Graylog.QueryField = originalField;
        saved.Graylog.MatchAnyAddress = originalAddresses;
        settings.Changed += Raise.Event<EventHandler<AppSettings>>(settings, saved);
        Assert.ThrowsAny<OperationCanceledException>(() => first.Current());
        using var next = factory.Capture();
        next.Current();
        factory.Dispose();
        Assert.ThrowsAny<OperationCanceledException>(() => next.Current());
    }

    [Fact]
    public async Task Read_only_clients_refuse_reconfiguration_actions_and_reads_after_disposal()
    {
        using var clients = new IntelligenceIntegrationClients(null, null, new AppSettings());
        Assert.False(clients.Graylog.IsConfigured);
        Assert.False(clients.Unimus.IsConfigured);
        Assert.Equal(ConfigText.Summary, clients.Options.ConfigText);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => clients.Graylog.Configure(null!));
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => clients.Graylog.Clear());
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => clients.Unimus.Configure(null!));
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => clients.Unimus.Clear());
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => clients.Unimus.TriggerBackupAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => clients.Unimus.TestConnectionAsync());
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => clients.Graylog.TestConnectionAsync());
        clients.Dispose(); clients.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clients.Graylog.GetStreamsAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clients.Unimus.GetLatestBackupAsync(1));
    }
}
