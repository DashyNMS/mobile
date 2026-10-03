using DashyNMS.Mobile.Rules;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

/// <summary>Alert rules and templates, read-only (#22).</summary>
public sealed class AlertRulesTests
{
    private const string PortDownBuilder =
        """{"condition":"AND","rules":[{"id":"ports.ifOperStatus","field":"ports.ifOperStatus","type":"string","input":"text","operator":"equal","value":"down"}],"valid":true}""";

    private static readonly AlertRule DeviceDown = new() { Id = 10, Name = "Device down", SeverityText = "critical", Rule = "%devices.status = 0" };

    private static readonly AlertRule PortDown = new()
    {
        Id = 20,
        Name = "Port down",
        SeverityText = "warning",
        Builder = PortDownBuilder,
        Devices = [1, 99],
        Groups = [5],
        Notes = "Check the patch panel first.",
        Extra = new AlertRuleExtra { Recovery = false },
    };

    private static readonly AlertRule Old = new() { Id = 30, Name = "Old rule", SeverityText = "critical", Disabled = true };

    private static readonly AlertRule Quiet = new() { Id = 40, Name = "Another", SeverityText = "ok" };

    private static readonly AlertTemplate Template = new() { Id = 7, Name = "Ports", Title = "Port {{ $alert->title }}", Template = "Port down\r\n{{ $alert->hostname }}", AlertRules = [20] };

    private readonly ILibreNmsClient _client = Fakes.Client(
        devices: [Fakes.Device(1, "core-sw")],
        alerts: [Fakes.Alert(2, 1, "warning"), Fakes.Alert(1, 1, "critical"), Fakes.Alert(3, 1, "critical")]); // rules 20, 10, 30

    public AlertRulesTests()
    {
        _client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns([Quiet, Old, PortDown, DeviceDown]);
        _client.Rules.GetAsync(20, Arg.Any<CancellationToken>()).Returns(PortDown);
        _client.AlertTemplates.ListAsync(Arg.Any<CancellationToken>()).Returns([Template]);
        _client.AlertTemplates.GetAsync(7, Arg.Any<CancellationToken>()).Returns(Template);
        _client.DeviceGroups.ListAsync(Arg.Any<CancellationToken>()).Returns([new DeviceGroup { Id = 5, Name = "Core" }]);
        _client.Locations.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Location>());
    }

    [Fact]
    public async Task Rules_alerting_come_first_then_by_severity_with_disabled_ones_last()
    {
        var vm = new AlertRulesViewModel(_client, new RecordingNavigation());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["Device down", "Port down", "Old rule", "Another"], vm.Rules.Select(r => r.Name));
        Assert.Equal("4 rules · 3 alerting", vm.Summary);
        var port = vm.Rules.Single(r => r.Id == 20);
        Assert.Equal("1 alerting", port.AlertingText);
        Assert.Equal("Ports", port.TemplateName);
        Assert.Equal("Warning · 2 devices, 1 group", port.Summary);
        Assert.Equal("ports.ifOperStatus = \"down\"", port.Condition);
        Assert.Equal("%devices.status = 0", vm.Rules[0].Condition); // the legacy text, with no builder
        Assert.EndsWith("Disabled", vm.Rules.Single(r => r.Id == 30).Summary);
    }

    [Fact]
    public async Task Alerting_now_and_search_narrow_the_list()
    {
        var vm = new AlertRulesViewModel(_client, new RecordingNavigation());
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.ToggleAlertingCommand.Execute(null);
        Assert.Equal(3, vm.Rules.Count);

        vm.OnlyAlerting = false;
        vm.SearchText = "patch panel"; // notes count
        await Task.Delay(600);
        Assert.Equal(["Port down"], vm.Rules.Select(r => r.Name));
    }

    [Fact]
    public async Task A_rule_opens_its_page()
    {
        var navigation = new RecordingNavigation();
        var vm = new AlertRulesViewModel(_client, navigation);
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.OpenCommand.ExecuteAsync(vm.Rules.Single(r => r.Id == 20));

        var visit = Assert.Single(navigation.Visits);
        Assert.Equal(Routes.AlertRule, visit.Route);
        Assert.Equal(20, visit.Parameters![Routes.RuleIdParameter]);
    }

    [Fact]
    public async Task A_rule_shows_where_it_applies_how_it_notifies_and_what_it_has_alerting()
    {
        var navigation = new RecordingNavigation();
        var vm = new AlertRuleViewModel(_client, Fakes.Settings(), navigation) { RuleId = 20 };

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;

        Assert.Equal("Port down", vm.Title);
        Assert.Equal("Warning", vm.SeverityText);
        Assert.Equal("2 devices, 1 group", vm.TargetSummary);
        Assert.Equal("Devices: core-sw, #99\nGroups: Core", vm.TargetNames); // one since gone, by id
        Assert.Equal("Off", vm.RecoveryText);
        Assert.Equal("On", vm.AcknowledgementText);
        Assert.Equal("Check the patch panel first.", vm.Notes);
        Assert.False(vm.HasProcedure);
        Assert.Equal("Ports", vm.TemplateName);
        Assert.Single(vm.Alerts);

        await vm.OpenTemplateCommand.ExecuteAsync(null);
        Assert.Equal(7, navigation.Visits.Single().Parameters![Routes.TemplateIdParameter]);
    }

    [Fact]
    public async Task A_rule_shows_at_once_with_the_rest_filling_in_behind_it()
    {
        // The open alerts are slow to come: the rule mustn't wait for them (#123).
        var alerts = new TaskCompletionSource<IReadOnlyList<Alert>>();
        _client.Alerts.ListAsync(AlertQuery.Open, Arg.Any<CancellationToken>()).Returns(alerts.Task);
        _client.Rules.GetAsync(10, Arg.Any<CancellationToken>()).Returns(DeviceDown);
        var vm = new AlertRuleViewModel(_client, Fakes.Settings(), new RecordingNavigation()) { RuleId = 10 };

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.HasLoaded);
        Assert.Equal("Device down", vm.Title);
        Assert.False(vm.HasNoAlerts); // not "nothing alerting" before the alerts are in

        alerts.SetResult([Fakes.Alert(1, 1, "critical")]); // rule 10
        await vm.Extras;
        Assert.Single(vm.Alerts);

        // It names no groups or locations: neither list is asked for.
        await _client.DeviceGroups.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
        await _client.Locations.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_rule_targeting_nothing_with_no_alerts_doesnt_fetch_the_device_list()
    {
        _client.Rules.GetAsync(40, Arg.Any<CancellationToken>()).Returns(Quiet);
        var vm = new AlertRuleViewModel(_client, Fakes.Settings(), new RecordingNavigation()) { RuleId = 40 };

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;

        Assert.True(vm.HasNoAlerts);
        await _client.Devices.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void A_rule_without_a_template_uses_LibreNMSs_default_and_says_when_its_inverted()
    {
        var vm = new AlertRuleViewModel(_client, Fakes.Settings(), new RecordingNavigation());
        var inverted = new AlertRule { Id = 50, Name = "Inverted", Rule = "%devices.status = 1", Extra = new AlertRuleExtra { Invert = true }, InvertMap = true, Locations = [3] };

        vm.Show(inverted, new Dictionary<int, Device>(), [], [new Location { Id = 3, Name = "London" }], [Template], new AppSettings());

        Assert.Equal("LibreNMS's default template", vm.TemplateName);
        Assert.False(vm.HasTemplate);
        Assert.Equal("All except 1 location", vm.TargetSummary);
        Assert.Equal("Locations: London", vm.TargetNames);
        Assert.Contains("Inverted", vm.ConditionNote);
    }

    [Fact]
    public async Task A_template_shows_its_titles_body_and_the_rules_using_it()
    {
        var navigation = new RecordingNavigation();
        var vm = new AlertTemplateViewModel(_client, navigation) { TemplateId = 7 };

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("Ports", vm.Title);
        Assert.Equal("Port {{ $alert->title }}", vm.AlertTitle);
        Assert.False(vm.HasRecoveryTitle);
        Assert.Equal("Port down\n{{ $alert->hostname }}", vm.Body);
        Assert.Equal("Used by 1 rule", vm.UsedBy);
        var rule = Assert.Single(vm.Rules);
        Assert.Equal(("Port down", "ports.ifOperStatus = \"down\""), (rule.Name, rule.Condition));

        // Each rule using it opens its own page (#122).
        await vm.OpenRuleCommand.ExecuteAsync(rule);
        Assert.Equal(20, navigation.Visits.Single().Parameters![Routes.RuleIdParameter]);
    }

    [Fact]
    public async Task The_severity_and_disabled_chips_hide_rules_and_Clear_brings_them_back()
    {
        var vm = new AlertRulesViewModel(_client, new RecordingNavigation());
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal((3, 2, 1, 1), (vm.AlertingCount, vm.CriticalCount, vm.WarningCount, vm.DisabledCount));
        Assert.False(vm.HasActiveFilters);

        vm.ToggleCriticalCommand.Execute(null);
        Assert.Equal(["Port down", "Another"], vm.Rules.Select(r => r.Name));

        vm.ToggleWarningCommand.Execute(null);
        Assert.Equal(["Another"], vm.Rules.Select(r => r.Name)); // ok severity: no chip, always shown

        vm.ClearFiltersCommand.Execute(null);
        vm.ToggleDisabledCommand.Execute(null);
        Assert.DoesNotContain(vm.Rules, r => r.IsDisabled);
        Assert.True(vm.HasActiveFilters);

        vm.ClearFiltersCommand.Execute(null);
        Assert.Equal(4, vm.Rules.Count);
        Assert.Equal("All devices · Disabled", vm.Rules.Single(r => r.Id == 30).DetailText);
        Assert.Equal("2 devices, 1 group · Ports", vm.Rules.Single(r => r.Id == 20).DetailText);
    }

    [Fact]
    public void Alert_rules_is_a_page_in_More_under_Monitor()
    {
        Assert.Equal("Monitor", AppPages.Group(AppPage.AlertRules));
        Assert.Equal("//main/more/alertrules", AppPages.Resolve(Routes.Main + "/" + AppPages.TabRoute(AppPage.AlertRules), new TabPins(new InMemoryPreferences())));
    }
}
