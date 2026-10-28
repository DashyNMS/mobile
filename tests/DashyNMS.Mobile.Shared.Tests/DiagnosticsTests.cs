using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

/// <summary>What the shared diagnostics say, and how they say it (#126).</summary>
public sealed class DiagnosticsTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 5, 9, 30, 0, TimeSpan.Zero));

    public DiagnosticsTests() => _time.SetLocalTimeZone(TimeZoneInfo.Utc);

    private DiagnosticsLog NewLog() => new(null, _time, "1.1.0 (163)", "Apple iPhone16,1 · iOS 26.1");

    [Fact]
    public void Starts_by_naming_the_app_and_the_phone()
    {
        Assert.Equal("2026-10-05 09:30:00.000 App: Started - DashyNMS mobile 1.1.0 (163) on Apple iPhone16,1 · iOS 26.1", NewLog().Lines[0]);
    }

    [Fact]
    public void The_same_line_again_is_counted_not_repeated()
    {
        var log = NewLog();

        log.Note("AlertWatcher", "Alert check: no changes");
        _time.Advance(TimeSpan.FromMinutes(1));
        log.Note("AlertWatcher", "Alert check: no changes");
        log.Note("AlertWatcher", "Alert check: no changes");
        log.Note("App", "Came back");

        Assert.Equal(
        [
            "2026-10-05 09:30:00.000 AlertWatcher: Alert check: no changes × 3",
            "2026-10-05 09:31:00.000 App: Came back",
        ], log.Lines.Skip(1));
    }

    [Fact]
    public void Pages_opened_one_after_another_make_one_trail()
    {
        var log = NewLog();

        log.NotePage("Alerts");
        log.NotePage("Devices");
        log.NotePage("Devices"); // the same again adds nothing
        log.NotePage("Dashboard");
        _time.Advance(DiagnosticsLog.TrailGap + TimeSpan.FromSeconds(1));
        log.NotePage("Alerts");

        Assert.Equal(
        [
            "2026-10-05 09:30:00.000 Pages: Alerts → Devices → Dashboard",
            "2026-10-05 09:32:01.000 Pages: Alerts",
        ], log.Lines.Skip(1));
    }

    [Fact]
    public void Errors_and_slow_loads_outlast_the_routine_when_trimmed()
    {
        var log = NewLog();
        log.NoteImportant("Error [error]", "NullReferenceException: boom");
        log.NoteImportant("Slow load", "AlertRule took 3.4 s");

        for (var i = 0; i < DiagnosticsLog.MaxLines + 50; i++)
        {
            log.Note("LibreNMS", $"GET /api/v0/devices · 200 · {i} ms");
        }

        Assert.Equal(DiagnosticsLog.MaxLines, log.Lines.Count);
        Assert.Contains(log.Lines, l => l.EndsWith("NullReferenceException: boom", StringComparison.Ordinal));
        Assert.Contains(log.Lines, l => l.EndsWith("AlertRule took 3.4 s", StringComparison.Ordinal));
    }

    [Fact]
    public void Hiding_names_replaces_hosts_devices_and_addresses_the_same_way_each_time()
    {
        var redaction = new DiagnosticsRedaction(["nms.example.net"], ["core-sw-01", "edge-rtr-02"]);

        var text = redaction.Apply(
            "09:30:00.000 Open device from core-sw-01 at 10.0.0.1 via nms.example.net; core-sw-01 again, edge-rtr-02 at 10.0.0.2, "
            + "core-sw-01.lab, fe80::1ff:fe23:4567:890a and 10.0.0.1 again");

        // Longest name first, so edge-rtr-02 is device-1; a name inside a longer one goes too.
        Assert.Equal(
            "09:30:00.000 Open device from device-2 at ip-1 via host-1; device-2 again, device-1 at ip-2, "
            + "device-2.lab, ip-3 and ip-1 again",
            text);
    }

    [Fact]
    public void Requests_say_where_how_and_how_long_but_never_the_query()
    {
        var log = NewLog();
        var requests = new HttpRequestLog(log, () => "nms.example.net");
        var at = _time.GetUtcNow();

        requests.Record(new HttpRequestLog.Request("GET", "nms.example.net", "/api/v0/devices", 200, null, TimeSpan.FromMilliseconds(412), at));
        requests.Record(new HttpRequestLog.Request("GET", "graylog.example.net", "/api/search/universal/relative", 500, null, TimeSpan.FromMilliseconds(2900), at));
        requests.Record(new HttpRequestLog.Request("GET", "nms.example.net", "/api/v0/rules", null, "TaskCanceledException", TimeSpan.FromSeconds(30), at));

        Assert.Equal(
        [
            "2026-10-05 09:30:00.000 LibreNMS: GET /api/v0/devices · 200 · 412 ms",
            "2026-10-05 09:30:00.000 graylog.example.net [warning]: GET /api/search/universal/relative · 500 · 2900 ms",
            "2026-10-05 09:30:00.000 LibreNMS [warning]: GET /api/v0/rules · TaskCanceledException · 30000 ms",
        ], log.Lines.Skip(1));
        Assert.Equal(["/api/v0/rules", "/api/search/universal/relative"], requests.Slowest(at.AddMinutes(-1), 2).Select(r => r.Path));
    }

    [Fact]
    public void A_slow_first_load_names_the_slowest_requests()
    {
        var log = NewLog();
        var requests = new HttpRequestLog(log, () => "nms.example.net");
        requests.Record(new HttpRequestLog.Request("GET", "nms.example.net", "/api/v0/rules/12", 200, null, TimeSpan.FromSeconds(3.1), _time.GetUtcNow()));
        var diagnostics = new AppDiagnostics(log, Fakes.Settings(), requests, _time);

        diagnostics.LoadTook("AlertRuleViewModel", TimeSpan.FromSeconds(1.2)); // quick enough: nothing said
        diagnostics.LoadTook("AlertRuleViewModel", TimeSpan.FromSeconds(3.4));

        Assert.Equal("2026-10-05 09:30:00.000 Slow load: AlertRule took 3.4 s; slowest: GET /api/v0/rules/12 · 200 · 3100 ms", log.Lines[^1]);
    }

    [Fact]
    public void Settings_changes_say_from_what_to_what()
    {
        var log = NewLog();
        var settings = new AppSettings();
        var store = Fakes.Settings(settings);
        var diagnostics = new AppDiagnostics(log, store, time: _time);
        diagnostics.Start();

        settings.DeviceNameStyle = DeviceNameStyle.Hostname;
        settings.BackupServerAddress = "https://10.0.0.9/";
        diagnostics.SettingsChanged(settings);
        diagnostics.SettingsChanged(settings); // nothing new: nothing said

        Assert.Equal(
            $"2026-10-05 09:30:00.000 Settings: Device names: {DeviceNameStyle.SysName.ToDisplayString()} → {DeviceNameStyle.Hostname.ToDisplayString()}; Backup address: none → set",
            log.Lines[^1]);
        Assert.Equal(2, log.Lines.Count);
    }

    [Theory]
    [InlineData(Routes.DeviceDetail, "device · device 7")]
    [InlineData(Routes.AlertRules, "alertrules")]
    [InlineData(Routes.Alerts, "alerts")]
    public void Navigation_is_described_by_ids_never_names(string route, string expected)
    {
        var parameters = route == Routes.DeviceDetail
            ? new Dictionary<string, object> { [Routes.DeviceIdParameter] = 7, [Routes.DeviceNameParameter] = "core-sw-01" }
            : null;

        Assert.Equal(expected, NavigationDescription.Describe(route, parameters));
    }
}

/// <summary>On the server's backup address, and the way back (#114).</summary>
public sealed class BackupAddressStatusTests
{
    private readonly ServerFailover _failover = new();
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    public BackupAddressStatusTests()
    {
        _session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "token", backupWebRoot: new Uri("https://10.44.100.11/")));
        _failover.Configure("https://10.44.100.11/");
    }

    private void FailOver()
    {
        _failover.RecordUnreachable();
        _failover.RecordUnreachable();
    }

    [Fact]
    public void Says_so_in_desktops_words_once_on_the_backup()
    {
        var status = new BackupAddressStatus(_failover, _session, _dialogs);
        Assert.False(status.IsOnBackup);
        Assert.Equal(string.Empty, status.Explanation);

        FailOver();

        Assert.True(status.IsOnBackup);
        Assert.Equal("10.44.100.11", status.BackupHost);
        Assert.StartsWith("Connected through the backup address 10.44.100.11 since ", status.Explanation);
        Assert.EndsWith(". nms.example.net stopped answering.", status.Explanation);
        Assert.StartsWith("On the backup address 10.44.100.11 since ", status.ServerRowText);
    }

    [Fact]
    public async Task Tapped_it_explains_and_switches_back_when_asked()
    {
        var status = new BackupAddressStatus(_failover, _session, _dialogs);
        var switchedBack = 0;
        status.SwitchedBack += (_, _) => switchedBack++;
        FailOver();

        await status.ExplainCommand.ExecuteAsync(null); // "Not now"
        Assert.True(status.IsOnBackup);

        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        await status.ExplainCommand.ExecuteAsync(null);

        await _dialogs.Received().ConfirmAsync("Backup address", Arg.Any<string>(), "Switch back to the server address", "Not now");
        Assert.False(_failover.IsOnBackup);
        Assert.False(status.IsOnBackup);
        Assert.Equal(1, switchedBack);
    }
}

/// <summary>The shared file's header and "hide names" (#126).</summary>
public sealed class DiagnosticsReportTests
{
    private readonly AppSettings _appSettings = new() { ServerUrl = "https://nms.example.net/" };
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly ServerFailover _failover = new();
    private readonly InMemoryPreferences _preferences = new();
    private readonly ILibreNmsClient _client = Fakes.Client(devices: [Fakes.Device(1, "core-sw-01", ip: "10.0.0.1")]);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 5, 9, 30, 0, TimeSpan.Zero));

    public DiagnosticsReportTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _session.IsConnected.Returns(true);
        _session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "secret-token"));
        _session.ServerInfo.Returns(new SystemInfo { LocalVersion = "24.9.1" });
    }

    private DiagnosticsReport NewReport(DiagnosticsLog log) => new(
        log, _session, Fakes.Settings(_appSettings), _failover, new TabPins(_preferences),
        new AlertCountThreshold(Fakes.Settings(_appSettings), _preferences), new NotifyRules(Fakes.Settings(_appSettings), _preferences), _preferences, _client, _time);

    [Fact]
    public void The_header_names_the_app_phone_server_address_and_settings_but_no_secrets()
    {
        var report = NewReport(new DiagnosticsLog(null, _time, "1.1.0 (163)", "Apple iPhone16,1 · iOS 26.1"));

        var header = report.Header();

        Assert.Equal("App: DashyNMS mobile 1.1.0 (163)", header[2]);
        Assert.Equal("Phone: Apple iPhone16,1 · iOS 26.1", header[3]);
        Assert.Equal("Server: LibreNMS 24.9.1 · https", header[4]);
        Assert.Equal("Address in use: main", header[5]);
        Assert.Contains("tabs Devices, Alerts, Health", header[6]);
        Assert.Contains("notify every alert", header[6]); // which alerts notify (#167): counts, never rule names
        Assert.DoesNotContain(header, l => l.Contains("secret-token", StringComparison.Ordinal));
    }

    [Fact]
    public async Task With_names_hidden_every_device_name_and_address_goes()
    {
        var log = new DiagnosticsLog(null, _time);
        log.Note("Open", "device · device 1 - core-sw-01 at 10.0.0.1 on nms.example.net");
        var report = NewReport(log);
        report.HideNames = true;

        var text = await report.BuildAsync();

        Assert.Contains("names hidden", text);
        Assert.DoesNotContain("core-sw-01", text);
        Assert.DoesNotContain("10.0.0.1", text);
        Assert.DoesNotContain("nms.example.net", text);
        Assert.Contains("device-1 at ip-1 on host-1", text);
        Assert.True(new DiagnosticsReport(log, _session, Fakes.Settings(_appSettings), _failover, new TabPins(_preferences),
            new AlertCountThreshold(Fakes.Settings(_appSettings), _preferences), new NotifyRules(Fakes.Settings(_appSettings), _preferences), _preferences).HideNames); // remembered
    }
}
