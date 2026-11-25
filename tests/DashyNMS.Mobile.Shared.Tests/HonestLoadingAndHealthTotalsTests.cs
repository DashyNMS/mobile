using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Models;
using DesktopNMS.Infrastructure;

namespace DashyNMS.Mobile.Tests;

public sealed class HonestLoadingAndHealthTotalsTests
{
    [Fact]
    public async Task Devices_hides_zero_counts_until_first_load_completes()
    {
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw")]);
        var settings = Fakes.Settings();
        var vm = new DevicesViewModel(client, new RecordingNavigation(), settings, new DeviceBookmarks(settings, TimeProvider.System), TimeProvider.System);

        Assert.Equal("Up", vm.UpChipText);
        Assert.Equal("Down", vm.DownChipText);
        Assert.Equal("Maintenance", vm.MaintenanceChipText);
        Assert.Equal("Disabled", vm.DisabledChipText);
        Assert.Equal(string.Empty, vm.CountText);

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;

        Assert.Equal("Up 1", vm.UpChipText);
        Assert.Equal("Down 0", vm.DownChipText);
        Assert.Contains("1 device", vm.CountText);
    }

    [Fact]
    public async Task Alerts_hides_zero_counts_and_empty_state_until_first_load_completes()
    {
        var client = Fakes.Client();
        var vm = new AlertsViewModel(
            client,
            Fakes.Settings(),
            Substitute.For<IDialogService>(),
            new RecordingNavigation(),
            Substitute.For<ISelfActionTracker>(),
            Substitute.For<IShareService>(),
            Substitute.For<IAppBadge>(),
            TimeProvider.System);

        Assert.Equal("Critical", vm.CriticalChipText);
        Assert.Equal("Warning", vm.WarningChipText);
        Assert.Equal("OK", vm.OkChipText);
        Assert.Equal("Acknowledged", vm.AcknowledgedChipText);
        Assert.Equal(string.Empty, vm.CountText);
        Assert.False(vm.IsEmpty);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("Critical 0", vm.CriticalChipText);
        Assert.Equal("Warning 0", vm.WarningChipText);
        Assert.Equal("OK 0", vm.OkChipText);
        Assert.Equal("Acknowledged 0", vm.AcknowledgedChipText);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task Health_preserves_dBm_casing_hides_preload_counts_and_reconciles_sensors_without_limits()
    {
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw")]);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 1, DeviceId = 1, SensorClass = "dbm", Description = "Gi0/1 Rx", Current = null },
            new Sensor { SensorId = 2, DeviceId = 1, SensorClass = "dbm", Description = "Gi0/2 Rx", Current = -5, LimitHigh = 0, LimitLow = -20 },
        ]);
        var vm = new HealthViewModel(client, Fakes.Settings(), new RecordingNavigation());
        vm.SelectCategoryCommand.Execute(vm.Categories.Single(c => c.SensorClass == "dbm"));

        Assert.Equal("Critical", vm.CriticalChipText);
        Assert.Equal("Warning", vm.WarningChipText);
        Assert.Equal("OK", vm.OkChipText);
        Assert.Equal(string.Empty, vm.CountText);

        await vm.RefreshIfStaleAsync();

        Assert.Equal("Critical 0", vm.CriticalChipText);
        Assert.Equal("Warning 0", vm.WarningChipText);
        Assert.Equal("OK 1", vm.OkChipText);
        Assert.Equal("2 dBm sensors · 1 without limits", vm.CountText);
        Assert.DoesNotContain("dbm", vm.CountText);
    }

    [Fact]
    public async Task Health_empty_dBm_category_preserves_dBm_casing()
    {
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw")]);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        var vm = new HealthViewModel(client, Fakes.Settings(), new RecordingNavigation());
        vm.SelectCategoryCommand.Execute(vm.Categories.Single(c => c.SensorClass == "dbm"));

        await vm.RefreshIfStaleAsync();

        Assert.Equal("LibreNMS has no dBm sensors.", vm.EmptyText);
    }
}
