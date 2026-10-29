using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;
using System.Net;

namespace DashyNMS.Mobile.Tests;

public sealed class AlertScopeActionTests
{
    private readonly ILibreNmsClient _client = Fakes.Client();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly ISelfActionTracker _self = Substitute.For<ISelfActionTracker>();
    private readonly HashSet<int> _acknowledged = [];
    private bool _gone;

    private static Alert Fault(int id, bool acknowledged = false, int device = 7)
    {
        var row = Fakes.Alert(id, device, "critical", acknowledged);
        row.Source = AlertSource.Fault;
        row.RuleId = 10;
        return row;
    }

    private Alert[] Current() => _gone ? [] : [Fault(1, _acknowledged.Contains(1)), Fault(2, _acknowledged.Contains(2))];

    public AlertScopeActionTests()
    {
        _client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()).Returns(_ => Current());
        _client.Alerts.GetAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(call => Current().FirstOrDefault(a => a.Id == call.Arg<int>()));
        _client.Rules.GetAsync(10, Arg.Any<CancellationToken>()).Returns(new AlertRule { Id = 10, NotifyPerEntity = false });
        _dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns("on it");
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _client.Alerts.AcknowledgeAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            _acknowledged.UnionWith([1, 2]);
            return Task.CompletedTask;
        });
    }

    private async Task<AlertsViewModel> List()
    {
        var vm = new AlertsViewModel(_client, Fakes.Settings(), _dialogs, new RecordingNavigation(), _self,
            Substitute.For<IShareService>(), new RecordingBadge(), TimeProvider.System);
        await vm.RefreshCommand.ExecuteAsync(null);
        return vm;
    }

    private async Task<AlertDetailViewModel> Detail()
    {
        var vm = new AlertDetailViewModel(_client, Fakes.Settings(), _dialogs, new RecordingNavigation(), _self);
        await vm.LoadAsync(1);
        return vm;
    }

    [Fact]
    public async Task Group_swipe_reads_back_both_faults_after_one_write()
    {
        var vm = await List();
        var before = vm.DisplayRows[0];
        Assert.Equal("Acknowledge group", before.AcknowledgeLabel);
        Assert.True(before.ShowAcknowledge);
        await vm.AcknowledgeCommand.ExecuteAsync(before);
        await _client.Alerts.Received(1).AcknowledgeAsync(1, "on it", true, Arg.Any<CancellationToken>());
        await _client.Alerts.DidNotReceive().AcknowledgeAsync(2, Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        Assert.Equal(2, vm.DisplayRows[0].Group!.AcknowledgedCount);
        Assert.Equal("Acknowledged 2 faults", vm.Selection.ResultText);
        Assert.False(before.Alert.IsAcknowledged); // the old row was never optimistically mutated
        _self.Received(1).Record(1, AlertChangeKind.Acknowledged);
        _self.Received(1).Record(2, AlertChangeKind.Acknowledged);
    }

    [Fact]
    public async Task A_partial_failure_reports_only_verified_changes()
    {
        _client.Alerts.AcknowledgeAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.Arg<int>() == 2) throw new LibreNmsApiException("Denied", HttpStatusCode.Forbidden);
            _acknowledged.Add(1);
            return Task.CompletedTask;
        });
        var vm = await List();
        await vm.AcknowledgeCommand.ExecuteAsync(vm.DisplayRows[0]);
        Assert.Equal(1, vm.DisplayRows[0].Group!.AcknowledgedCount);
        Assert.Contains("couldn't be", vm.Selection.ResultText);
        _self.Received(1).Record(1, AlertChangeKind.Acknowledged);
        _self.DidNotReceive().Record(2, AlertChangeKind.Acknowledged);
    }

    [Fact]
    public async Task Missing_target_is_reported_as_no_longer_open()
    {
        _client.Alerts.AcknowledgeAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            _gone = true;
            throw new LibreNmsApiException("No Alert by that ID", HttpStatusCode.NotFound);
        });
        var vm = await List();
        await vm.AcknowledgeCommand.ExecuteAsync(vm.DisplayRows[0]);
        Assert.Empty(vm.DisplayRows);
        Assert.Contains("no longer open", vm.Selection.ResultText);
        _self.DidNotReceiveWithAnyArgs().Record(default, default);
    }

    [Fact]
    public async Task A_resource_refusal_does_not_disable_other_groups()
    {
        var permissions = new ApiPermissions();
        permissions.NoteServerVersion(new Version(26, 10, 0));
        permissions.Learn(HttpMethod.Put, "faults/1", new LibreNmsApiException("Denied", HttpStatusCode.Forbidden, serverMessage: "Insufficient permissions to update this alert"));
        _client.Permissions.Returns(permissions);
        _client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()).Returns([Fault(1), Fault(3, device: 8)]);
        var vm = await List();
        Assert.True(vm.CanAcknowledge);
        Assert.False(vm.DisplayRows.Single(a => a.Alert.DeviceId == 7).CanChange);
        Assert.True(vm.DisplayRows.Single(a => a.Alert.DeviceId == 8).CanChange);
    }

    [Fact]
    public async Task Detail_defaults_to_group_scope_and_refreshes_after_the_write()
    {
        var vm = await Detail();
        Assert.Equal("Acknowledge group", vm.AcknowledgeLabel);
        Assert.False(vm.CanAcknowledgeFault);
        Assert.Contains("every open fault", vm.FaultScopeText);
        AlertStateChange? changed = null;
        vm.AlertChanged += (_, value) => changed = value;
        await vm.AcknowledgeCommand.ExecuteAsync(null);
        Assert.True(vm.Alert!.IsAcknowledged);
        Assert.Equal("Acknowledged 2 faults", vm.ActionResult);
        Assert.Equal(new AlertGroupKey(7, 10), changed!.Group);
    }

    [Fact]
    public async Task Single_fault_action_is_available_only_with_confirmed_per_entity_scope()
    {
        _client.Rules.GetAsync(10, Arg.Any<CancellationToken>()).Returns(new AlertRule { Id = 10, NotifyPerEntity = true, MaxEntities = 20 });
        _client.Alerts.AcknowledgeAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _acknowledged.Add(call.Arg<int>());
            return Task.CompletedTask;
        });
        var vm = await Detail();
        Assert.True(vm.CanAcknowledgeFault);
        await vm.AcknowledgeFaultCommand.ExecuteAsync(null);
        Assert.True(vm.Alert!.IsAcknowledged);
        Assert.DoesNotContain(2, _acknowledged);
        Assert.Equal("Acknowledged 1 fault", vm.ActionResult);
    }
    [Fact]
    public async Task A_partly_acknowledged_group_can_be_returned_to_active()
    {
        _acknowledged.Add(1);
        _client.Alerts.UnmuteAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            _acknowledged.Clear();
            return Task.CompletedTask;
        });
        var vm = await List();
        var header = vm.DisplayRows[0];
        Assert.True(header.ShowAcknowledge);
        Assert.True(header.ShowUnacknowledge);
        await vm.UnacknowledgeCommand.ExecuteAsync(header);
        Assert.Equal(0, vm.DisplayRows[0].Group!.AcknowledgedCount);
        Assert.Contains("Returned to active 1 fault", vm.Selection.ResultText);
    }

    [Fact]
    public async Task A_single_fault_write_rechecks_scope_if_the_rule_changes()
    {
        _client.Rules.GetAsync(10, Arg.Any<CancellationToken>()).Returns(new AlertRule { Id = 10, NotifyPerEntity = true, MaxEntities = 20 });
        var vm = await Detail();
        Assert.True(vm.CanAcknowledgeFault);
        _client.Rules.GetAsync(10, Arg.Any<CancellationToken>()).Returns(new AlertRule { Id = 10, NotifyPerEntity = false });
        await vm.AcknowledgeFaultCommand.ExecuteAsync(null);
        await _client.Alerts.DidNotReceiveWithAnyArgs().AcknowledgeAsync(default);
        Assert.Contains("every open fault", vm.ActionResult);
        Assert.False(vm.CanAcknowledgeFault);
    }

}
