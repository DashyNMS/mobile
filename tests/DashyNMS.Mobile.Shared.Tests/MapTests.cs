using DashyNMS.Mobile.Map;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class MapHtmlTests
{
    private static string Page(string name = "London", string tiles = "https://tile.openstreetmap.org/{z}/{x}/{y}.png") =>
        MapHtml.Build([new MapPinData(0, name, 51.5, -0.12, 3, "down")], tiles, openStreetMapTiles: true, dark: false, "/*leaflet*/", "/*css*/");

    [Fact]
    public void Only_this_pages_own_scripts_run_and_images_come_only_from_the_tile_server()
    {
        var page = Page();

        var csp = System.Text.RegularExpressions.Regex.Match(page, "Content-Security-Policy\" content=\"([^\"]+)\"").Groups[1].Value;
        Assert.StartsWith("default-src 'none'; script-src 'nonce-", csp);
        Assert.DoesNotContain("unsafe-inline'; img", csp.Replace("style-src 'unsafe-inline'", string.Empty));
        Assert.EndsWith("img-src https: data:", csp);
        var nonce = System.Text.RegularExpressions.Regex.Match(csp, "nonce-([^']+)").Groups[1].Value;
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(page, $"<script nonce=\"{System.Text.RegularExpressions.Regex.Escape(nonce)}\">").Count);
        Assert.NotEqual(nonce, System.Text.RegularExpressions.Regex.Match(Page(), "nonce-([^']+)").Groups[1].Value); // one-off
        Assert.Contains("OpenStreetMap\\u003C/a\\u003E contributors", page); // the credit, linked (#164)
        Assert.Contains("https://www.openstreetmap.org/copyright", page);
        Assert.Contains("<meta name=\"referrer\" content=\"strict-origin-when-cross-origin\">", page);
    }

    [Fact]
    public void A_hostile_location_name_cant_close_the_script_or_become_markup()
    {
        var page = Page("</script><script>alert(1)</script><img src=x onerror=steal()>");

        Assert.DoesNotContain("</script><script>alert", page);
        Assert.DoesNotContain("<img src=x", page);
        Assert.Contains("\\u003C/script\\u003E", page); // still there, as data
        Assert.Contains("hole.textContent", page);       // counts drawn as text
        Assert.Contains("title: title", page);           // names only as the marker's title attribute
        Assert.DoesNotContain("innerHTML", page);
    }

    [Fact]
    public void An_http_tile_server_is_allowed_only_when_desktops_setting_says_so() =>
        Assert.EndsWith("img-src http: data:\">", Page(tiles: "http://tiles.lan/{z}/{x}/{y}.png").Split('\n').First(l => l.Contains("Content-Security-Policy")).Trim());

    [Theory]
    [InlineData("dashynms-map://pins/3", "3")]
    [InlineData("DASHYNMS-MAP://pins/0", "0")]
    [InlineData("dashynms-map://pins/2,5,9", "2,5,9")] // a merged pin (#84)
    [InlineData("dashynms-map://pins/2,x", null)]
    [InlineData("dashynms-map://pins/", null)]
    [InlineData("dashynms-map://device/3", null)]
    [InlineData("https://evil.example/pins/3", null)]
    [InlineData(null, null)]
    public void Only_a_pin_tap_is_read_from_a_navigation(string? url, string? pins) =>
        Assert.Equal(pins, MapHtml.PinsFrom(url) is { } ids ? string.Join(",", ids) : null);

    [Fact]
    public void Pins_that_land_together_merge_as_on_desktop()
    {
        var page = Page();

        // Core's PinClustering, in the page: nearby pins merge and regroup on zoom (#84).
        Assert.Contains($"var radius = {MapHtml.ClusterRadius};", page);
        Assert.Contains("map.on('zoomend', draw)", page);
        Assert.Contains("' locations'", page);
    }
}

public sealed class MapViewModelTests
{
    private readonly ILibreNmsClient _client = Fakes.Client(devices:
    [
        At(Fakes.Device(1, "core-sw", location: "London"), 1),
        At(Fakes.Device(2, "access-sw", up: false, location: "London"), 1),
        At(Fakes.Device(3, "leeds-rtr", location: "Leeds"), 2),
        Fakes.Device(4, "lab-server"),
        At(Fakes.Device(5, "nowhere-sw", location: "Null Island"), 3),
    ]);

    private static Device At(Device device, int locationId)
    {
        device.LocationId = locationId;
        return device;
    }

    private readonly IMapAssets _assets = Substitute.For<IMapAssets>();
    private readonly RecordingNavigation _navigation = new();

    public MapViewModelTests()
    {
        _assets.LeafletAsync().Returns(("/*js*/", "/*css*/"));
        _client.Locations.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Location { Id = 1, Name = "London", Latitude = 51.5074, Longitude = -0.1278 },
            new Location { Id = 2, Name = "Leeds", Latitude = 53.8008, Longitude = -1.5491 },
            new Location { Id = 3, Name = "Null Island", Latitude = 0, Longitude = 0 },
        ]);
    }

    private async Task<MapViewModel> Loaded(AppSettings? settings = null)
    {
        var vm = new MapViewModel(_client, Fakes.Settings(settings), _navigation, _assets);
        await vm.RefreshCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Unknown_members_keep_their_count_and_grey_ring_share()
    {
        var unknown = System.Text.Json.JsonSerializer.Deserialize<Device>(
            """{"device_id":8,"hostname":"unknown.example.net"}""", DesktopNMS.Core.Json.LibreNmsJson.Options)!;
        unknown.LocationId = 1;
        _client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns([At(Fakes.Device(1, "up.example.net"), 1), unknown]);
        var vm = await Loaded();
        var pin = Assert.Single(vm.Locations);
        Assert.Equal((2, 1, 1, 0, 0, 0), (pin.Count, pin.Up, pin.Unknown, pin.Down, pin.Maintenance, pin.Off));
        var json = System.Text.Json.JsonSerializer.SerializeToElement(pin);
        Assert.Equal(0.5, json.GetProperty("unknown").GetDouble() / json.GetProperty("count").GetDouble());
        var grey = Assert.Single(pin.Ring, part => part.Colour == "#6E6E6E");
        var green = Assert.Single(pin.Ring, part => part.Colour == "#2EA043");
        Assert.Equal(1, grey.Count);
        Assert.Equal(1, green.Count);
        Assert.Equal(pin.Count, pin.Ring.Sum(part => part.Count));
        vm.SelectPin(pin.Id);
        Assert.Equal(2, vm.Devices.Count);
        Assert.Contains(vm.Devices, d => d.State == DeviceState.Unknown);
        Assert.Equal([pin.Id], vm.SelectedPinIds);
        vm.ClearSelectionCommand.Execute(null);
        Assert.Empty(vm.SelectedPinIds);
    }

    [Fact]
    public async Task Empty_unlocated_loading_and_failure_states_notify_without_discarding_rows()
    {
        var vm = await Loaded();
        List<string?> changed = [];
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var pending = new TaskCompletionSource<IReadOnlyList<Device>>();
        _client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(pending.Task);
        var refresh = vm.RefreshCommand.ExecuteAsync(null);
        Assert.Empty(vm.EmptyText);
        Assert.False(vm.CanChooseLocation);
        pending.SetException(new HttpRequestException("offline"));
        await refresh;
        Assert.True(vm.HasError);
        Assert.Empty(vm.EmptyText);
        Assert.Equal(2, vm.Locations.Count);
        Assert.True(vm.CanChooseLocation);
        Assert.Contains(nameof(MapViewModel.CanChooseLocation), changed);
        Assert.Contains(nameof(MapViewModel.HasEmptyText), changed);

        _client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns([Fakes.Device(9, "unlocated.example.net")]);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("No devices have locations with coordinates.", vm.EmptyText);
        Assert.True(vm.HasUnplacedText);
        Assert.False(vm.CanChooseLocation);
        Assert.Contains(nameof(MapViewModel.Locations), changed);
        Assert.Contains(nameof(MapViewModel.HasUnplacedText), changed);
        _client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("No devices.", vm.EmptyText);
        Assert.False(vm.HasUnplacedText);
    }

    [Fact]
    public async Task Native_location_choices_select_the_same_complete_membership_as_pins()
    {
        var vm = await Loaded();
        var london = Assert.Single(vm.Locations, p => p.Name == "London");
        Assert.Equal(2, london.Count);
        vm.SelectPin(london.Id);
        Assert.Equal("London", vm.SelectedLocation);
        Assert.Equal(2, vm.Devices.Count);
        Assert.True(vm.HasSingleLocation);
        Assert.Empty(vm.EmptyText);
    }

    [Fact]
    public async Task One_pin_per_location_red_when_a_device_there_is_down()
    {
        var vm = await Loaded();

        Assert.Contains("\"name\":\"London\"", vm.MapPage);
        Assert.Contains("\"count\":2,\"state\":\"down\"", vm.MapPage);
        Assert.Contains("\"name\":\"Leeds\"", vm.MapPage);
        Assert.Contains("tile.openstreetmap.org", vm.MapPage);
        Assert.Equal("2 devices have no location with coordinates, so aren't on the map.", vm.UnplacedText);
    }

    [Fact]
    public async Task Desktops_own_tile_server_setting_is_used()
    {
        var vm = await Loaded(new AppSettings { MapTileUrl = "https://tiles.example.net/{z}/{x}/{y}.png" });

        Assert.Contains("tiles.example.net", vm.MapPage);
        Assert.DoesNotContain("openstreetmap.org/copyright", vm.MapPage);
    }

    [Fact]
    public async Task Tapping_a_pin_lists_its_devices_down_first_and_they_open()
    {
        var vm = await Loaded();
        var london = System.Text.RegularExpressions.Regex.Match(vm.MapPage!, "\"id\":(\\d+),\"name\":\"London\"").Groups[1].Value;

        vm.SelectPin(int.Parse(london));

        Assert.Equal("London", vm.SelectedLocation);
        Assert.Equal(["access-sw", "core-sw"], vm.Devices.Select(d => d.Name));
        await vm.OpenDeviceCommand.ExecuteAsync(vm.Devices[0]);
        Assert.Equal(2, Assert.Single(_navigation.Visits).Parameters![Services.Routes.DeviceIdParameter]);

        vm.ClearSelectionCommand.Execute(null);
        Assert.False(vm.HasSelection);
        Assert.Empty(vm.Devices);
    }

    [Fact]
    public async Task A_merged_pin_lists_every_locations_devices_together()
    {
        var vm = await Loaded();
        var ids = System.Text.RegularExpressions.Regex.Matches(vm.MapPage!, "\"id\":(\\d+),\"name\":\"(London|Leeds)\"")
            .Select(m => int.Parse(m.Groups[1].Value))
            .ToList();

        vm.SelectPins(ids);

        Assert.Equal("2 locations", vm.SelectedLocation);
        Assert.Equal(["access-sw", "core-sw", "leeds-rtr"], vm.Devices.Select(d => d.Name)); // down first
        Assert.False(vm.HasSingleLocation); // no one location for Show in Devices

        await vm.ShowInDevicesCommand.ExecuteAsync(null);
        Assert.Empty(_navigation.Visits);
    }

    [Fact]
    public async Task A_pin_leads_to_its_location_on_the_devices_tab()
    {
        var vm = await Loaded();
        var leeds = System.Text.RegularExpressions.Regex.Match(vm.MapPage!, "\"id\":(\\d+),\"name\":\"Leeds\"").Groups[1].Value;
        vm.SelectPin(int.Parse(leeds));

        await vm.ShowInDevicesCommand.ExecuteAsync(null);

        var visit = Assert.Single(_navigation.Visits);
        Assert.Equal(Services.Routes.Devices, visit.Route);
        Assert.Equal("Leeds", visit.Parameters![Services.Routes.LocationParameter]); // #84
    }

    [Fact]
    public void A_geocoded_location_object_places_a_device_the_row_alone_wouldnt()
    {
        var device = Fakes.Device(9, "kl-sw");
        device.Location = """{"id": 7, "location": "Kuala Lumpur DC", "lat": "3.139", "lng": 101.6869}""";

        DashyNMS.Mobile.Services.DeviceLocation.CompleteFromLocationObject(device);

        Assert.Equal(7, device.LocationId); // #84
        Assert.Equal(3.139, device.Latitude);
        Assert.Equal(101.6869, device.Longitude);
    }

    [Fact]
    public async Task The_map_refits_once_it_has_its_real_size()
    {
        var vm = await Loaded();

        // Leaflet drew only part of the map when the page loaded before its final size (#84).
        Assert.Contains("map.invalidateSize()", vm.MapPage);
        Assert.Contains("addEventListener('resize'", vm.MapPage);
    }
}
