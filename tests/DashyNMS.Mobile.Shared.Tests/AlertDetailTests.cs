using System.Text.Json;
using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Json;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class AlertDetailViewModelTests
{
    private const string ErrorsRule = """
        {
          "condition": "AND",
          "rules": [
            { "id": "ports.ifInErrors_delta", "field": "ports.ifInErrors_delta", "operator": "greater", "value": "500" }
          ],
          "valid": true
        }
        """;

    private readonly ILibreNmsClient _client = Fakes.Client();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly RecordingNavigation _navigation = new();
    private readonly ISelfActionTracker _selfActions = Substitute.For<ISelfActionTracker>();

    private AlertDetailViewModel NewViewModel() => new(_client, Fakes.Settings(), _dialogs, _navigation, _selfActions);

    private static AlertLogEntry LogEntry(int ruleId, int state, DateTime at, string rowJson = "{}") =>
        JsonSerializer.Deserialize<AlertLogEntry>(
            $$"""{ "id": 1, "rule_id": {{ruleId}}, "device_id": 3, "state": {{state}}, "time_logged": "{{at:yyyy-MM-dd HH:mm:ss}}", "details": { "rule": [ {{rowJson}} ] } }""",
            LibreNmsJson.Options)!;

    private void GivenAlert(Alert alert, AlertRule? rule = null, params AlertLogEntry[] log)
    {
        _client.Alerts.GetAsync(alert.Id, Arg.Any<CancellationToken>()).Returns(alert);
        _client.Rules.GetAsync(alert.RuleId, Arg.Any<CancellationToken>()).Returns(rule);
        _client.Logs.ListAlertLogAsync(alert.DeviceId, AlertDetailViewModel.LogDepth, Arg.Any<CancellationToken>()).Returns(log);
    }

    [Fact]
    public async Task Shows_why_it_fired_with_the_rules_columns_first_as_desktop()
    {
        var alert = Fakes.Alert(5, deviceId: 3, "critical");
        var rule = new AlertRule { Id = alert.RuleId, Name = "Port errors", SeverityText = "critical", Builder = ErrorsRule };
        GivenAlert(alert, rule,
            LogEntry(alert.RuleId, 1, new DateTime(2026, 1, 1, 12, 0, 0),
                """{ "hostname": "sw1", "ifName": "Gi0/1", "ifAlias": "uplink", "ifInErrors_delta": 812, "ifSpeed": 1000000000 }"""),
            LogEntry(alert.RuleId, 0, new DateTime(2026, 1, 1, 9, 0, 0)),
            LogEntry(ruleId: 999, 1, new DateTime(2026, 1, 1, 8, 0, 0))); // another rule: not this alert's
        var vm = NewViewModel();

        await vm.LoadAsync(5);

        Assert.Equal("Rule 5", vm.Title);
        Assert.Equal(["Alert", "Why it fired", "Rule", "History"], vm.Groups.Select(g => g.Name));

        var fault = Assert.Single(vm.Groups[1]);
        Assert.Equal("Gi0/1 - uplink", fault.Title);
        Assert.Equal([new SectionField("ifInErrors_delta", "812")], fault.Fields); // the column the rule tests; the rest only when asked (#46)
        Assert.Equal(RowStatus.Critical, fault.Status);
        Assert.True(vm.HasMoreFields);
        Assert.Equal("Show all fields", vm.Groups[1].FooterText); // on the card itself (Batch 11)
        Assert.Same(vm.ToggleAllFieldsCommand, vm.Groups[1].FooterCommand);

        var ruleRow = vm.Groups[2][0];
        Assert.Equal("Port errors", ruleRow.Title);
        Assert.Contains("ifInErrors_delta", ruleRow.Subtitle);

        Assert.Equal(2, vm.Groups[3].Count); // only this rule's entries
        Assert.Equal(RowStatus.Ok, vm.Groups[3][1].Status);
        Assert.True(vm.CanAcknowledge);
        Assert.False(vm.CanUnacknowledge);

        // Show all fields: everything else measured, smaller, for every match.
        vm.ToggleAllFieldsCommand.Execute(null);
        var full = Assert.Single(vm.Groups[1]);
        Assert.Equal(new SectionField("ifInErrors_delta", "812"), full.Fields[0]);
        Assert.Contains(full.Fields, f => f.Name == "ifSpeed" && f.IsSecondary);
        Assert.Equal("Show only what the rule tests", vm.Groups[1].FooterText);
        Assert.Equal("Show only what the rule tests", vm.AllFieldsText);
    }

    [Fact]
    public async Task Still_shows_the_alert_when_the_token_cant_read_rules_or_logs()
    {
        var alert = Fakes.Alert(5, deviceId: 3, "warning");
        _client.Alerts.GetAsync(5, Arg.Any<CancellationToken>()).Returns(alert);
        _client.Rules.GetAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<AlertRule?>(_ => throw new LibreNmsApiException("forbidden", System.Net.HttpStatusCode.Forbidden));
        _client.Logs.ListAlertLogAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<AlertLogEntry>>(_ => throw new LibreNmsApiException("forbidden", System.Net.HttpStatusCode.Forbidden));
        var vm = NewViewModel();

        await vm.LoadAsync(5);

        Assert.False(vm.HasError);
        Assert.Equal(["Alert"], vm.Groups.Select(g => g.Name));
        Assert.Equal("host3", vm.Groups[0][0].Value);
    }

    [Fact]
    public async Task A_cleared_alert_says_so_and_can_still_open_its_device()
    {
        _client.Alerts.GetAsync(5, Arg.Any<CancellationToken>()).Returns((Alert?)null);
        var vm = NewViewModel();

        await vm.LoadAsync(5, deviceId: 3);

        Assert.True(vm.IsGone);
        Assert.False(vm.HasAlert);
        Assert.Empty(vm.Groups);

        await vm.OpenDeviceCommand.ExecuteAsync(null);
        Assert.Equal(3, Assert.Single(_navigation.Visits).Parameters![Routes.DeviceIdParameter]);
    }

    [Fact]
    public async Task Acknowledging_records_it_as_your_own_and_reloads()
    {
        var alert = Fakes.Alert(5, deviceId: 3, "critical");
        GivenAlert(alert);
        _dialogs.PromptAsync(default!, default!, default!, default).ReturnsForAnyArgs(" on it ");
        var vm = NewViewModel();
        await vm.LoadAsync(5);
        var changes = new List<AlertStateChange>();
        vm.AlertChanged += (_, change) => changes.Add(change);

        await vm.AcknowledgeCommand.ExecuteAsync(null);

        await _client.Alerts.Received(1).AcknowledgeAsync(5, "on it", true, Arg.Any<CancellationToken>());
        _selfActions.Received(1).Record(5, AlertChangeKind.Acknowledged);
        await _client.Alerts.Received(2).GetAsync(5, Arg.Any<CancellationToken>());

        // For the list beside it on a larger screen (#88).
        Assert.Equal(new AlertStateChange(5, Acknowledged: true, "on it"), Assert.Single(changes));
    }

    [Fact]
    public async Task Tapping_an_alert_in_the_list_opens_its_page()
    {
        var alert = Fakes.Alert(5, deviceId: 3, "critical");
        await _navigation.GoToAlertAsync(alert);

        var visit = Assert.Single(_navigation.Visits);
        Assert.Equal(Routes.AlertDetail, visit.Route);
        Assert.Equal(5, visit.Parameters![Routes.AlertIdParameter]);
        Assert.Equal(3, visit.Parameters[Routes.DeviceIdParameter]);
    }

    [Theory]
    [InlineData(
        "sensors.sensor_descr REGEXP \"(?i)(power|psu) AND x\" AND sensors.sensor_alert = 1 AND (macros.a = 1 OR (b = 2 AND c = 3))",
        "sensors.sensor_descr REGEXP \"(?i)(power|psu) AND x\"\nAND sensors.sensor_alert = 1\nAND (macros.a = 1 OR (b = 2 AND c = 3))")]
    [InlineData(
        "%bgpPeers.bgpPeerState != \"established\" && %macros.device_up = 1 || x = 'a OR b'",
        "%bgpPeers.bgpPeerState != \"established\"\n&& %macros.device_up = 1\n|| x = 'a OR b'")]
    [InlineData("ports.ifOperStatus = \"down\"", "ports.ifOperStatus = \"down\"")]
    [InlineData("hostname = \"ANDROID\" and vendor = 'x'", "hostname = \"ANDROID\"\nand vendor = 'x'")]
    public void Breaks_the_rule_condition_at_its_top_level_ands_and_ors(string condition, string expected) =>
        Assert.Equal(expected, AlertDetailViewModel.BreakCondition(condition));
}

public sealed class AlertNotificationTargetTests
{
    [Fact]
    public async Task A_single_alerts_notification_opens_that_alert()
    {
        var session = Substitute.For<DesktopNMS.Services.ISessionService>();
        session.IsConnected.Returns(true);
        var navigation = new RecordingNavigation();
        var router = new NotificationRouter(session, navigation);
        await router.MainShownAsync();

        await router.OpenAsync(new NotificationTarget(DeviceId: 3, AlertId: 5));

        var visit = Assert.Single(navigation.Visits);
        Assert.Equal(Routes.AlertDetail, visit.Route);
        Assert.Equal(5, visit.Parameters![Routes.AlertIdParameter]);
        Assert.Equal(3, visit.Parameters[Routes.DeviceIdParameter]);
    }
}
