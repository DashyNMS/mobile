using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

/// <summary>Port graph widgets (#172): desktop's Graph widget can show a port's graph, and the layout is shared.</summary>
public sealed class PortGraphCardTests
{
    private const string Svg = "<svg width=\"1\" height=\"1\" xmlns=\"http://www.w3.org/2000/svg\"></svg>";

    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly ILibreNmsClient _client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")]);

    public PortGraphCardTests()
    {
        _settings = Fakes.Settings(_appSettings);
        _client.Graphs.ListAsync(2, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_bits", Description = "Traffic" }]);
        _client.Graphs.ListHealthAsync(2, Arg.Any<CancellationToken>()).Returns([]);
        _client.Ports.ListForDeviceAsync(2, Arg.Any<CancellationToken>()).Returns(
        [
            new Port { PortId = 21, IfIndex = 2, IfName = "Gi0/2" },
            new Port { PortId = 20, IfIndex = 1, IfName = "Gi0/1", IfAlias = "uplink" },
            new Port { PortId = 22, IfIndex = 3, IfName = null, IfDescr = "unnamed" }, // no ifName: can't be asked for
        ]);
    }

    private DashboardWidget PortCard()
    {
        var card = DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        card.GraphDeviceId = 2;
        card.GraphName = "port_bits";
        card.GraphPortIfName = "Gi0/1";
        card.GraphTimeRangePreset = GraphTimeRangePreset.Week;
        return card;
    }

    [Fact]
    public async Task A_port_graph_from_desktop_is_fetched_as_the_ports()
    {
        PortCard();
        _client.Graphs.GetPortSvgAsync(2, "Gi0/1", "port_bits", Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false)
            .Returns(Svg);
        var vm = new DashboardViewModel(_client, _settings, new RecordingNavigation(), new DeviceBookmarks(_settings, TimeProvider.System));

        await vm.RefreshCommand.ExecuteAsync(null);

        var card = vm.Cards.Single(c => c.Type == DashboardLayout.Graph);
        Assert.Contains("data:image/svg+xml", card.GraphPage);
        Assert.Equal("Traffic · Gi0/1 · edge-rtr", card.Title);
        await _client.Graphs.Received(1).GetPortSvgAsync(2, "Gi0/1", "port_bits", GraphTimeRange.LastWeek, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false);
        await _client.Graphs.DidNotReceive().GetSvgAsync(2, "port_bits", Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Tapping_a_port_graph_opens_that_ports_graphs_on_its_graph_and_range()
    {
        PortCard();
        var navigation = new RecordingNavigation();
        var vm = new DashboardViewModel(_client, _settings, navigation, new DeviceBookmarks(_settings, TimeProvider.System));
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.OpenGraphCommand.ExecuteAsync(vm.Cards.Single(c => c.Type == DashboardLayout.Graph));

        var (route, parameters) = Assert.Single(navigation.Visits);
        Assert.Equal(Routes.DeviceGraphs, route);
        Assert.Equal("Gi0/1", parameters![Routes.PortParameter]);
        Assert.Equal("port_bits", parameters[Routes.GraphParameter]);
        Assert.Equal(GraphTimeRangePreset.Week, parameters[Routes.GraphRangeParameter]);
    }

    [Fact]
    public async Task The_set_up_offers_the_devices_graphs_or_one_of_its_named_ports()
    {
        var vm = new GraphPickerViewModel(_client, _settings, new RecordingNavigation());
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ChooseDeviceCommand.ExecuteAsync(vm.Devices.Single(d => d.DeviceId == 2));

        Assert.Equal(["The device's own graphs", "Gi0/1 · uplink", "Gi0/2"], vm.Sources.Select(s => s.Label));
        Assert.Equal(["Traffic"], vm.Graphs.Select(g => g.Description));

        vm.SelectedGraph = vm.Graphs[0];
        vm.SelectedSource = vm.Sources[1];

        Assert.Equal(["Traffic", "Unicast packets", "Broadcast and multicast packets", "Errors"], vm.Graphs.Select(g => g.Description));
        Assert.Null(vm.SelectedGraph); // device_bits isn't a port graph
    }

    [Fact]
    public async Task Saving_a_port_graph_sets_its_port_and_names_the_card_after_it()
    {
        var vm = new GraphPickerViewModel(_client, _settings, new RecordingNavigation());
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.ChooseDeviceCommand.ExecuteAsync(vm.Devices.Single(d => d.DeviceId == 2));
        vm.SelectedSource = vm.Sources.Single(s => s.IfName == "Gi0/1");
        vm.SelectedGraph = vm.Graphs.Single(g => g.Name == "port_errors");

        await vm.SaveCommand.ExecuteAsync(null);

        var card = DashboardLayout.Current(_appSettings).Single(w => w.WidgetType == DashboardLayout.Graph);
        Assert.Equal((2, "port_errors", "Gi0/1"), (card.GraphDeviceId, card.GraphName, card.GraphPortIfName));
        Assert.Equal("Errors · Gi0/1 · edge-rtr", card.Title);
    }

    [Fact]
    public async Task A_port_graph_opens_its_set_up_on_its_port_and_switching_back_clears_the_port()
    {
        var card = PortCard();
        var vm = new GraphPickerViewModel(_client, _settings, new RecordingNavigation()) { WidgetId = card.Id };

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Gi0/1", vm.SelectedSource.IfName);
        Assert.Equal("port_bits", vm.SelectedGraph?.Name);

        vm.SelectedSource = vm.Sources[0];
        vm.SelectedGraph = vm.Graphs.Single(g => g.Name == "device_bits");
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(card.GraphPortIfName);
        Assert.Equal("device_bits", card.GraphName);
    }

    [Fact]
    public void Customise_names_a_port_graphs_port() =>
        Assert.Equal("Graph · port_bits · Gi0/1", new DashboardCardOption(PortCard()).Subtitle);
}
