using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Widgets;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class AlertGroupCountTests
{
    private static Alert Fault(int id, bool acknowledged = false, string severity = "critical")
    {
        var alert = Fakes.Alert(id, 7, severity, acknowledged);
        alert.Source = AlertSource.Fault;
        alert.RuleId = 10;
        return alert;
    }

    [Fact]
    public void Eight_faults_are_one_badge_count_and_one_widget_row()
    {
        var alerts = Enumerable.Range(1, 8).Select(id => Fault(id)).ToArray();
        var settings = new AppSettings();
        Assert.Equal(1, AlertBadge.Count(alerts, settings));
        var snapshot = WidgetSnapshot.Build(alerts, DateTimeOffset.UtcNow, settings);
        Assert.Equal(1, snapshot.Critical);
        Assert.Equal(8, snapshot.Faults);
        Assert.Equal(8, Assert.Single(snapshot.Alerts).FaultCount);
        Assert.Equal("1 alert · 8 faults · visible to this account", snapshot.AlertTotalsText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_partly_acknowledged_group_counts_once_with_all_its_faults(bool includeAcknowledged)
    {
        var alerts = new[] { Fault(1, acknowledged: true), Fault(2) };
        var settings = new AppSettings { AlertTabBadgeIncludesAcknowledged = includeAcknowledged };
        Assert.Equal(1, AlertBadge.Count(alerts, settings));
        var snapshot = WidgetSnapshot.Build(alerts, DateTimeOffset.UtcNow, settings);
        Assert.Equal((1, 0, 2), (snapshot.Critical, snapshot.Acknowledged, snapshot.Faults));
        Assert.False(Assert.Single(snapshot.Alerts).Acknowledged);
    }

    [Fact]
    public void Fully_acknowledged_groups_follow_the_setting_on_the_badge_only()
    {
        var alerts = new[] { Fault(1, acknowledged: true), Fault(2, acknowledged: true) };
        var settings = new AppSettings { AlertTabBadgeIncludesAcknowledged = false };
        Assert.Equal(0, AlertBadge.Count(alerts, settings));
        var snapshot = WidgetSnapshot.Build(alerts, DateTimeOffset.UtcNow, settings);
        Assert.True(Assert.Single(snapshot.Alerts).Acknowledged);
        Assert.Equal(1, snapshot.Acknowledged);
        settings.AlertTabBadgeIncludesAcknowledged = true;
        Assert.Equal(1, AlertBadge.Count(alerts, settings));
    }

    [Fact]
    public void Widgets_show_every_open_alert_whatever_the_app_icon_counts()
    {
        var warning = Fault(1, severity: "warning");
        warning.RuleId = 11;
        var alerts = new[] { warning, Fault(2, acknowledged: true) };
        var settings = new AppSettings { AlertTabBadgeIncludesAcknowledged = false };
        settings.Notifications.CountFrom = AlertSeverity.Critical;
        Assert.Equal(0, AlertBadge.Count(alerts, settings));
        var snapshot = WidgetSnapshot.Build(alerts, DateTimeOffset.UtcNow, settings);
        Assert.Equal((0, 1, 1), (snapshot.Critical, snapshot.Warning, snapshot.Acknowledged));
        Assert.Equal(2, snapshot.Alerts.Count);
    }

    [Fact]
    public void A_group_counts_when_any_child_meets_severity_and_acknowledgement_settings()
    {
        var alerts = new[] { Fault(1, severity: "warning"), Fault(2, acknowledged: true) };
        var settings = new AppSettings { AlertTabBadgeIncludesAcknowledged = false };
        settings.Notifications.CountFrom = AlertSeverity.Critical;
        Assert.Equal(0, AlertBadge.Count(alerts, settings));
        settings.AlertTabBadgeIncludesAcknowledged = true;
        Assert.Equal(1, AlertBadge.Count(alerts, settings));
        var dot = new AlertTabDot();
        dot.Update(alerts, settings);
        Assert.Equal(1, dot.CriticalCount);
    }

    [Fact]
    public void Ok_severity_groups_are_included_in_the_widget_total()
    {
        var snapshot = WidgetSnapshot.Build([Fault(1, severity: "ok"), Fault(2, severity: "ok")], DateTimeOffset.UtcNow, new AppSettings());
        Assert.Equal(1, snapshot.Ok);
        Assert.Equal(1, snapshot.Active);
        Assert.Equal(1, snapshot.AlertGroups);
    }

    [Fact]
    public async Task Dashboard_counts_and_top_rows_are_groups_with_separate_fault_totals()
    {
        var settings = Fakes.Settings();
        var client = Fakes.Client(alerts: Enumerable.Range(1, 8).Select(id => Fault(id)).ToArray());
        var vm = new DashboardViewModel(client, settings, new RecordingNavigation(), new DeviceBookmarks(settings, TimeProvider.System));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(1, vm.CriticalAlerts);
        Assert.Equal(8, Assert.Single(vm.TopAlerts).Group!.FaultCount);
        Assert.Equal("1 alert · 8 faults · visible to this account", vm.AlertTotalsText);
    }

    [Fact]
    public async Task Dashboard_counts_ignore_the_app_icons_count_settings()
    {
        var settings = Fakes.Settings();
        settings.Current.Notifications.CountFrom = AlertSeverity.Critical;
        settings.Current.AlertTabBadgeIncludesAcknowledged = false;
        var warning = Fault(1, severity: "warning");
        warning.RuleId = 11;
        var acknowledged = Fault(2, acknowledged: true);
        var client = Fakes.Client(alerts: [warning, acknowledged]);
        var vm = new DashboardViewModel(client, settings, new RecordingNavigation(), new DeviceBookmarks(settings, TimeProvider.System));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal((0, 1, 1), (vm.CriticalAlerts, vm.WarningAlerts, vm.AcknowledgedAlerts));
        Assert.Equal("2 alerts · 2 faults · visible to this account", vm.AlertTotalsText);
    }

    [Fact]
    public void Widget_group_and_fault_totals_round_trip_without_the_badge_switch_disabling_widgets()
    {
        var settings = new AppSettings { ShowAlertTabBadge = false };
        var alerts = new[] { Fault(1), Fault(2) };
        Assert.Equal(0, AlertBadge.Count(alerts, settings));
        var snapshot = WidgetSnapshot.FromJson(WidgetSnapshot.Build(alerts, DateTimeOffset.UtcNow, settings).ToJson());
        Assert.Equal(1, snapshot.Critical);
        Assert.Equal(2, snapshot.Faults);
        Assert.Equal(2, Assert.Single(snapshot.Alerts).FaultCount);
    }
}
