using DashyNMS.Mobile.DeviceSections;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

/// <summary>The Graphs page's own legend (#158), as desktop's Graphs in cards.</summary>
public sealed class GraphLegendTests
{
    // An rrdtool graph with LibreNMS's legend: two lines, then a colour square each, under the time axis.
    private const string TwoSeries =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1137\" height=\"433\" viewBox=\"0 0 1137 433\">\n"
        + "<path fill=\"none\" stroke-width=\"0.6\" stroke-linecap=\"butt\" stroke-linejoin=\"miter\" stroke=\"rgb(12%, 12%, 12%)\" stroke-opacity=\"1\" stroke-miterlimit=\"10\" d=\"M 57 365 L 1065 365 \"/>\n"
        + "<path fill=\"none\" stroke-width=\"1\" stroke=\"rgb(0%, 40%, 0%)\" stroke-opacity=\"1\" d=\"M 61 200 L 70 190\"/>\n"
        + "<path fill=\"none\" stroke-width=\"1\" stroke=\"rgb(0%, 0%, 60%)\" stroke-opacity=\"1\" d=\"M 61 300 L 70 290\"/>\n"
        + "<path fill-rule=\"nonzero\" fill=\"rgb(0%, 40%, 0%)\" fill-opacity=\"1\" d=\"M 16 401.921875 L 16 409.121094 L 23.199219 409.121094 L 23.199219 401.921875 Z M 16 401.921875 \"/>\n"
        + "<path fill-rule=\"nonzero\" fill=\"rgb(0%, 0%, 60%)\" fill-opacity=\"1\" d=\"M 16 415.921875 L 16 423.121094 L 23.199219 423.121094 L 23.199219 415.921875 Z M 16 415.921875 \"/>\n"
        + "</svg>\n";

    private const string Single = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1000\" height=\"400\"><path fill=\"none\" stroke=\"rgb(0%, 40%, 0%)\" d=\"M 1 1 L 2 2\"/></svg>";

    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly ILibreNmsClient _client = Fakes.Client();

    public GraphLegendTests()
    {
        _settings = Fakes.Settings(_appSettings);
        _client.Graphs.ListAsync(7, Arg.Any<CancellationToken>()).Returns([]);
        _client.Graphs.ListHealthAsync(7, Arg.Any<CancellationToken>()).Returns(
        [
            new GraphType { Name = "device_temperature", Description = "Temperature" },
            new GraphType { Name = "device_mystery", Description = "Mystery" },
        ]);
        _client.Graphs.ListWirelessAsync(7, Arg.Any<CancellationToken>()).Returns([]);
        _client.Graphs.GetSvgAsync(7, Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), true).Returns(TwoSeries);
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 41, DeviceId = 7, SensorClass = "temperature", Description = "Outlet", Current = 43 },
            new Sensor { SensorId = 40, DeviceId = 7, SensorClass = "temperature", Description = "Inlet", Current = 31 },
            new Sensor { SensorId = 50, DeviceId = 8, SensorClass = "temperature", Description = "Elsewhere", Current = 20 },
        ]);
        _client.Graphs.GetSensorSvgAsync(7, "device_temperature", Arg.Any<int>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false).Returns(Single);
    }

    private async Task<DeviceGraphsViewModel> OpenAsync(string graph = "device_temperature")
    {
        var vm = new DeviceGraphsViewModel(_client, _settings);
        await vm.LoadAsync(7, "core-sw", graph);
        return vm;
    }

    [Fact]
    public async Task Names_each_series_from_the_api_with_its_value_and_crops_librenmss_legend()
    {
        var vm = await OpenAsync();

        Assert.Equal(["Inlet", "Outlet"], vm.Series.Select(s => s.Name)); // as LibreNMS orders them: by description
        Assert.Equal(["31 °C", "43 °C"], vm.Series.Select(s => s.Value));
        Assert.All(vm.Series, s => Assert.StartsWith("#", s.Colour));
        Assert.Equal("2 of 2 shown", vm.ShownCount);
        Assert.False(vm.SomeHidden);
        Assert.True(vm.GraphAspect < 433.0 / 1137); // legend cropped off
        await _client.Graphs.Received(1).GetSvgAsync(7, "device_temperature", GraphTimeRange.LastDay, 1000, 520, Arg.Any<CancellationToken>(), true);
    }

    [Fact]
    public async Task Turning_one_off_redraws_without_fetching_remembers_it_and_fits_the_scale_to_the_one_left()
    {
        var vm = await OpenAsync();

        vm.Series[0].ToggleCommand.Execute(null);
        await Task.Yield();

        Assert.False(vm.Series[0].IsShown);
        Assert.Equal("1 of 2 shown", vm.ShownCount);
        Assert.True(vm.SomeHidden);
        Assert.Equal(["Inlet"], _appSettings.GraphHiddenSeries["7:device_temperature"]); // desktop's key, so it's shared
        await _client.Graphs.Received(1).GetSvgAsync(7, "device_temperature", Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), true);
        await _client.Graphs.Received(1).GetSensorSvgAsync(7, "device_temperature", 41, Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false);
    }

    [Fact]
    public async Task The_last_one_shown_cant_be_turned_off_and_Show_all_brings_them_back()
    {
        var vm = await OpenAsync();
        vm.Series[0].ToggleCommand.Execute(null);

        vm.Series[1].ToggleCommand.Execute(null);
        Assert.True(vm.Series[1].IsShown);

        vm.ShowAllCommand.Execute(null);
        Assert.All(vm.Series, s => Assert.True(s.IsShown));
        Assert.False(_appSettings.GraphHiddenSeries.ContainsKey("7:device_temperature"));
    }

    [Fact]
    public async Task Press_and_hold_shows_only_that_one()
    {
        var vm = await OpenAsync();

        vm.Series[1].OnlyCommand.Execute(null);

        Assert.Equal([false, true], vm.Series.Select(s => s.IsShown));
    }

    [Fact]
    public async Task Hidden_series_stay_hidden_when_the_device_is_opened_again()
    {
        _appSettings.GraphHiddenSeries["7:device_temperature"] = ["Outlet", "Gone since"];

        var vm = await OpenAsync();

        Assert.Equal([true, false], vm.Series.Select(s => s.IsShown));
    }

    [Fact]
    public async Task A_graph_core_cant_name_keeps_librenmss_own_legend()
    {
        var vm = await OpenAsync("device_mystery");

        Assert.False(vm.HasSeries);
        Assert.Empty(vm.Series);
        Assert.Equal(433.0 / 1137, vm.GraphAspect, 3); // uncropped
    }

    [Fact]
    public async Task The_range_chips_follow_the_chosen_range()
    {
        var vm = await OpenAsync();

        vm.SelectRangeCommand.Execute(vm.Ranges[2]);

        Assert.Equal([false, false, true, false, false], vm.RangeChips.Select(c => c.IsSelected));
    }
}
