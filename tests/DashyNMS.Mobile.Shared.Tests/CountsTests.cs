using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

/// <summary>Batch 3: the dashboard's counts, and each count opening its tab filtered (#31-#35).</summary>
public sealed class CountsTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly RecordingNavigation _navigation = new();
    private readonly ILibreNmsClient _client = Fakes.Client(
        devices:
        [
            Fakes.Device(1, "core-sw"),
            Fakes.Device(2, "edge-rtr"),
            Fakes.Device(3, "access-sw", up: false),
            Fakes.Device(4, "old-fw", disabled: true),
        ],
        alerts: [Fakes.Alert(1, 1, "critical"), Fakes.Alert(2, 3, "warning", acknowledged: true)]);

    public CountsTests() => _settings = Fakes.Settings(_appSettings);

    private DashboardViewModel NewDashboard() =>
        new(_client, _settings, _navigation, new DeviceBookmarks(_settings, TimeProvider.System), new MaintenanceScan(_client, TimeProvider.System));

    [Fact]
    public async Task Ok_counts_open_alerts_with_LibreNMSs_ok_severity()
    {
        var client = Fakes.Client(alerts:
        [
            Fakes.Alert(1, 1, "critical"),
            Fakes.Alert(2, 2, "ok"),
            Fakes.Alert(3, 2, "ok"),
            Fakes.Alert(4, 3, "ok", acknowledged: true), // acknowledged counts as that
        ]);
        var vm = new DashboardViewModel(client, _settings, _navigation, new DeviceBookmarks(_settings, TimeProvider.System));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal((1, 0, 2, 1), (vm.CriticalAlerts, vm.WarningAlerts, vm.OkAlerts, vm.AcknowledgedAlerts));
    }

    [Fact]
    public async Task Maintenance_is_counted_on_its_own_and_not_as_up()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.DeviceStatus);
        _client.Devices.IsUnderMaintenanceAsync(2, Arg.Any<CancellationToken>()).Returns(true);
        var vm = NewDashboard();

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.MaintenanceCounted;

        Assert.Equal((1, 1, 1, 1), (vm.DevicesUp, vm.DevicesDown, vm.DevicesMaintenance, vm.DevicesInactive));
        await _client.Devices.DidNotReceive().IsUnderMaintenanceAsync(4, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("critical")]
    [InlineData("warning")]
    [InlineData("acknowledged")]
    public async Task An_alert_count_opens_Alerts_showing_only_that_kind(string kind)
    {
        var vm = NewDashboard();

        await vm.ShowAlertsCommand.ExecuteAsync(kind);

        var visit = Assert.Single(_navigation.Visits);
        Assert.Equal(Routes.Alerts, visit.Route);
        Assert.Equal(kind, visit.Parameters![Routes.AlertFilterParameter]);
    }

    [Theory]
    [InlineData("up", DeviceState.Up)]
    [InlineData("maintenance", DeviceState.Maintenance)]
    [InlineData("disabled", DeviceState.Disabled)]
    public async Task A_device_count_opens_Devices_showing_only_that_state(string tapped, DeviceState state)
    {
        var vm = NewDashboard();

        await vm.ShowDevicesCommand.ExecuteAsync(tapped);

        var visit = Assert.Single(_navigation.Visits);
        Assert.Equal(Routes.Devices, visit.Route);
        Assert.Equal(state, visit.Parameters![Routes.StateParameter]);
    }

    [Fact]
    public async Task Alerts_show_only_the_tapped_kind()
    {
        var client = Fakes.Client(alerts:
        [
            Fakes.Alert(1, 1, "critical"),
            Fakes.Alert(2, 1, "warning"),
            Fakes.Alert(3, 2, "critical", acknowledged: true),
            Fakes.Alert(4, 2, "ok"),
        ]);
        var vm = new AlertsViewModel(client, _settings, Substitute.For<IDialogService>(), _navigation,
            Substitute.For<ISelfActionTracker>(), Substitute.For<IShareService>(), new RecordingBadge(), new FakeTimeProvider());
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.ShowOnly("critical");
        Assert.Equal([1], vm.Alerts.Select(a => a.Id));

        vm.ShowOnly("warning");
        Assert.Equal([2], vm.Alerts.Select(a => a.Id));

        vm.ShowOnly("ok");
        Assert.Equal([4], vm.Alerts.Select(a => a.Id));
        Assert.Equal(1, vm.OkCount);
        // Shown, not saved over the user's own filter (#81).
        Assert.True(_appSettings.Filter.ShowCritical);
        Assert.True(_appSettings.Filter.ShowUnknownSeverity);

        vm.ShowOnly("acknowledged");
        Assert.Equal([3], vm.Alerts.Select(a => a.Id));
        Assert.True(vm.HasActiveFilters);

        // The dashboard's See all.
        vm.ShowOnly("all");
        Assert.Equal(4, vm.Alerts.Count);
        Assert.False(vm.HasActiveFilters);
    }

    [Fact]
    public async Task Devices_show_only_the_tapped_state()
    {
        var bookmarks = new DeviceBookmarks(_settings, TimeProvider.System);
        var vm = new DevicesViewModel(_client, _navigation, _settings, bookmarks, TimeProvider.System);
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;

        vm.ShowOnlyState(DeviceState.Down);
        Assert.Equal([3], vm.Devices.Select(d => d.DeviceId));

        vm.ShowOnlyState(DeviceState.Disabled);
        Assert.Equal([4], vm.Devices.Select(d => d.DeviceId));
    }

    [Fact]
    public void Lowering_the_recently_viewed_count_drops_the_oldest_straight_away()
    {
        var time = new FakeTimeProvider();
        var bookmarks = new DeviceBookmarks(_settings, time);
        for (var id = 1; id <= 6; id++)
        {
            bookmarks.RecordViewed(id, $"device {id}");
        }

        bookmarks.RecentlyViewedCount = 3;

        Assert.Equal([6, 5, 4], bookmarks.RecentlyViewed.Select(r => r.DeviceId));
        Assert.Equal(3, _appSettings.RecentlyViewedDeviceCount);
    }

    [Fact]
    public void Settings_offers_the_recently_viewed_count_and_switch()
    {
        var bookmarks = new DeviceBookmarks(_settings, TimeProvider.System);
        var vm = new SettingsViewModel(
            Substitute.For<DesktopNMS.Services.ISessionService>(), _settings, Substitute.For<IDialogService>(), _navigation,
            new RecordingNotifier(), null!, new DashyNMS.Mobile.Alerts.NoAppBadge(), new InMemoryAppearance(),
            new DashyNMS.Mobile.Widgets.NoHomeWidgets(), bookmarks);

        Assert.Equal("10", vm.RecentlyViewedCountLabels[vm.RecentlyViewedCountIndex]); // desktop's default

        vm.RecentlyViewedCountIndex = 1;
        vm.ShowRecentlyViewed = false;

        Assert.Equal(5, _appSettings.RecentlyViewedDeviceCount);
        Assert.False(_appSettings.ShowRecentlyViewedDevices);
    }
}
