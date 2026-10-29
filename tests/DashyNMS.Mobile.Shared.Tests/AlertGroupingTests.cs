using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class AlertGroupingTests
{
    private static Alert Fault(int id, bool acknowledged = false)
    {
        var fault = Fakes.Alert(id, 7, "critical", acknowledged);
        fault.RuleId = 10;
        fault.Source = AlertSource.Fault;
        fault.EntityType = "processor";
        fault.EntityId = id;
        fault.EntityKey = $"processor:{id}";
        fault.FirstSeen = new DateTime(2026, 10, 1).AddMinutes(id);
        return fault;
    }

    private static async Task<AlertsViewModel> Load(Alert[] faults, AppSettings? settings = null)
    {
        var vm = new AlertsViewModel(Fakes.Client(alerts: faults), Fakes.Settings(settings), Substitute.For<IDialogService>(),
            new RecordingNavigation(), Substitute.For<ISelfActionTracker>(), Substitute.For<IShareService>(), new RecordingBadge(), TimeProvider.System);
        await vm.RefreshCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Eight_faults_are_one_expandable_group_with_named_children_and_times()
    {
        var faults = Enumerable.Range(1, 8).Select(id => Fault(id)).ToArray();
        var vm = await Load(faults);
        var header = Assert.Single(vm.DisplayRows);
        Assert.True(header.IsGroupHeader);
        Assert.Equal("1 alert group · 8 faults · visible to this account", vm.CountText);
        Assert.Equal(1, vm.CriticalCount);
        Assert.Contains("First seen", header.GroupTimesText);
        Assert.Equal(faults[0].FirstSeen, header.Group!.FirstSeen);
        await vm.OpenAlertCommand.ExecuteAsync(header);
        Assert.Equal(9, vm.DisplayRows.Count);
        Assert.All(vm.DisplayRows.Skip(1), row => Assert.StartsWith("Processor ", row.RowRule));
        await vm.OpenAlertCommand.ExecuteAsync(vm.DisplayRows[0]);
        Assert.Single(vm.DisplayRows);
    }

    [Fact]
    public async Task A_new_active_fault_keeps_a_partly_acknowledged_group_visible()
    {
        var settings = new AppSettings();
        settings.Filter.ShowAcknowledged = false;
        var vm = await Load([Fault(1, acknowledged: true), Fault(2)], settings);
        var header = Assert.Single(vm.DisplayRows);
        Assert.False(header.IsAcknowledged);
        Assert.Contains("Partly acknowledged (1/2)", header.MetaText);
        await vm.OpenAlertCommand.ExecuteAsync(header);
        Assert.Equal(3, vm.DisplayRows.Count);
        Assert.Equal(1, vm.CriticalCount);
        Assert.Equal(0, vm.AcknowledgedCount);
    }

    [Fact]
    public async Task Fully_acknowledged_groups_follow_the_include_acknowledged_filter()
    {
        var settings = new AppSettings();
        settings.Filter.ShowAcknowledged = false;
        var vm = await Load([Fault(1, acknowledged: true), Fault(2, acknowledged: true)], settings);
        Assert.Empty(vm.DisplayRows);
        Assert.Equal(1, vm.AcknowledgedCount);
        vm.ShowAcknowledged = true;
        Assert.Single(vm.DisplayRows);
    }

    [Fact]
    public async Task Thousands_of_faults_start_collapsed_and_the_worst_severity_is_shown()
    {
        var faults = Enumerable.Range(1, 3000).Select(id => Fault(id)).ToArray();
        faults[0].SeverityText = "warning";
        var vm = await Load(faults);
        Assert.Single(vm.DisplayRows);
        Assert.Equal(AlertSeverity.Critical, vm.DisplayRows[0].Severity);
        Assert.Equal(3000, vm.DisplayRows[0].Group!.FaultCount);
    }

    [Fact]
    public async Task Refresh_preserves_expansion_and_highlights_the_selected_faults_group()
    {
        var vm = await Load([Fault(1), Fault(2)]);
        await vm.OpenAlertCommand.ExecuteAsync(vm.DisplayRows[0]);
        vm.SelectKey(new AlertKey(AlertSource.Fault, 2));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.DisplayRows.Count);
        Assert.True(vm.DisplayRows[0].IsExpanded);
        Assert.True(vm.DisplayRows[0].IsSelected);
        Assert.True(vm.DisplayRows.Single(row => row.IsFaultChild && row.Id == 2).IsSelected);
    }

    [Fact]
    public async Task Recovered_faults_from_a_shared_poll_are_excluded_from_open_groups()
    {
        var recovered = Fault(1);
        recovered.StateValue = 0;
        var vm = await Load([recovered, Fault(2)]);
        var header = Assert.Single(vm.DisplayRows);
        Assert.Equal(1, header.Group!.FaultCount);
        Assert.Equal("1 alert group · 1 fault · visible to this account", vm.CountText);
    }

    [Fact]
    public void The_tab_dot_counts_each_qualifying_group_once()
    {
        var dot = new AlertTabDot();
        dot.Update([Fault(1, acknowledged: true), Fault(2), Fault(3)],
            new AppSettings { ShowAlertTabBadge = true, AlertTabBadgeIncludesAcknowledged = false });
        Assert.Equal(1, dot.CriticalCount);
        Assert.Equal(AlertTabSeverity.Critical, dot.Severity);
    }
}
