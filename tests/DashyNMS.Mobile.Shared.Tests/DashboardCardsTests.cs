using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class DashboardLayoutTests
{
    [Fact]
    public void An_empty_list_is_the_dashboard_as_it_always_was()
    {
        Assert.Equal(
            [DashboardLayout.AlertsGauge, DashboardLayout.DeviceStatus, DashboardLayout.Alerts, DashboardLayout.PinnedDevices, DashboardLayout.RecentlyViewed],
            DashboardLayout.Current(new AppSettings()).Select(w => w.WidgetType));
    }

    [Fact]
    public void Desktops_list_in_order_each_kind_once_ignoring_what_a_phone_cant_show()
    {
        var settings = new AppSettings
        {
            DashboardWidgets =
            [
                new DashboardWidget { WidgetType = "Graph" },
                new DashboardWidget { WidgetType = "SomethingNew" },
                new DashboardWidget { WidgetType = "Alerts" },
                new DashboardWidget { WidgetType = "Graph" },
            ],
        };

        Assert.Equal(["Graph", "Alerts"], DashboardLayout.Current(settings).Select(w => w.WidgetType));
    }

    [Fact]
    public void Saving_an_order_keeps_each_cards_own_set_up()
    {
        var settings = new AppSettings();
        var graph = DashboardLayout.Ensure(settings, DashboardLayout.Graph);
        graph.GraphDeviceId = 7;

        DashboardLayout.Save(settings, [DashboardLayout.Graph, DashboardLayout.Alerts]);

        Assert.Equal(["Graph", "Alerts"], settings.DashboardWidgets.Select(w => w.WidgetType));
        Assert.Equal(7, settings.DashboardWidgets[0].GraphDeviceId);
    }
}

public sealed class CustomiseDashboardViewModelTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;

    public CustomiseDashboardViewModelTests() => _settings = Fakes.Settings(_appSettings);

    private static string[] Shown(AppSettings settings) => DashboardLayout.Current(settings).Select(w => w.WidgetType).ToArray();

    [Fact]
    public void Lists_the_showing_cards_first_then_the_rest()
    {
        var vm = new CustomiseDashboardViewModel(_settings, new RecordingNavigation());

        Assert.Equal(DashboardLayout.Kinds.Count, vm.Cards.Count);
        Assert.Equal(DashboardLayout.DefaultTypes, vm.Cards.Take(5).Select(c => c.Kind.Type));
        Assert.All(vm.Cards.Skip(5), c => Assert.False(c.IsShown));
    }

    [Fact]
    public void Showing_hiding_and_moving_save_straight_away()
    {
        var vm = new CustomiseDashboardViewModel(_settings, new RecordingNavigation());

        vm.Cards.Single(c => c.Kind.Type == DashboardLayout.Wireless).IsShown = true;
        vm.Cards.Single(c => c.Kind.Type == DashboardLayout.RecentlyViewed).IsShown = false;
        vm.MoveUpCommand.Execute(vm.Cards.Single(c => c.Kind.Type == DashboardLayout.Alerts));

        Assert.Equal(
            [DashboardLayout.AlertsGauge, DashboardLayout.Alerts, DashboardLayout.DeviceStatus, DashboardLayout.PinnedDevices, DashboardLayout.Wireless],
            Shown(_appSettings));
        _settings.Received(3).Save();
    }

    [Fact]
    public void Reset_brings_back_the_standard_dashboard()
    {
        var vm = new CustomiseDashboardViewModel(_settings, new RecordingNavigation());
        vm.Cards.Single(c => c.Kind.Type == DashboardLayout.Graph).IsShown = true;
        vm.MoveDownCommand.Execute(vm.Cards[0]);

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Equal(DashboardLayout.DefaultTypes, Shown(_appSettings));
    }

    [Fact]
    public async Task Sensors_and_graph_cards_have_set_up()
    {
        var navigation = new RecordingNavigation();
        var vm = new CustomiseDashboardViewModel(_settings, navigation);

        Assert.True(vm.Cards.Single(c => c.Kind.Type == DashboardLayout.Graph).CanSetUp);
        Assert.False(vm.Cards.Single(c => c.Kind.Type == DashboardLayout.Alerts).CanSetUp);
        await vm.SetUpCommand.ExecuteAsync(vm.Cards.Single(c => c.Kind.Type == DashboardLayout.Sensors));

        Assert.Equal(Routes.PickSensors, Assert.Single(navigation.Visits).Route);
    }
}

public sealed class DashboardCardsTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly ILibreNmsClient _client = Fakes.Client(
        devices:
        [
            new Device { DeviceId = 1, Hostname = "core-sw", Status = true, Os = "ios" },
            new Device { DeviceId = 2, Hostname = "wlc-1", Status = true, Os = "arubaos" },
            new Device { DeviceId = 3, Hostname = "wlc-2", Status = false, Os = "arubaos" },
        ],
        alerts: [Fakes.Alert(1, 1, "critical"), Fakes.Alert(2, 1, "warning"), Fakes.Alert(3, 1, "warning", acknowledged: true), Fakes.Alert(4, 1, "critical")]);

    public DashboardCardsTests() => _settings = Fakes.Settings(_appSettings);

    private DashboardViewModel NewViewModel() => new(_client, _settings, new RecordingNavigation(), new DeviceBookmarks(_settings, TimeProvider.System));

    [Fact]
    public async Task The_standard_cards_dont_fetch_sensors_graphs_or_wireless()
    {
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(DashboardLayout.DefaultTypes, vm.Cards.Select(c => c.Type));
        Assert.Same(vm, vm.Cards[0].Dashboard);
        await _client.Sensors.DidNotReceiveWithAnyArgs().ListAsync(default);
        await _client.Graphs.DidNotReceiveWithAnyArgs().GetSvgAsync(default, default!, default!, default, default, default);
        await _client.Devices.DidNotReceiveWithAnyArgs().GetWirelessSensorsAsync(default, default);
    }

    [Fact]
    public async Task The_sensors_card_shows_the_picked_sensors_in_order_coloured()
    {
        var card = DashboardLayout.Ensure(_appSettings, DashboardLayout.Sensors);
        card.Sensors.Add(new PinnedSensor { SensorId = 20, DeviceId = 1 });
        card.Sensors.Add(new PinnedSensor { SensorId = 10, DeviceId = 1 });
        card.Sensors.Add(new PinnedSensor { SensorId = 99, DeviceId = 1 }); // gone from LibreNMS
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 10, DeviceId = 1, SensorClass = "temperature", Description = "Inlet", Current = 30 },
            new Sensor { SensorId = 20, DeviceId = 1, SensorClass = "temperature", Description = "CPU", Current = 80 },
        ]);
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["CPU", "Inlet"], vm.PinnedSensors.Select(s => s.Title));
        Assert.Equal(RowStatus.Critical, vm.PinnedSensors[0].Status);
        Assert.Equal("core-sw", vm.PinnedSensors[0].Subtitle);
    }

    [Fact]
    public async Task The_graph_card_draws_its_graph_or_asks_to_be_set_up()
    {
        DashboardLayout.Ensure(_appSettings, DashboardLayout.Graph);
        var vm = NewViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.GraphNeedsSetUp);
        Assert.Null(vm.GraphPage);

        var card = DashboardLayout.Ensure(_appSettings, DashboardLayout.Graph);
        card.GraphDeviceId = 1;
        card.GraphName = "device_processor";
        card.GraphTimeRangePreset = GraphTimeRangePreset.Week;
        _client.Graphs.GetSvgAsync(1, "device_processor", Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns("<svg width=\"1\" height=\"1\" xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.GraphNeedsSetUp);
        Assert.Contains("data:image/svg+xml", vm.GraphPage);
        await _client.Graphs.Received(1).GetSvgAsync(1, "device_processor", GraphTimeRange.LastWeek, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Wireless_asks_one_device_per_os_then_only_the_wireless_ones_down_first()
    {
        DashboardLayout.Ensure(_appSettings, DashboardLayout.Wireless);
        _client.Devices.GetWirelessSensorsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ci => (int)ci[0] switch
        {
            1 => Array.Empty<WirelessSensor>(),
            2 => [new WirelessSensor { SensorClass = "clients", Current = 40 }, new WirelessSensor { SensorClass = "ap-count", Current = 12 }],
            _ => [new WirelessSensor { SensorClass = "clients", Current = 5 }],
        });
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["wlc-2", "wlc-1"], vm.WirelessControllers.Select(c => c.Title));
        Assert.Equal(RowStatus.Critical, vm.WirelessControllers[0].Status);
        Assert.Equal("40 clients", vm.WirelessControllers[1].Value);
        Assert.Equal("12 access points", vm.WirelessControllers[1].Subtitle);
        Assert.Equal("2 controllers · 45 clients", vm.WirelessSummary);

        // Probing is done once: a second refresh only asks the controllers.
        _client.Devices.ClearReceivedCalls();
        await vm.RefreshCommand.ExecuteAsync(null);
        await _client.Devices.DidNotReceive().GetWirelessSensorsAsync(1, Arg.Any<CancellationToken>());
    }
}

public sealed class SensorPickerViewModelTests
{
    [Fact]
    public async Task Search_then_tap_adds_a_sensor_to_the_card_and_tapping_again_removes_it()
    {
        var appSettings = new AppSettings();
        var settings = Fakes.Settings(appSettings);
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")]);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 10, DeviceId = 1, SensorClass = "temperature", Description = "Inlet", Current = 30 },
            new Sensor { SensorId = 20, DeviceId = 2, SensorClass = "dbm", Description = "Gi0/1 Rx", Current = -3 },
        ]);
        var vm = new SensorPickerViewModel(client, settings);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Empty(vm.Sensors); // nothing picked, nothing searched

        vm.SearchText = "edge";
        Assert.Equal(["Gi0/1 Rx"], vm.Sensors.Select(s => s.Row.Title));

        vm.ToggleCommand.Execute(vm.Sensors[0]);
        var card = DashboardLayout.Current(appSettings).Single(w => w.WidgetType == DashboardLayout.Sensors);
        Assert.Equal(20, Assert.Single(card.Sensors).SensorId);
        Assert.Equal("dbm", card.Sensors[0].SensorClass);

        vm.SearchText = string.Empty;
        Assert.Single(vm.Sensors); // what's on the card
        vm.ToggleCommand.Execute(vm.Sensors[0]);
        Assert.Empty(card.Sensors);
        settings.Received(2).Save();
    }
}

public sealed class GraphPickerViewModelTests
{
    [Fact]
    public async Task Pick_a_device_then_a_graph_and_range_then_save()
    {
        var appSettings = new AppSettings();
        var settings = Fakes.Settings(appSettings);
        var navigation = new RecordingNavigation();
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")]);
        client.Graphs.ListAsync(2, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_bits", Description = "Traffic" }]);
        client.Graphs.ListHealthAsync(2, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_temperature", Description = "Temperature" }]);
        var vm = new GraphPickerViewModel(client, settings, navigation);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.DeviceSearch = "edge";
        Assert.Equal(["edge-rtr"], vm.Devices.Select(d => d.Name));
        Assert.False(vm.SaveCommand.CanExecute(null));

        await vm.ChooseDeviceCommand.ExecuteAsync(vm.Devices[0]);
        Assert.Equal(["Temperature", "Traffic"], vm.Graphs.Select(g => g.Description));
        vm.SelectedGraph = vm.Graphs[1];
        vm.SelectedRange = vm.Ranges.Single(r => r.Preset == GraphTimeRangePreset.Week);
        await vm.SaveCommand.ExecuteAsync(null);

        var card = DashboardLayout.Current(appSettings).Single(w => w.WidgetType == DashboardLayout.Graph);
        Assert.Equal((2, "device_bits", GraphTimeRangePreset.Week), (card.GraphDeviceId, card.GraphName, card.GraphTimeRangePreset));
        Assert.Equal("Traffic · edge-rtr", card.Title);
        Assert.Equal(Routes.Back, Assert.Single(navigation.Visits).Route);
    }
}
