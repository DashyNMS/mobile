using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class GroupsLocationsViewModelTests
{
    private readonly ILibreNmsClient _client = Fakes.Client(devices:
    [
        Fakes.Device(1, "core-sw", location: "London"),
        Fakes.Device(2, "access-sw", up: false, location: "Leeds"),
        Fakes.Device(3, "backup-rtr", disabled: true, location: "London"),
        Fakes.Device(4, "lab-server"),
    ]);

    private readonly RecordingNavigation _navigation = new();

    public GroupsLocationsViewModelTests()
    {
        _client.DeviceGroups.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new DeviceGroup { Id = 1, Name = "Core", Type = "static" },
            new DeviceGroup { Id = 2, Name = "Switches", Description = "Every switch", Type = "dynamic" },
            new DeviceGroup { Id = 3, Name = "Empty" },
        ]);
        _client.DeviceGroups.GetMembershipByDeviceAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, IReadOnlyList<string>> { [1] = ["Core", "Switches"], [2] = ["Switches"], [3] = ["Core"] });
        _client.Locations.ListAsync(Arg.Any<CancellationToken>())
            .Returns([new Location { Id = 1, Name = "London", Latitude = 51.5074, Longitude = -0.1278 }, new Location { Id = 2, Name = "Bristol" }]);
    }

    private async Task<GroupsLocationsViewModel> Loaded()
    {
        var vm = new GroupsLocationsViewModel(_client, _navigation);
        await vm.RefreshCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Groups_with_devices_down_come_first_with_their_counts()
    {
        var vm = await Loaded();

        Assert.Equal(["Switches", "Core", "Empty", "Not in a group"], vm.Places.Select(p => p.Title));
        var switches = vm.Places[0];
        Assert.Equal("2 devices", switches.Value);
        Assert.Equal("1 down", switches.Detail);
        Assert.Equal(RowStatus.Critical, switches.Status);
        Assert.Equal("Every switch · Dynamic", switches.Subtitle);
        Assert.Equal("1 disabled", vm.Places[1].Detail);
        Assert.Equal("0 devices", vm.Places[2].Value);
    }

    [Fact]
    public async Task Locations_come_from_the_devices_with_coordinates_where_known()
    {
        var vm = await Loaded();

        vm.ShowLocationsCommand.Execute(null);

        Assert.Equal(["Leeds", "Bristol", "London"], vm.Places.Select(p => p.Title));
        Assert.Equal("51.5074, -0.1278", vm.Places.Single(p => p.Title == "London").Subtitle);
        Assert.Equal("2 devices", vm.Places.Single(p => p.Title == "London").Value);
    }

    [Fact]
    public async Task Tapping_one_opens_the_devices_list_filtered_to_it()
    {
        var vm = await Loaded();

        await vm.OpenCommand.ExecuteAsync(vm.Places.Single(p => p.Title == "Not in a group"));
        vm.ShowLocationsCommand.Execute(null);
        await vm.OpenCommand.ExecuteAsync(vm.Places.Single(p => p.Title == "London"));

        Assert.Equal(Routes.Devices, _navigation.Visits[0].Route);
        Assert.Equal(DevicesViewModel.NoGroupKey, _navigation.Visits[0].Parameters![Routes.GroupParameter]);
        Assert.Equal("London", _navigation.Visits[1].Parameters![Routes.LocationParameter]);
    }

    [Fact]
    public async Task Search_narrows_either_list()
    {
        var vm = await Loaded();

        vm.SearchText = "switch";

        Assert.Equal(["Switches"], vm.Places.Select(p => p.Title));
    }
}
