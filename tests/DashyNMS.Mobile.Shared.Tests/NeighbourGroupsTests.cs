using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

/// <summary>Desktop's neighbour views as the phone's groups (#98).</summary>
public sealed class NeighbourGroupsTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly ILibreNmsClient _client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")]);
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly RecordingNavigation _navigation = new();
    private readonly NeighbourDirectory _directory;

    public NeighbourGroupsTests()
    {
        _settings = Fakes.Settings(_appSettings);
        _directory = new NeighbourDirectory(_client, _settings);
        _client.Links.ListAllAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new NetworkLink { Id = 1, LocalDeviceId = 1, LocalPortId = 11, RemoteHostname = "ap-lobby", RemoteVersion = "Aruba AP-515", RemotePort = "eth0", Protocol = "lldp" },
            new NetworkLink { Id = 2, LocalDeviceId = 1, LocalPortId = 12, RemoteHostname = "ap-kitchen", RemoteVersion = "Aruba AP-305", RemotePort = "eth0", Protocol = "lldp" },
            new NetworkLink { Id = 3, LocalDeviceId = 2, LocalPortId = 21, RemoteHostname = "SEP001122", RemoteVersion = "Cisco IP Phone 8851", RemotePort = "Port 1", Protocol = "cdp" },
        ]);
        _client.Ports.ListAllStatusAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Port { PortId = 11, DeviceId = 1, IfName = "Gi0/1", IfAlias = "AP", IfOperStatus = "up" },
            new Port { PortId = 12, DeviceId = 1, IfName = "Gi0/2", IfAlias = "AP", IfOperStatus = "up" },
            new Port { PortId = 21, DeviceId = 2, IfName = "Gi0/3", IfAlias = "Desk 4", IfOperStatus = "up" },
        ]);
    }

    private static NeighbourViewDefinition Group(string name, bool matchAll, params (NeighbourRuleField Field, NeighbourRuleOperator Op, string Value)[] rules) => new()
    {
        Name = name,
        MatchAll = matchAll,
        Rules = rules.Select(r => new NeighbourRule { Field = r.Field, Operator = r.Op, Value = r.Value }).ToList(),
    };

    private async Task<NeighboursViewModel> Loaded()
    {
        var vm = new NeighboursViewModel(_directory, _settings, _navigation);
        await vm.RefreshCommand.ExecuteAsync(null);
        return vm;
    }

    private static string[] Names(NeighboursViewModel vm) => vm.Links.Select(l => l.RemoteName).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task Group_chips_count_and_filter_with_each_test()
    {
        _appSettings.NeighbourViews =
        [
            Group("Aruba APs", true, (NeighbourRuleField.SystemDescription, NeighbourRuleOperator.Contains, "aruba")),
            Group("Starts ap-", true, (NeighbourRuleField.SystemName, NeighbourRuleOperator.StartsWith, "AP-")),
            Group("CDP", true, (NeighbourRuleField.Protocol, NeighbourRuleOperator.Equals, "cdp")),
            Group("Not on edge", true, (NeighbourRuleField.Switch, NeighbourRuleOperator.DoesNotContain, "edge")),
            Group("515 by regex", true, (NeighbourRuleField.SystemDescription, NeighbourRuleOperator.Matches, @"AP-5\d\d$")),
        ];
        var vm = await Loaded();

        Assert.Equal(["All 3", "Aruba APs 2", "Starts ap- 2", "CDP 1", "Not on edge 2", "515 by regex 1"], vm.Groups.Select(g => g.Text));

        vm.SelectGroupCommand.Execute(vm.Groups[3]);
        Assert.Equal(["SEP001122"], Names(vm));
        Assert.True(vm.Groups[3].IsSelected);
        Assert.Equal("1 of 3 neighbours", vm.CountText);

        vm.SelectGroupCommand.Execute(vm.Groups[5]);
        Assert.Equal(["ap-lobby"], Names(vm));

        vm.SelectGroupCommand.Execute(vm.Groups[5]); // tapped again: everything
        Assert.Equal(3, vm.Links.Count);
        Assert.True(vm.Groups[0].IsSelected);
    }

    [Fact]
    public async Task All_or_any_and_the_switch_ports_description()
    {
        _appSettings.NeighbourViews =
        [
            Group("AP ports, 305", true,
                (NeighbourRuleField.SwitchPortDescription, NeighbourRuleOperator.Equals, "AP"),
                (NeighbourRuleField.SystemDescription, NeighbourRuleOperator.Contains, "305")),
            Group("AP ports or phones", false,
                (NeighbourRuleField.SwitchPortDescription, NeighbourRuleOperator.Equals, "AP"),
                (NeighbourRuleField.SystemDescription, NeighbourRuleOperator.Contains, "phone")),
        ];
        var vm = await Loaded();

        vm.SelectGroupCommand.Execute(vm.Groups[1]);
        Assert.Equal(["ap-kitchen"], Names(vm));

        vm.SelectGroupCommand.Execute(vm.Groups[2]);
        Assert.Equal(["SEP001122", "ap-kitchen", "ap-lobby"], Names(vm));
    }

    [Fact]
    public async Task A_broken_regex_matches_nothing_rather_than_failing()
    {
        _appSettings.NeighbourViews = [Group("Broken", true, (NeighbourRuleField.SystemName, NeighbourRuleOperator.Matches, "(ap"))];
        var vm = await Loaded();

        vm.SelectGroupCommand.Execute(vm.Groups[1]);

        Assert.Empty(vm.Links);
        Assert.Equal("Broken 0", vm.Groups[1].Text);
    }

    [Fact]
    public async Task The_editor_checks_a_regex_as_its_typed_and_wont_save_a_broken_one()
    {
        var editor = new NeighbourGroupEditorViewModel(_settings, _navigation, _dialogs, _directory);
        editor.Load(null);
        editor.Name = "APs";
        var rule = editor.Rules.Single();
        rule.OperatorIndex = (int)NeighbourRuleOperator.Matches;

        rule.Value = "ap-(lobby";
        Assert.True(rule.HasProblem);
        Assert.StartsWith("Not a valid regular expression", rule.Problem);

        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Equal(rule.Problem, editor.ErrorMessage);
        Assert.Empty(_appSettings.NeighbourViews);

        rule.Value = "ap-(lobby|kitchen)";
        Assert.False(rule.HasProblem);
    }

    [Fact]
    public async Task The_editor_saves_a_new_group_into_desktops_settings()
    {
        var editor = new NeighbourGroupEditorViewModel(_settings, _navigation, _dialogs, _directory);
        editor.Load(null);
        Assert.Equal("New neighbourhood", editor.Heading);
        editor.Name = "  Phones ";
        editor.MatchIndex = 1;
        editor.Rules[0].FieldIndex = (int)NeighbourRuleField.SystemDescription;
        editor.Rules[0].Value = "phone";
        editor.AddRuleCommand.Execute(null); // left blank: dropped

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_appSettings.NeighbourViews);
        Assert.Equal(("Phones", false), (saved.Name, saved.MatchAll));
        var rule = Assert.Single(saved.Rules);
        Assert.Equal((NeighbourRuleField.SystemDescription, NeighbourRuleOperator.Contains, "phone"), (rule.Field, rule.Operator, rule.Value));
        _settings.Received(1).Save();
        Assert.Equal(Routes.Back, _navigation.Visits.Single().Route);
    }

    [Fact]
    public async Task Editing_keeps_a_rule_from_a_newer_version_as_written()
    {
        var group = Group("Mixed", true, (NeighbourRuleField.SystemName, NeighbourRuleOperator.Contains, "ap"));
        group.Rules.Add(new NeighbourRule { FieldName = "ChassisId", OperatorName = "Contains", Value = "00:11" });
        _appSettings.NeighbourViews = [group];
        var editor = new NeighbourGroupEditorViewModel(_settings, _navigation, _dialogs, _directory);

        editor.Load(group.Id);
        Assert.Equal("Edit neighbourhood", editor.Heading);
        Assert.True(editor.Rules[1].IsUnsupported);
        editor.Name = "Renamed";
        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_appSettings.NeighbourViews);
        Assert.Equal(("Renamed", group.Id), (saved.Name, saved.Id));
        Assert.Equal("ChassisId", saved.Rules[1].FieldName);
    }

    [Fact]
    public async Task The_list_reorders_and_deletes_groups()
    {
        _appSettings.NeighbourViews = [Group("A", true), Group("B", true), Group("C", true)];
        _dialogs.ConfirmDestructiveAsync(default!, default!, default!).ReturnsForAnyArgs(true);
        var list = new NeighbourGroupsViewModel(_settings, _navigation, _dialogs, _directory);

        list.MoveUpCommand.Execute(list.Groups[2]);
        Assert.Equal(["A", "C", "B"], _appSettings.NeighbourViews.Select(v => v.Name));

        await list.DeleteCommand.ExecuteAsync(list.Groups[0]);
        Assert.Equal(["C", "B"], _appSettings.NeighbourViews.Select(v => v.Name));
        Assert.Equal(["C", "B"], list.Groups.Select(g => g.Name));

        await list.EditCommand.ExecuteAsync(list.Groups[1]);
        Assert.Equal(_appSettings.NeighbourViews[1].Id, _navigation.Visits.Last().Parameters![Routes.GroupIdParameter]);
    }

    [Fact]
    public async Task The_list_says_what_each_group_asks_for_and_how_many_it_holds()
    {
        _appSettings.NeighbourViews =
        [
            Group("APs", true, (NeighbourRuleField.SystemName, NeighbourRuleOperator.StartsWith, "ap-")),
            Group("Kit", false, (NeighbourRuleField.SystemDescription, NeighbourRuleOperator.Contains, "Aruba"), (NeighbourRuleField.SystemDescription, NeighbourRuleOperator.Contains, "Phone")),
        ];
        var list = new NeighbourGroupsViewModel(_settings, _navigation, _dialogs, _directory);
        Assert.Equal("System name starts with \"ap-\"", list.Groups[0].Summary);

        await list.LoadCountsAsync();

        Assert.Equal("System name starts with \"ap-\" · 2", list.Groups[0].Summary);
        Assert.Equal("2 rules, any must match · 3", list.Groups[1].Summary);
    }

    [Fact]
    public async Task The_editor_counts_each_rule_and_the_group_as_its_edited()
    {
        var editor = new NeighbourGroupEditorViewModel(_settings, _navigation, _dialogs, _directory);
        editor.Load(null);
        await editor.LoadNeighboursAsync();
        var first = editor.Rules[0];
        Assert.False(first.HasMatchText);

        first.FieldIndex = (int)NeighbourRuleField.SystemDescription;
        first.Value = "Aruba";
        Assert.Equal("Matches 2", first.MatchText);

        editor.AddRuleCommand.Execute(null);
        var second = editor.Rules[1];
        second.FieldIndex = (int)NeighbourRuleField.SystemName;
        second.Value = "kitchen";
        Assert.Equal("Matches 1", second.MatchText);
        Assert.Equal("Matches 1 neighbour", editor.GroupMatchText);

        editor.SetMatchAnyCommand.Execute(null);
        Assert.True(editor.IsMatchAny);
        Assert.Equal("Matches 2 neighbours", editor.GroupMatchText);

        _dialogs.ChooseAsync("Test", Arg.Any<IReadOnlyList<string>>()).Returns("starts with");
        await editor.ChooseOperatorCommand.ExecuteAsync(second);
        Assert.Equal("starts with", second.OperatorText);
        Assert.Equal("Matches 0", second.MatchText);
    }
}
