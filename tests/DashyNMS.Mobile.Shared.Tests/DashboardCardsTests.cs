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
    public void Desktops_list_in_order_graphs_and_sensors_as_often_as_they_appear_others_once()
    {
        var settings = new AppSettings
        {
            DashboardWidgets =
            [
                new DashboardWidget { WidgetType = "Graph" },
                new DashboardWidget { WidgetType = "SomethingNew" },
                new DashboardWidget { WidgetType = "Alerts" },
                new DashboardWidget { WidgetType = "Graph" },
                new DashboardWidget { WidgetType = "Alerts" },
            ],
        };

        Assert.Equal(["Graph", "Alerts", "Graph"], DashboardLayout.Current(settings).Select(w => w.WidgetType));
    }

    [Fact]
    public void Saving_an_order_keeps_each_cards_own_set_up()
    {
        var settings = new AppSettings();
        var graph = DashboardLayout.Add(settings, DashboardLayout.Graph);
        graph.GraphDeviceId = 7;
        var alerts = DashboardLayout.Current(settings).Single(w => w.WidgetType == DashboardLayout.Alerts);

        DashboardLayout.Save(settings, [graph, alerts]);

        Assert.Equal(["Graph", "Alerts"], settings.DashboardWidgets.Select(w => w.WidgetType));
        Assert.Equal(7, settings.DashboardWidgets[0].GraphDeviceId);
    }

    [Fact]
    public void Saving_on_the_phone_keeps_desktops_widgets_it_doesnt_show()
    {
        var unknown = new DashboardWidget { WidgetType = "SomethingNew", Title = "From desktop" };
        var secondAlerts = new DashboardWidget { WidgetType = "Alerts", Title = "Critical only" };
        var alerts = new DashboardWidget { WidgetType = "Alerts" };
        var graph = new DashboardWidget { WidgetType = "Graph" };
        var settings = new AppSettings { DashboardWidgets = [alerts, unknown, graph, secondAlerts] };

        DashboardLayout.Save(settings, [graph]); // Alerts hidden on the phone

        Assert.Equal([graph, unknown, secondAlerts], settings.DashboardWidgets);
    }

    [Fact]
    public void Graphs_and_sensors_can_be_added_again_and_again_others_only_once()
    {
        var settings = new AppSettings();

        var first = DashboardLayout.Add(settings, DashboardLayout.Graph);
        var second = DashboardLayout.Add(settings, DashboardLayout.Graph);
        var wireless = DashboardLayout.Add(settings, DashboardLayout.Wireless);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Same(wireless, DashboardLayout.Add(settings, DashboardLayout.Wireless));
        Assert.Equal(2, DashboardLayout.Current(settings).Count(w => w.WidgetType == DashboardLayout.Graph));
        Assert.Same(second, DashboardLayout.Find(settings, second.Id));
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

        // Sensors and Graph cards come from the Add buttons, so aren't listed until added.
        Assert.Equal(DashboardLayout.Kinds.Count(k => !k.AllowsSeveral), vm.Cards.Count);
        Assert.Equal(DashboardLayout.DefaultTypes, vm.Cards.Take(5).Select(c => c.Kind.Type));
        Assert.All(vm.Cards.Skip(5), c => Assert.False(c.IsShown));
        Assert.All(vm.Cards, c => Assert.True(c.CanSwitch));
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
        DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        var vm = new CustomiseDashboardViewModel(_settings, new RecordingNavigation());
        vm.MoveDownCommand.Execute(vm.Cards[0]);

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Equal(DashboardLayout.DefaultTypes, Shown(_appSettings));
        Assert.DoesNotContain(vm.Cards, c => c.Kind.Type == DashboardLayout.Graph);
    }

    [Fact]
    public async Task An_added_card_is_set_up_or_removed_not_switched_off()
    {
        var sensors = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        var navigation = new RecordingNavigation();
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var vm = new CustomiseDashboardViewModel(_settings, navigation, dialogs);
        var card = vm.Cards.Single(c => c.Kind.Type == DashboardLayout.Sensors);

        Assert.True(card.CanSetUp && card.CanRemove && !card.CanSwitch);
        Assert.False(vm.Cards.Single(c => c.Kind.Type == DashboardLayout.Alerts).CanRemove);

        await vm.SetUpCommand.ExecuteAsync(card);
        var visit = Assert.Single(navigation.Visits);
        Assert.Equal(Routes.PickSensors, visit.Route);
        Assert.Equal(sensors.Id, visit.Parameters![Routes.WidgetIdParameter]);

        await vm.RemoveCommand.ExecuteAsync(card);
        await dialogs.Received(1).ConfirmAsync("Remove card", Arg.Any<string>(), "Remove", "Cancel");
        Assert.DoesNotContain(vm.Cards, c => c.Kind.Type == DashboardLayout.Sensors);
        Assert.DoesNotContain(DashboardLayout.Sensors, Shown(_appSettings));
    }

    [Fact]
    public async Task Adding_graph_cards_makes_one_each_time_and_opens_its_set_up()
    {
        var navigation = new RecordingNavigation();
        var vm = new CustomiseDashboardViewModel(_settings, navigation);

        await vm.AddGraphCommand.ExecuteAsync(null);
        await vm.AddGraphCommand.ExecuteAsync(null);

        var graphs = vm.Cards.Where(c => c.Kind.Type == DashboardLayout.Graph).ToList();
        Assert.Equal(2, graphs.Count); // the placeholder made way
        Assert.All(graphs, g => Assert.True(g.IsShown));
        Assert.Equal(
            [.. DashboardLayout.DefaultTypes, DashboardLayout.Graph, DashboardLayout.Graph],
            Shown(_appSettings));
        Assert.Equal(graphs.Select(g => (object)g.Widget.Id), navigation.Visits.Select(v => v.Parameters![Routes.WidgetIdParameter]));
    }

    [Fact]
    public void A_card_with_its_own_title_is_listed_by_it()
    {
        var card = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        card.Title = "Core switch temps";

        var vm = new CustomiseDashboardViewModel(_settings, new RecordingNavigation());

        Assert.Equal("Core switch temps", vm.Cards.Single(c => c.Kind.Type == DashboardLayout.Sensors).Title);
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
        var card = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
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

        var sensors = vm.Cards.Single(c => c.Type == DashboardLayout.Sensors).Sensors;
        Assert.Equal(["CPU", "Inlet"], sensors.Select(s => s.Title));
        Assert.Equal(RowStatus.Critical, sensors[0].Status);
        Assert.Equal("core-sw", sensors[0].Subtitle);
    }

    [Fact]
    public async Task Several_sensors_cards_each_show_their_own_from_one_request()
    {
        var temps = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        temps.Title = "Core switch temps";
        temps.Sensors.Add(new PinnedSensor { SensorId = 10, DeviceId = 1 });
        var cpu = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        cpu.Sensors.Add(new PinnedSensor { SensorId = 20, DeviceId = 1 });
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 10, DeviceId = 1, SensorClass = "temperature", Description = "Inlet", Current = 30 },
            new Sensor { SensorId = 20, DeviceId = 1, SensorClass = "temperature", Description = "CPU", Current = 80 },
        ]);
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        var cards = vm.Cards.Where(c => c.Type == DashboardLayout.Sensors).ToList();
        Assert.Equal(["Core switch temps", "Sensors"], cards.Select(c => c.Title));
        Assert.Equal(["Inlet"], cards[0].Sensors.Select(s => s.Title));
        Assert.Equal(["CPU"], cards[1].Sensors.Select(s => s.Title));
        await _client.Sensors.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Each_graph_card_draws_its_own_graph_or_asks_to_be_set_up()
    {
        var empty = DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        var cpu = DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        cpu.GraphDeviceId = 1;
        cpu.GraphName = "device_processor";
        cpu.GraphTimeRangePreset = GraphTimeRangePreset.Week;
        var traffic = DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        traffic.GraphDeviceId = 1;
        traffic.GraphName = "device_bits";
        traffic.Title = "WAN traffic";
        _client.Graphs.GetSvgAsync(1, Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns("<svg width=\"1\" height=\"1\" xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        var cards = vm.Cards.Where(c => c.Type == DashboardLayout.Graph).ToList();
        Assert.True(cards[0].GraphNeedsSetUp);
        Assert.Null(cards[0].GraphPage);
        Assert.False(cards[1].GraphNeedsSetUp);
        Assert.Contains("data:image/svg+xml", cards[1].GraphPage);
        Assert.Equal(["Graph", "device_processor · core-sw", "WAN traffic"], cards.Select(c => c.Title));
        await _client.Graphs.Received(1).GetSvgAsync(1, "device_processor", GraphTimeRange.LastWeek, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _client.Graphs.Received(1).GetSvgAsync(1, "device_bits", Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_cards_hint_opens_that_cards_own_set_up()
    {
        var navigation = new RecordingNavigation();
        var second = DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        var vm = new DashboardViewModel(_client, _settings, navigation, new DeviceBookmarks(_settings, TimeProvider.System));
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.OpenGraphCommand.ExecuteAsync(vm.Cards.First(c => c.Widget.Id == second.Id));

        var visit = Assert.Single(navigation.Visits);
        Assert.Equal(Routes.PickGraph, visit.Route);
        Assert.Equal(second.Id, visit.Parameters![Routes.WidgetIdParameter]);
    }

    [Fact]
    public async Task A_graph_card_opens_its_own_graph_and_range()
    {
        var navigation = new RecordingNavigation();
        var card = DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        card.GraphDeviceId = 3;
        card.GraphName = "device_processor";
        card.GraphTimeRangePreset = GraphTimeRangePreset.Week;
        var vm = new DashboardViewModel(_client, _settings, navigation, new DeviceBookmarks(_settings, TimeProvider.System));

        await vm.OpenGraphCommand.ExecuteAsync(vm.Cards.Single(c => c.Widget.Id == card.Id));

        var visit = Assert.Single(navigation.Visits);
        Assert.Equal(Routes.DeviceGraphs, visit.Route);
        Assert.Equal(3, visit.Parameters![Routes.DeviceIdParameter]);
        Assert.Equal("device_processor", visit.Parameters[Routes.GraphParameter]); // not the device's first graph (#119)
        Assert.Equal(GraphTimeRangePreset.Week, visit.Parameters[Routes.GraphRangeParameter]);
    }

    [Fact]
    public async Task Wireless_asks_one_device_per_os_then_only_the_wireless_ones_down_first()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.Wireless);
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

public sealed class DashboardCardSetUpTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly ILibreNmsClient _client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")]);

    public DashboardCardSetUpTests()
    {
        _settings = Fakes.Settings(_appSettings);
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 10, DeviceId = 1, SensorClass = "temperature", Description = "Inlet", Current = 30 },
        ]);
        _client.Graphs.ListAsync(2, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_bits", Description = "Traffic" }]);
        _client.Graphs.ListHealthAsync(2, Arg.Any<CancellationToken>()).Returns([]);
    }

    [Fact]
    public async Task The_sensor_picker_sets_up_the_card_it_was_opened_for_and_names_it()
    {
        var first = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        var second = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        var vm = new SensorPickerViewModel(_client, _settings) { WidgetId = second.Id };
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, vm.CardTitle); // still the kind's

        vm.SearchText = "Inlet";
        vm.ToggleCommand.Execute(vm.Sensors[0]);
        vm.CardTitle = "  Core switch temps ";

        Assert.Empty(first.Sensors);
        Assert.Equal(10, Assert.Single(second.Sensors).SensorId);
        Assert.Equal("Core switch temps", second.Title);

        vm.CardTitle = string.Empty; // back to the kind's
        Assert.Equal("Sensors", second.Title);
    }

    [Fact]
    public async Task The_graph_picker_keeps_a_typed_title_or_names_the_card_after_its_graph()
    {
        var card = DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        var vm = new GraphPickerViewModel(_client, _settings, new RecordingNavigation()) { WidgetId = card.Id };
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.ChooseDeviceCommand.ExecuteAsync(vm.Devices.Single(d => d.DeviceId == 2));
        vm.SelectedGraph = vm.Graphs[0];
        vm.CardTitle = "WAN traffic";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(("WAN traffic", 2, "device_bits"), (card.Title, card.GraphDeviceId, card.GraphName));
        Assert.Single(DashboardLayout.Current(_appSettings), w => w.WidgetType == DashboardLayout.Graph);

        // Opened again, it shows the title it was given.
        var again = new GraphPickerViewModel(_client, _settings, new RecordingNavigation()) { WidgetId = card.Id };
        await again.LoadCommand.ExecuteAsync(null);
        Assert.Equal("WAN traffic", again.CardTitle);
    }
}
