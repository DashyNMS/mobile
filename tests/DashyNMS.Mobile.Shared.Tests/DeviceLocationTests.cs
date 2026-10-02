using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Tests;

/// <summary>Locations shown by name, and the Device card's Location and Groups as links.</summary>
public sealed class DeviceLocationTests
{
    private const string Geocoded = """{"id":3,"location":"Server room A, Leeds","lat":53.8,"lng":-1.55}""";

    [Theory]
    [InlineData(Geocoded, "Server room A, Leeds")]
    [InlineData("""{"id":3,"name":"Leeds DC"}""", "Leeds DC")]
    [InlineData("  London DC ", "London DC")]
    [InlineData("{not json", "{not json")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void A_geocoded_location_shows_by_its_name(string? raw, string? expected) =>
        Assert.Equal(expected, DeviceLocation.Name(raw));

    [Fact]
    public void Devices_list_filter_and_search_use_the_name()
    {
        var item = new DeviceItem(Fakes.Device(1, "core-sw", ip: "10.0.0.1", location: Geocoded), DesktopNMS.Core.Configuration.DeviceNameStyle.Hostname);

        Assert.Equal("10.0.0.1 · Server room A, Leeds", item.Details);
        Assert.True(item.Matches("room A"));
        Assert.False(item.Matches("lat"));
    }

    [Fact]
    public async Task The_device_card_links_its_location_and_groups_to_the_devices_there()
    {
        var device = Fakes.Device(7, "core-sw", ip: "192.0.2.1", location: Geocoded);
        var client = Fakes.Client(devices: [device]);
        client.Devices.GetAsync("7", Arg.Any<CancellationToken>()).Returns(device);
        client.DeviceGroups.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(
        [
            new DesktopNMS.Core.Models.DeviceGroup { Id = 1, Name = "Core switches" },
            new DesktopNMS.Core.Models.DeviceGroup { Id = 2, Name = "Leeds" },
        ]);
        var settings = Fakes.Settings();
        var navigation = new RecordingNavigation();
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns("Leeds");
        var vm = new DeviceDetailViewModel(
            client, settings, Substitute.For<ILauncherService>(), new DeviceBookmarks(settings, TimeProvider.System),
            dialogs, navigation, sections: new DeviceSectionLoader(client, settings));

        await vm.LoadAsync(7);
        await vm.Extras;

        var location = Assert.Single(vm.Properties, p => p.Key == "Location");
        Assert.Equal("Server room A, Leeds", location.Value);
        Assert.True(location.IsLink);
        Assert.False(vm.Properties.Single(p => p.Key == "IP address").IsLink);

        await vm.OpenPropertyCommand.ExecuteAsync(location);
        await vm.OpenPropertyCommand.ExecuteAsync(vm.Properties.Single(p => p.Key == "Groups"));

        var expected = new (string Route, string Key, object Value)[]
        {
            (Routes.Devices, Routes.LocationParameter, "Server room A, Leeds"),
            (Routes.Devices, Routes.GroupParameter, "Leeds"),
        };
        Assert.Equal(expected, navigation.Visits.Select(v => (v.Route, v.Parameters!.Keys.Single(), v.Parameters.Values.Single())));
    }

    [Fact]
    public async Task The_device_card_gives_its_other_names_in_one_row()
    {
        var device = Fakes.Device(7, "10.0.0.7", sysName: "core-sw.example.net");
        device.Display = "Core switch";
        var client = Fakes.Client(devices: [device]);
        client.Devices.GetAsync("7", Arg.Any<CancellationToken>()).Returns(device);
        var settings = Fakes.Settings();
        var vm = new DeviceDetailViewModel(
            client, settings, Substitute.For<ILauncherService>(), new DeviceBookmarks(settings, TimeProvider.System),
            Substitute.For<IDialogService>(), new RecordingNavigation(), sections: new DeviceSectionLoader(client, settings));

        await vm.LoadAsync(7);

        // Titled by sysName (the default); the others in one row (#116).
        Assert.Equal("core-sw.example.net", vm.Title);
        Assert.Equal("10.0.0.7, Core switch", Assert.Single(vm.Properties, p => p.Key == "Also known as").Value);
        Assert.DoesNotContain(vm.Properties, p => p.Key is "Hostname" or "sysName" or "Display name");

        // Nothing to add: no row.
        device.Display = null;
        device.Hostname = "CORE-SW";
        await vm.LoadAsync(7);
        Assert.DoesNotContain(vm.Properties, p => p.Key == "Also known as");
    }
}
