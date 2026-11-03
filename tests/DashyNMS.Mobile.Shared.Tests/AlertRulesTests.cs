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
    public async Task First_pending_related_lookup_does_not_claim_account_visibility()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<AlertRule>>();
        _client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns(pending.Task);
        var vm = new AlertTemplateViewModel(_client, new RecordingNavigation()) { TemplateId = 7 };
        var refresh = vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Assigned to 1 rule · loading rule details", vm.UsedBy);
        Assert.DoesNotContain("available to this account", vm.UsedBy);
        pending.SetResult([PortDown]);
        await refresh;
        Assert.Equal("Assigned to 1 rule", vm.UsedBy);
    }

    [Fact]
    public void Related_rule_description_does_not_invent_alert_activity_or_repeat_the_template()
    {
        var row = new AlertRuleItem(PortDown, 0, "Template name must not be repeated");
        Assert.Contains(row.Name, row.RelatedRuleDescription);
        Assert.Contains(row.Condition, row.RelatedRuleDescription);
        Assert.Contains("Warning", row.RelatedRuleDescription);
        Assert.Contains("Enabled", row.RelatedRuleDescription);
        Assert.DoesNotContain("alerting", row.RelatedRuleDescription);
        Assert.DoesNotContain("Template name", row.RelatedRuleDescription);
    }

    [Fact]
    public async Task A_main_template_failure_clears_an_earlier_related_failure_status()
    {
        _client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<AlertRule>>(_ => throw new LibreNmsApiException("Unavailable"));
        var vm = new AlertTemplateViewModel(_client, new RecordingNavigation()) { TemplateId = 7 };
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.HasRulesStatus);
        _client.AlertTemplates.GetAsync(7, Arg.Any<CancellationToken>()).Returns<AlertTemplate?>(_ => throw new LibreNmsApiException("Unavailable"));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.HasError);
        Assert.False(vm.HasRulesStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_rule_refresh_keeps_pending_details_for_the_displayed_rule(bool missing)
    {
        var pending = new TaskCompletionSource<IReadOnlyList<Alert>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _client.Alerts.ListAsync(AlertQuery.Open, Arg.Any<CancellationToken>()).Returns(pending.Task);
        var vm = new AlertRuleViewModel(_client, Fakes.Settings(), new RecordingNavigation()) { RuleId = 20 };
        await vm.RefreshCommand.ExecuteAsync(null);
        var firstExtras = vm.Extras;
        Assert.Equal("Loading template…", vm.TemplateName);
        if (missing)
            _client.Rules.GetAsync(20, Arg.Any<CancellationToken>()).Returns(Task.FromResult<AlertRule?>(null));
        else
            _client.Rules.GetAsync(20, Arg.Any<CancellationToken>()).Returns<AlertRule?>(_ => throw new LibreNmsApiException("Unavailable"));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.HasError);
        Assert.False(vm.HasSupplementaryStatus);
        pending.SetResult([Fakes.Alert(2, 1, "warning")]);
        await firstExtras;
        Assert.Equal("Ports", vm.TemplateName);
        Assert.True(vm.HasAlerts);
        Assert.False(vm.HasSupplementaryStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_names_describe_current_targets_and_combine_failures(bool loadFirst)
    {
        var vm = new AlertRuleViewModel(_client, Fakes.Settings(), new RecordingNavigation()) { RuleId = 20 };
        if (loadFirst)
        {
            await vm.RefreshCommand.ExecuteAsync(null);
            await vm.Extras;
        }
        _client.Rules.GetAsync(20, Arg.Any<CancellationToken>()).Returns(new AlertRule { Id = 20, Devices = [1, 42], Groups = [8] });
        _client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<Device>>(_ => throw new LibreNmsApiException("Unavailable"));
        _client.DeviceGroups.ListAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<DeviceGroup>>(_ => throw new LibreNmsApiException("Unavailable"));
        _client.Alerts.ListAsync(AlertQuery.Open, Arg.Any<CancellationToken>()).Returns<IReadOnlyList<Alert>>(_ => throw new LibreNmsApiException("Unavailable"));
        _client.AlertTemplates.ListAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<AlertTemplate>>(_ => throw new LibreNmsApiException("Unavailable"));
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;
        Assert.Equal(loadFirst ? "Devices: core-sw, #42\nGroups: #8" : "Devices: #1, #42\nGroups: #8", vm.TargetNames);
        Assert.Contains("Alerts could not be loaded", vm.SupplementaryStatus);
        Assert.Contains("Template could not be loaded", vm.SupplementaryStatus);
        Assert.Contains("target names", vm.SupplementaryStatus);
        Assert.DoesNotContain("previously loaded", vm.SupplementaryStatus);
    }

    [Fact]
    public async Task Older_extras_cannot_replace_newer_failed_target_lookup()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<Alert>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _client.Alerts.ListAsync(AlertQuery.Open, Arg.Any<CancellationToken>()).Returns(pending.Task);
        var vm = new AlertRuleViewModel(_client, Fakes.Settings(), new RecordingNavigation()) { RuleId = 20 };
        await vm.RefreshCommand.ExecuteAsync(null);
        var olderExtras = vm.Extras;
        _client.Rules.GetAsync(20, Arg.Any<CancellationToken>()).Returns(new AlertRule { Id = 20, Groups = [42] });
        _client.Alerts.ListAsync(AlertQuery.Open, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Alert>>([]));
        _client.DeviceGroups.ListAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<DeviceGroup>>(_ => throw new LibreNmsApiException("Unavailable"));
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;
        var status = vm.SupplementaryStatus;
        pending.SetResult([Fakes.Alert(2, 1, "warning")]);
        await olderExtras;
        Assert.Equal("Groups: #42", vm.TargetNames);
        Assert.Equal(status, vm.SupplementaryStatus);
        Assert.True(vm.HasNoAlerts);
    }

    [Theory]
    [InlineData("  ports.ifOperStatus = 2  ")]
    [InlineData("\n%devices.status = 0\n")]
    public void Condition_list_trims_and_detail_preserves_legacy_source(string source)
    {
        var rule = new AlertRule { Rule = source };
        Assert.Equal(source, RuleText.ConditionSource(rule));
        Assert.Equal(source.Trim(), RuleText.Condition(rule));
    }

    [Fact]
    public async Task Detail_preserves_complete_SQL_notes_and_procedure()
    {
        const string sql = "  SELECT *\r\nFROM devices WHERE devices.status = 0 AND devices.disabled = 0  ";
        var rule = new AlertRule { Id = 20, Query = sql, Rule = "different legacy condition", Builder = PortDownBuilder, Extra = new AlertRuleExtra { Options = new AlertRuleExtraOptions { OverrideQuery = true } },
            Notes = "  Keep leading spaces\r\nand trailing spaces  ", Procedure = "\n  Step one\nStep two  \n" };
        _client.Rules.GetAsync(20, Arg.Any<CancellationToken>()).Returns(rule);
        var vm = new AlertRuleViewModel(_client, Fakes.Settings(), new RecordingNavigation()) { RuleId = 20 };
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;
        Assert.Equal(sql, vm.Condition);
        Assert.Equal(sql, RuleText.ConditionSource(rule));
        Assert.Equal(sql.Trim(), RuleText.Condition(rule));
        Assert.Equal(rule.Notes, vm.Notes);
        Assert.Equal(rule.Procedure, vm.Procedure);
    }

    [Fact]
    public async Task Failed_extras_keep_last_good_alerts_and_template_navigation()
    {
        var navigation = new RecordingNavigation();
        var vm = new AlertRuleViewModel(_client, Fakes.Settings(), navigation) { RuleId = 20 };
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;
        _client.Alerts.ListAsync(AlertQuery.Open, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Alert>>(_ => throw new LibreNmsApiException("Unavailable"));
        _client.AlertTemplates.ListAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<AlertTemplate>>(_ => throw new LibreNmsApiException("Unavailable"));
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;
        Assert.Single(vm.Alerts);
        Assert.False(vm.HasNoAlerts);
        Assert.True(vm.HasSupplementaryStatus);
        Assert.Equal("Ports", vm.TemplateName);
        await vm.OpenTemplateCommand.ExecuteAsync(null);
        Assert.Equal(7, Assert.Single(navigation.Visits).Parameters![Routes.TemplateIdParameter]);
    }

    [Fact]
    public async Task Unavailable_initial_extras_do_not_claim_empty_or_default()
    {
        _client.Alerts.ListAsync(AlertQuery.Open, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Alert>>(_ => throw new LibreNmsApiException("Unavailable"));
        _client.AlertTemplates.ListAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<AlertTemplate>>(_ => throw new LibreNmsApiException("Unavailable"));
        var vm = new AlertRuleViewModel(_client, Fakes.Settings(), new RecordingNavigation()) { RuleId = 20 };
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;
        Assert.False(vm.HasNoAlerts);
        Assert.False(vm.HasTemplate);
        Assert.Equal("Template unavailable", vm.TemplateName);
        Assert.True(vm.HasSupplementaryStatus);
    }

    [Fact]
    public async Task Filter_descriptions_announce_toggles_and_clear_with_source_counts()
    {
        var vm = new AlertRulesViewModel(_client, new RecordingNavigation());
        await vm.RefreshCommand.ExecuteAsync(null);
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.Equal("Critical, 2 rules, included", vm.CriticalFilterDescription);
        Assert.Equal("Alerting, 3 rules, filter off", vm.AlertingFilterDescription);
        vm.ToggleCriticalCommand.Execute(null);
        vm.ToggleWarningCommand.Execute(null);
        vm.ToggleDisabledCommand.Execute(null);
        vm.ToggleAlertingCommand.Execute(null);
        Assert.Equal("Critical, 2 rules, excluded", vm.CriticalFilterDescription);
        Assert.Equal("Warning, 1 rule, excluded", vm.WarningFilterDescription);
        Assert.Equal("Disabled, 1 rule, excluded", vm.DisabledFilterDescription);
        Assert.Equal("Alerting, 3 rules, filter on", vm.AlertingFilterDescription);
        Assert.Contains(nameof(vm.CriticalFilterDescription), changes);
        Assert.Contains(nameof(vm.WarningFilterDescription), changes);
        Assert.Contains(nameof(vm.DisabledFilterDescription), changes);
        Assert.Contains(nameof(vm.AlertingFilterDescription), changes);
        vm.ClearFiltersCommand.Execute(null);
        Assert.EndsWith("included", vm.CriticalFilterDescription);
        Assert.EndsWith("included", vm.WarningFilterDescription);
        Assert.EndsWith("included", vm.DisabledFilterDescription);
        Assert.EndsWith("off", vm.AlertingFilterDescription);
    }

    [Fact]
    public async Task Embedded_selection_survives_filters_and_replacement_rows()
    {
        var vm = new AlertRulesViewModel(_client, new RecordingNavigation());
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.Select(20);
        Assert.Equal(20, Assert.Single(vm.Rules, row => row.IsSelected).Id);
        vm.ToggleWarningCommand.Execute(null);
        Assert.DoesNotContain(vm.Rules, row => row.IsSelected);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.ClearFiltersCommand.Execute(null);
        Assert.Equal(20, Assert.Single(vm.Rules, row => row.IsSelected).Id);
        vm.Select(null);
        Assert.DoesNotContain(vm.Rules, row => row.IsSelected);
    }

    [Fact]
    public void Row_description_preserves_source_and_announces_state_changes()
    {
        var row = new AlertRuleItem(new AlertRule { Id = 81, Name = " ", Rule = "ports.ifOperStatus = 2", Disabled = true, SeverityText = "warning" }, 0, " ");
        var changes = new List<string?>();
        row.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.StartsWith("Rule 81. ports.ifOperStatus = 2. All devices.", row.AccessibleDescription);
        Assert.Contains("Warning · Disabled · 0 alerting", row.AccessibleDescription);
        Assert.DoesNotContain(". .", row.AccessibleDescription);
        row.IsSelected = true;
        Assert.Contains("Selected", row.AccessibleDescription);
        Assert.Contains(nameof(AlertRuleItem.AccessibleDescription), changes);
        Assert.Equal("ports.ifOperStatus = 2", row.Condition);
    }

    [Theory]
    [InlineData(true, 8, "On", "8")]
    [InlineData(false, null, "Off", "Inherited (server default)")]
    [InlineData(null, null, "Not reported by this server", "Inherited (server default)")]
    public async Task Per_entity_settings_preserve_unknown_and_inherited_values(bool? enabled, int? maximum, string expectedEnabled, string expectedMaximum)
    {
        _client.Rules.GetAsync(20, Arg.Any<CancellationToken>()).Returns(new AlertRule { Id = 20, NotifyPerEntity = enabled, MaxEntities = maximum });
        var vm = new AlertRuleViewModel(_client, Fakes.Settings(), new RecordingNavigation()) { RuleId = 20 };
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;
        Assert.Equal(expectedEnabled, vm.PerEntityText);
        Assert.Equal(expectedMaximum, vm.MaxEntitiesText);
    }

    [Theory]
    [InlineData("last_ping")]
    [InlineData("last_ping_timetaken")]
    [InlineData("last_poll_attempted")]
    [InlineData("agent_uptime")]
    public async Task Removed_device_columns_are_flagged(string column)
    {
        _client.Rules.GetAsync(20, Arg.Any<CancellationToken>()).Returns(new AlertRule { Id = 20, Query = $"SELECT * FROM devices WHERE devices.{column} > 0" });
        var vm = new AlertRuleViewModel(_client, Fakes.Settings(), new RecordingNavigation()) { RuleId = 20 };
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;
        Assert.True(vm.HasCompatibilityWarning);
        Assert.Contains("26.10", vm.CompatibilityWarning);
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
        Assert.Equal("Port down\r\n{{ $alert->hostname }}", vm.Body);
        Assert.Equal("Assigned to 1 rule", vm.UsedBy);
        var rule = Assert.Single(vm.Rules);
        Assert.Equal(("Port down", "ports.ifOperStatus = \"down\""), (rule.Name, rule.Condition));

        // Each rule using it opens its own page (#122).
        await vm.OpenRuleCommand.ExecuteAsync(rule);
        Assert.Equal(20, navigation.Visits.Single().Parameters![Routes.RuleIdParameter]);
        Assert.Equal(Routes.AlertRule, navigation.Visits.Single().Route);
    }

    [Fact]
    public void Template_source_is_preserved_and_default_status_is_never_invented()
    {
        var vm = new AlertTemplateViewModel(_client, new RecordingNavigation());
        var template = new AlertTemplate { Id = 8, Title = "  {{ $alert->title }} \r\n", TitleRec = "\tRecovery  ", Template = "<b>source</b>\r\n  \n\t" };
        vm.Show(template, []);
        Assert.Equal(template.Title, vm.AlertTitle);
        Assert.Equal(template.TitleRec, vm.RecoveryTitle);
        Assert.Equal(template.Template, vm.Body);
        Assert.Contains("Default-template status is not reported", vm.UsedBy);
        Assert.Empty(vm.Rules);
    }

    [Fact]
    public async Task Initial_related_rule_failure_does_not_hide_template_or_claim_no_assignments()
    {
        _client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<AlertRule>>(_ => throw new LibreNmsApiException("Unavailable"));
        var vm = new AlertTemplateViewModel(_client, new RecordingNavigation()) { TemplateId = 7 };
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.HasLoaded);
        Assert.Equal(Template.Template, vm.Body);
        Assert.Empty(vm.Rules);
        Assert.Equal("Assigned to 1 rule · rule details unavailable", vm.UsedBy);
        Assert.True(vm.HasRulesStatus);
        Assert.DoesNotContain("previously", vm.RulesStatus);
        Assert.True(vm.CanRetryRules);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_template_refresh_retains_last_source_and_related_navigation(bool missing)
    {
        var navigation = new RecordingNavigation();
        var vm = new AlertTemplateViewModel(_client, navigation) { TemplateId = 7 };
        await vm.RefreshCommand.ExecuteAsync(null);
        if (missing)
            _client.AlertTemplates.GetAsync(7, Arg.Any<CancellationToken>()).Returns(Task.FromResult<AlertTemplate?>(null));
        else
            _client.AlertTemplates.GetAsync(7, Arg.Any<CancellationToken>()).Returns<AlertTemplate?>(_ => throw new LibreNmsApiException("Unavailable"));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.HasError);
        Assert.Equal(Template.Template, vm.Body);
        await vm.OpenRuleCommand.ExecuteAsync(Assert.Single(vm.Rules));
        Assert.Equal(20, Assert.Single(navigation.Visits).Parameters![Routes.RuleIdParameter]);
    }

    [Fact]
    public async Task Related_rule_failure_keeps_template_and_only_current_assignments()
    {
        var vm = new AlertTemplateViewModel(_client, new RecordingNavigation()) { TemplateId = 7 };
        await vm.RefreshCommand.ExecuteAsync(null);
        _client.AlertTemplates.GetAsync(7, Arg.Any<CancellationToken>()).Returns(new AlertTemplate { Id = 7, Name = "Changed", Template = "new source", AlertRules = [10] });
        _client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<AlertRule>>(_ => throw new LibreNmsApiException("Unavailable"));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("new source", vm.Body);
        Assert.Empty(vm.Rules);
        Assert.True(vm.HasRulesStatus);
        Assert.Equal("Assigned to 1 rule · rule details unavailable", vm.UsedBy);
        Assert.False(vm.HasError);
        _client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns([DeviceDown]);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(10, Assert.Single(vm.Rules).Id);
        Assert.False(vm.HasRulesStatus);
    }

    [Fact]
    public async Task Unassigned_template_skips_failing_rule_lookup()
    {
        _client.AlertTemplates.GetAsync(7, Arg.Any<CancellationToken>()).Returns(new AlertTemplate { Id = 7 });
        _client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<AlertRule>>(_ => throw new LibreNmsApiException("Unavailable"));
        var vm = new AlertTemplateViewModel(_client, new RecordingNavigation()) { TemplateId = 7 };
        await vm.RefreshCommand.ExecuteAsync(null);
        await _client.Rules.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
        Assert.StartsWith("No explicit rule assignments.", vm.UsedBy);
        Assert.False(vm.HasRulesStatus);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task Assigned_lookup_failure_retains_current_row_and_server_count()
    {
        var navigation = new RecordingNavigation();
        var vm = new AlertTemplateViewModel(_client, navigation) { TemplateId = 7 };
        await vm.RefreshCommand.ExecuteAsync(null);
        _client.AlertTemplates.GetAsync(7, Arg.Any<CancellationToken>()).Returns(new AlertTemplate { Id = 7, AlertRules = [20, 99] });
        _client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<AlertRule>>(_ => throw new LibreNmsApiException("Unavailable"));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Assigned to 2 rules · rule details unavailable", vm.UsedBy);
        Assert.Contains("previously loaded", vm.RulesStatus);
        await vm.OpenRuleCommand.ExecuteAsync(Assert.Single(vm.Rules));
        Assert.Equal(20, Assert.Single(navigation.Visits).Parameters![Routes.RuleIdParameter]);
    }

    [Fact]
    public async Task Partial_assignment_visibility_survives_loading_lookup()
    {
        _client.AlertTemplates.GetAsync(7, Arg.Any<CancellationToken>()).Returns(new AlertTemplate { Id = 7, AlertRules = [20, 99] });
        var vm = new AlertTemplateViewModel(_client, new RecordingNavigation()) { TemplateId = 7 };
        await vm.RefreshCommand.ExecuteAsync(null);
        var pending = new TaskCompletionSource<IReadOnlyList<AlertRule>>();
        _client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns(pending.Task);
        var refresh = vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Assigned to 2 rules · loading rule details", vm.UsedBy);
        Assert.True(vm.IsBusy);
        Assert.False(vm.CanRetryRules);
        pending.SetResult([PortDown]);
        await refresh;
        Assert.Equal("Assigned to 2 rules · 1 available to this account", vm.UsedBy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task First_template_failure_does_not_claim_retained_content(bool missing)
    {
        if (missing)
            _client.AlertTemplates.GetAsync(7, Arg.Any<CancellationToken>()).Returns(Task.FromResult<AlertTemplate?>(null));
        else
            _client.AlertTemplates.GetAsync(7, Arg.Any<CancellationToken>()).Returns<AlertTemplate?>(_ => throw new LibreNmsApiException("Unavailable"));
        var vm = new AlertTemplateViewModel(_client, new RecordingNavigation()) { TemplateId = 7 };
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.HasError);
        Assert.False(vm.HasLoaded);
        Assert.DoesNotContain("last loaded", vm.ErrorMessage!);
        Assert.False(vm.CanRetryRules);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unexpected_related_lookup_failures_reach_base_handling(bool cancelled)
    {
        _client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<AlertRule>>(_ =>
            throw (cancelled ? (Exception)new OperationCanceledException() : new InvalidOperationException("Unexpected lookup failure")));
        var vm = new AlertTemplateViewModel(_client, new RecordingNavigation()) { TemplateId = 7 };
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(!cancelled, vm.HasError);
        Assert.Equal("Assigned to 1 rule · rule details unavailable", vm.UsedBy);
        if (!cancelled) Assert.Equal("Unexpected lookup failure", vm.ErrorMessage);
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
        Assert.Equal("All devices", vm.Rules.Single(r => r.Id == 30).DetailText);
        Assert.Equal("2 devices, 1 group · Ports", vm.Rules.Single(r => r.Id == 20).DetailText);
    }

    [Fact]
    public void Alert_rules_is_a_page_in_More_under_Monitor()
    {
        Assert.Equal("Monitor", AppPages.Group(AppPage.AlertRules));
        Assert.Equal("//main/more/alertrules", AppPages.Resolve(Routes.Main + "/" + AppPages.TabRoute(AppPage.AlertRules), new TabPins(new InMemoryPreferences())));
    }
}
