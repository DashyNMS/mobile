using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class HealthViewModelTests
{
    private readonly ILibreNmsClient _client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")]);
    private readonly RecordingNavigation _navigation = new();

    public HealthViewModelTests()
    {
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 1, DeviceId = 2, SensorClass = "temperature", Description = "Inlet", Current = 30, LimitHigh = 70 },
            new Sensor { SensorId = 2, DeviceId = 1, SensorClass = "temperature", Description = "CPU", Current = 90, LimitHigh = 70 },
            new Sensor { SensorId = 3, DeviceId = 1, SensorClass = "temperature", Description = "Board", Current = 40, LimitHigh = 70 },
            new Sensor { SensorId = 4, DeviceId = 1, SensorClass = "dbm", Description = "Gi0/1 Rx", Current = -3 },
            new Sensor { SensorId = 5, DeviceId = 1, SensorClass = "voltage", Description = "PSU", Current = 12 }, // not a Health category
        ]);
    }

    private async Task<HealthViewModel> Loaded(string category)
    {
        var vm = new HealthViewModel(_client, Fakes.Settings(), _navigation);
        vm.SelectCategoryCommand.Execute(vm.Categories.Single(c => c.SensorClass == category));
        await vm.RefreshIfStaleAsync();
        return vm;
    }

    [Fact]
    public void Has_desktops_four_categories_in_its_order()
    {
        var vm = new HealthViewModel(_client, Fakes.Settings(), _navigation);

        Assert.Equal(["dBm", "Signal", "Temperature", "Fan speed"], vm.Categories.Select(c => c.Label));
        Assert.True(vm.Categories[0].IsSelected);
    }

    [Fact]
    public async Task Problems_first_then_by_device_with_the_device_beneath_each_sensor()
    {
        var vm = await Loaded("temperature");

        Assert.Equal(["CPU", "Board", "Inlet"], vm.Sensors.Select(s => s.Title));
        Assert.Equal(RowStatus.Critical, vm.Sensors[0].Status);
        Assert.Equal("core-sw", vm.Sensors[0].Subtitle);
        Assert.Equal((1, 0, 2), (vm.CriticalCount, vm.WarningCount, vm.OkCount));
        Assert.Equal("3 temperature sensors", vm.CountText);
        Assert.True(vm.Categories.Single(c => c.SensorClass == "temperature").IsSelected);
        Assert.False(vm.Categories[0].IsSelected);
    }

    [Fact]
    public async Task Severity_chips_and_search_narrow_the_list()
    {
        var vm = await Loaded("temperature");

        vm.ToggleOkCommand.Execute(null);
        Assert.Equal(["CPU"], vm.Sensors.Select(s => s.Title));
        Assert.Equal("1 of 3 temperature sensors", vm.CountText);

        vm.ClearFiltersCommand.Execute(null);
        vm.SearchText = "edge";
        Assert.Equal(["Inlet"], vm.Sensors.Select(s => s.Title));
        Assert.True(vm.HasActiveFilters);
    }

    [Fact]
    public async Task Tapping_a_sensor_opens_its_devices_sensors()
    {
        var vm = await Loaded("temperature");

        await vm.OpenSensorCommand.ExecuteAsync(vm.Sensors[0]);

        var visit = Assert.Single(_navigation.Visits);
        Assert.Equal(Routes.DeviceSection, visit.Route);
        Assert.Equal(1, visit.Parameters![Routes.DeviceIdParameter]);
        Assert.Equal(DeviceSection.Sensors, visit.Parameters[Routes.SectionParameter]);
    }

    [Fact]
    public async Task Coming_back_soon_after_doesnt_fetch_every_sensor_again()
    {
        var vm = await Loaded("temperature");

        await vm.RefreshIfStaleAsync();

        await _client.Sensors.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }
}
