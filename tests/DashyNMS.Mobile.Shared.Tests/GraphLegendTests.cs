using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.ViewModels;
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
    public async Task Deleted_ports_are_not_offered_and_cannot_supply_live_legend_values()
    {
        _client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(
        [
            new Port { Deleted = true, IfName = "Gi0/1", IfDescr = "Deleted duplicate", IfInOctetsRate = 999999 },
            new Port { IfName = "Gi0/1", IfDescr = "Live interface", IfInOctetsRate = 125, IfOutOctetsRate = 0 },
            new Port { Deleted = true, IfName = "Gi0/2" },
        ]);
        _client.Graphs.GetPortSvgAsync(7, "Gi0/1", "port_bits", Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), true).Returns(TwoSeries);
        var vm = new DeviceGraphsViewModel(_client, _settings);
        await vm.LoadPortAsync(7, "Gi0/1", "Live interface", "example.net");
        Assert.Equal([null, "Gi0/1"], vm.Targets.Select(t => t.IfName));
        Assert.Equal("Live interface", vm.Targets[1].Label);
        Assert.Equal(["In", "Out"], vm.Series.Select(s => s.Name));
        Assert.Equal(["1 kb/s", "0 b/s"], vm.Series.Select(s => s.Value));
    }

    [Theory]
    [InlineData(null, null, null, null, "–", "–")]
    [InlineData(125d, null, null, null, "1 kb/s (incomplete)", "–")]
    [InlineData(0d, 0d, 0d, 0d, "0 b/s", "0 b/s")]
    [InlineData(125d, 250d, 125d, 0d, "2 kb/s", "2 kb/s")]
    public async Task Device_traffic_legend_preserves_availability_and_bit_units(
        double? firstIn, double? firstOut, double? secondIn, double? secondOut,
        string expectedIn, string expectedOut)
    {
        _client.Graphs.ListAsync(7, Arg.Any<CancellationToken>()).Returns(
            [new GraphType { Name = "device_bits", Description = "Traffic" }]);
        _client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(
        [
            new Port { IfName = "eth0", IfInOctetsRate = firstIn, IfOutOctetsRate = firstOut },
            new Port { IfName = "eth1", IfInOctetsRate = secondIn, IfOutOctetsRate = secondOut },
        ]);

        var vm = await OpenAsync("device_bits");

        // Two SVG colour squares must map to both API entries, even without readings.
        Assert.Equal(["In", "Out"], vm.Series.Select(s => s.Name));
        Assert.Equal([expectedIn, expectedOut], vm.Series.Select(s => s.Value));
        Assert.Equal("2 of 2 shown", vm.ShownCount);
        Assert.NotNull(vm.GraphPage);
        Assert.False(vm.HasError);
        Assert.All(vm.Series, s => Assert.Contains(s.Value!, s.AccessibleName));
        Assert.True(vm.GraphAspect < 433.0 / 1137);
    }

    [Theory]
    [InlineData(null, "–")]
    [InlineData(double.NaN, "–")]
    [InlineData(double.PositiveInfinity, "–")]
    [InlineData(double.NegativeInfinity, "–")]
    [InlineData(-1d, "–")]
    [InlineData(101d, "–")]
    [InlineData(0d, "0%")]
    [InlineData(42d, "42%")]
    [InlineData(100d, "100%")]
    public async Task Processor_legend_preserves_unavailable_and_valid_percentages(double? reading, string expected)
    {
        _client.Graphs.ListHealthAsync(7, Arg.Any<CancellationToken>()).Returns(
            [new GraphType { Name = "device_processor", Description = "Processors" }]);
        // Deliberately reverse API order: Core's processor identity order maps the SVG.
        _client.Health.ListProcessorsAsync(7, Arg.Any<CancellationToken>()).Returns(
        [
            new ProcessorSensor { ProcessorId = 20, Description = "CPU 20", UsagePercent = 25 },
            new ProcessorSensor { ProcessorId = 10, Description = "CPU 10", UsagePercent = reading },
        ]);

        var vm = await OpenAsync("device_processor");

        Assert.Equal(["CPU 10", "CPU 20"], vm.Series.Select(s => s.Name));
        Assert.Equal([expected, "25%"], vm.Series.Select(s => s.Value));
        Assert.Equal("2 of 2 shown", vm.ShownCount);
        Assert.NotNull(vm.GraphPage);
        Assert.False(vm.HasError);
        Assert.Contains(expected, vm.Series[0].AccessibleName);
        Assert.True(vm.GraphAspect < 433.0 / 1137);
    }

    [Fact]
    public async Task Mismatched_sensor_entries_keep_the_server_legend()
    {
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { DeviceId = 7, SensorClass = "temperature", Description = "Inlet", Current = 31 },
            new Sensor { DeviceId = 7, SensorClass = "temperature", Description = "Missing", Current = null },
            new Sensor { DeviceId = 7, SensorClass = "temperature", Description = "Outlet", Current = 43 },
        ]);
        var vm = await OpenAsync();
        Assert.Empty(vm.Series);
        Assert.NotNull(vm.GraphPage);
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
        Assert.False(vm.ShowAllCommand.CanExecute(null));
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
        Assert.True(vm.ShowAllCommand.CanExecute(null));
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
        Assert.False(vm.ShowAllCommand.CanExecute(null));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Redraw_failure_is_observed_and_stale_failure_cannot_replace_current_state(bool superseded)
    {
        var vm = await OpenAsync();
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _client.Graphs.GetSensorSvgAsync(7, "device_temperature", 41, Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false)
            .Returns(pending.Task);
        vm.Series[0].ToggleCommand.Execute(null);
        var redraw = vm.RedrawThemeAsync(); // shares the pending sensor request
        if (superseded)
        {
            await vm.LoadAsync(7, "example", "device_mystery");
            vm.ErrorMessage = "Current selection error";
        }

        var page = vm.GraphPage;
        pending.SetException(new HttpRequestException("Synthetic redraw failure"));
        await redraw;

        Assert.False(vm.IsBusy);
        if (superseded) Assert.Equal(page, vm.GraphPage);
        else Assert.NotEqual(page, vm.GraphPage); // current hidden state gets the shared-scale fallback
        Assert.Equal(superseded ? "Current selection error" : "Couldn't reach the LibreNMS server. Check your connection and try again.", vm.ErrorMessage);
        await _client.Graphs.Received(1).GetSensorSvgAsync(7, "device_temperature", 41, Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false);
        if (!superseded)
        {
            vm.ShowAllCommand.Execute(null);
            Assert.False(vm.HasError);
            Assert.Null(vm.FailureOutcome);
            Assert.Null(vm.ReadFailureText);
        }
    }

    [Fact]
    public async Task Loading_or_failed_graph_cannot_restore_series_for_the_previous_graph()
    {
        var vm = await OpenAsync();
        vm.Series[0].ToggleCommand.Execute(null);
        Assert.True(vm.ShowAllCommand.CanExecute(null));
        _appSettings.GraphHiddenSeries["7:device_mystery"] = ["Keep saved"];
        var pending = new TaskCompletionSource<string>();
        _client.Graphs.GetSvgAsync(7, "device_mystery", Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), true).Returns(pending.Task);
        var load = vm.LoadAsync(7, "example", "device_mystery");
        Assert.Empty(vm.Series);
        Assert.False(vm.HasSeries);
        Assert.False(vm.SomeHidden);
        Assert.False(vm.ShowAllCommand.CanExecute(null));
        vm.ShowAllCommand.Execute(null); // direct command execution must also guard stale taps
        Assert.Equal(["Keep saved"], _appSettings.GraphHiddenSeries["7:device_mystery"]);
        pending.SetException(new HttpRequestException("Synthetic graph failure"));
        await load;
        Assert.False(vm.SomeHidden);
        Assert.False(vm.HasSeries);
        Assert.Equal(["Keep saved"], _appSettings.GraphHiddenSeries["7:device_mystery"]);
    }

    [Fact]
    public async Task Show_all_supersedes_a_first_load_sensor_failure_without_stale_error()
    {
        _appSettings.GraphHiddenSeries["7:device_temperature"] = ["Inlet"];
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _client.Graphs.GetSensorSvgAsync(7, "device_temperature", 41, Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false)
            .Returns(pending.Task);
        var vm = new DeviceGraphsViewModel(_client, _settings);
        var failures = 0;
        EventHandler<Exception> onFailed = (sender, _) =>
        {
            if (ReferenceEquals(sender, vm)) Interlocked.Increment(ref failures);
        };
        ViewModelBase.Failed += onFailed;
        try
        {
            var load = vm.LoadAsync(7, "example", "device_temperature");
            Assert.True(vm.SomeHidden);
            vm.ShowAllCommand.Execute(null);
            var current = vm.GraphPage;
            Assert.NotNull(current);
            pending.SetException(new HttpRequestException("Superseded sensor failure"));
            await load;
            Assert.Equal(current, vm.GraphPage);
            Assert.False(vm.HasError);
            Assert.Equal(0, failures);
        }
        finally
        {
            ViewModelBase.Failed -= onFailed;
        }
    }

    [Fact]
    public async Task The_range_chips_follow_the_chosen_range()
    {
        var vm = await OpenAsync();

        vm.SelectRangeCommand.Execute(vm.Ranges[2]);

        Assert.Equal([false, false, true, false, false], vm.RangeChips.Select(c => c.IsSelected));
    }
}
