using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Widgets;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class AlertTargetTests
{
    [Theory]
    [InlineData("fault:12", AlertSource.Fault)]
    [InlineData("alert:12", AlertSource.Alert)]
    public async Task Qualified_widget_and_notification_targets_keep_their_source(string text, AlertSource source)
    {
        var target = WidgetLink.TryParse(new Uri($"dashynms://alert/{text}?device=7"));
        Assert.Equal(new AlertKey(source, 12), target!.Key);
        var visit = await Route(target);
        Assert.Equal(Routes.AlertDetail, visit.Route);
        Assert.Equal(text, visit.Parameters![Routes.AlertKeyParameter]);
        Assert.False(visit.Parameters.ContainsKey(Routes.AlertIdParameter));
    }

    [Theory]
    [InlineData("dashynms://group/device:7/rule:3")]
    [InlineData("dashynms://group/device%3A7%2Frule%3A3")]
    public async Task Group_target_survives_launch_before_sign_in(string link)
    {
        var target = WidgetLink.TryParse(new Uri(link));
        Assert.Equal(new AlertGroupKey(7, 3), target!.Group);
        var visit = await Route(target);
        Assert.Equal(Routes.Alerts, visit.Route);
        Assert.Equal("device:7/rule:3", visit.Parameters![Routes.AlertGroupKeyParameter]);
    }

    [Fact]
    public async Task Old_numeric_widget_with_device_opens_alerts_with_an_explanation()
    {
        var target = WidgetLink.TryParse(new Uri("dashynms://alert/12?device=7"));
        var visit = await Route(target!);
        Assert.Equal(Routes.Alerts, visit.Route);
        Assert.Equal(AlertTargets.MovedNotice, visit.Parameters![Routes.AlertNoticeParameter]);
    }

    [Theory]
    [InlineData("12", null)]
    [InlineData("fault:0", null)]
    [InlineData("fault:2147483648", null)]
    [InlineData(null, "device:7/rule:0")]
    public async Task Malformed_payload_cannot_fall_through_to_a_device(string? key, string? group)
    {
        var visit = await Route(NotificationTarget.FromPayload(7, null, key, group));
        Assert.Equal(Routes.Alerts, visit.Route);
        Assert.Equal(AlertTargets.MovedNotice, visit.Parameters![Routes.AlertNoticeParameter]);
    }

    [Theory]
    [InlineData("dashynms://group/device:0/rule:3")]
    [InlineData("dashynms://group/device:7/rule:2147483648")]
    [InlineData("dashynms://alert/fault:-1")]
    [InlineData("dashynms://alert/alert:12/other")]
    public void Malformed_links_are_ignored(string link) => Assert.Null(WidgetLink.TryParse(new Uri(link)));

    private static async Task<(string Route, IDictionary<string, object>? Parameters)> Route(NotificationTarget target)
    {
        var session = Substitute.For<ISessionService>();
        var navigation = new RecordingNavigation();
        var router = new NotificationRouter(session, navigation);
        await router.OpenAsync(target);
        Assert.Empty(navigation.Visits);
        session.IsConnected.Returns(true);
        await router.MainShownAsync();
        var visit = Assert.Single(navigation.Visits);
        return (visit.Route, visit.Parameters);
    }

    [Theory]
    [InlineData(AlertSource.Fault, AlertSource.Alert)]
    [InlineData(AlertSource.Alert, AlertSource.Fault)]
    public async Task Detail_never_reads_an_id_from_the_wrong_source(AlertSource current, AlertSource requested)
    {
        var row = Fakes.Alert(12, 7, "critical");
        row.Source = current;
        var client = Fakes.Client(alerts: [row]);
        client.Alerts.GetAsync(12, Arg.Any<CancellationToken>()).Returns(row);
        var vm = new AlertDetailViewModel(client, Fakes.Settings(), Substitute.For<IDialogService>(), new RecordingNavigation(), Substitute.For<ISelfActionTracker>());
        await vm.LoadAsync(new AlertKey(requested, 12), 7);
        Assert.True(vm.IsGone);
        Assert.Null(vm.Alert);
        await client.Alerts.DidNotReceive().GetAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_group_target_expands_all_its_children_without_saving_a_filter()
    {
        var rows = new[] { Fakes.Alert(1, 7, "critical"), Fakes.Alert(2, 7, "warning", acknowledged: true), Fakes.Alert(3, 8, "critical") };
        foreach (var row in rows) { row.Source = AlertSource.Fault; row.RuleId = 3; }
        var settings = Fakes.Settings(new AppSettings());
        var vm = new AlertsViewModel(Fakes.Client(alerts: rows), settings, Substitute.For<IDialogService>(), new RecordingNavigation(), Substitute.For<ISelfActionTracker>(), Substitute.For<IShareService>(), new RecordingBadge(), TimeProvider.System);
        vm.ShowGroup(new AlertGroupKey(7, 3));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.DisplayRows.Count);
        Assert.True(vm.DisplayRows[0].IsExpanded);
        Assert.Equal(new[] { 1, 2 }, vm.DisplayRows.Skip(1).Select(a => a.Id).Order());
        settings.DidNotReceive().Save();
        vm.ShowSavedFilter();
        Assert.Equal(2, vm.DisplayRows.Count(a => !a.IsFaultChild));
        vm.ShowGroup(new AlertGroupKey(99, 3));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Empty(vm.DisplayRows);
        Assert.Equal(AlertTargets.MovedNotice, vm.NavigationNotice);
        vm.ClearFiltersCommand.Execute(null);
        Assert.Null(vm.NavigationNotice);
        Assert.NotEmpty(vm.DisplayRows);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"version\":1,\"signedIn\":true}")]
    [InlineData("{\"version\":3,\"signedIn\":true}")]
    [InlineData("{\"version\":\"2\",\"signedIn\":true}")]
    [InlineData("[]")]
    public void Snapshots_without_supported_version_are_ignored(string json) => Assert.False(WidgetSnapshot.FromJson(json).SignedIn);

    [Fact]
    public void Fault_snapshot_round_trip_keeps_both_qualified_targets()
    {
        var row = Fakes.Alert(12, 7, "critical"); row.Source = AlertSource.Fault; row.RuleId = 3;
        var snapshot = WidgetSnapshot.FromJson(WidgetSnapshot.Build([row], DateTimeOffset.UtcNow, new AppSettings()).ToJson());
        Assert.Equal(2, snapshot.Version);
        var alert = Assert.Single(snapshot.Alerts);
        Assert.Equal("fault:12", alert.Key);
        Assert.Equal("device:7/rule:3", alert.GroupKey);
    }
}
