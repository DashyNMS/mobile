using DashyNMS.Mobile.DeviceSections;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class GraphTargetTests
{
    private const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1000\" height=\"400\"><path fill=\"none\" stroke=\"rgb(0%, 40%, 0%)\" d=\"M 1 1 L 2 2\"/></svg>";
    private readonly ILibreNmsClient _client = Fakes.Client();

    public GraphTargetTests()
    {
        _client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(
        [
            new Port { PortId = 1, IfName = "Gi0/1" },
            new Port { PortId = 2, IfName = "Gi0/2" },
            new Port { PortId = 3 },
        ]);
        _client.Graphs.ListAsync(7, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_ping", Description = "Ping" }]);
        _client.Graphs.ListHealthAsync(7, Arg.Any<CancellationToken>()).Returns([]);
        _client.Graphs.ListWirelessAsync(7, Arg.Any<CancellationToken>()).Returns([]);
        _client.Graphs.GetSvgAsync(7, Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), true).Returns(Svg);
        _client.Graphs.GetPortSvgAsync(7, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), true).Returns(Svg);
    }

    [Fact]
    public async Task Switching_between_device_and_ports_retains_range_and_separates_cached_graphs()
    {
        var vm = new DeviceGraphsViewModel(_client);
        await vm.LoadAsync(7, "example.net", range: GraphTimeRange.LastWeek.Preset);
        Assert.Equal(3, vm.Targets.Count);
        Assert.Null(vm.SelectedTarget!.IfName);
        await vm.SelectTargetAsync(vm.Targets.Single(t => t.IfName == "Gi0/1"));
        Assert.Equal("Gi0/1", vm.PortIfName);
        Assert.Equal("port_bits", vm.SelectedGraph!.Name);
        Assert.Equal(GraphTimeRange.LastWeek, vm.SelectedRange.Range);
        Assert.Contains(vm.Graphs, g => g.Name == "port_errors");
        await vm.SelectTargetAsync(vm.Targets.Single(t => t.IfName == "Gi0/2"));
        await vm.SelectTargetAsync(vm.Targets.Single(t => t.IfName == "Gi0/1"));
        await vm.SelectTargetAsync(vm.Targets[0]);
        Assert.Null(vm.PortIfName);
        Assert.Equal("device_ping", vm.SelectedGraph!.Name);
        Assert.NotNull(vm.GraphPage);
        await _client.Graphs.Received(1).GetPortSvgAsync(7, "Gi0/1", "port_bits", GraphTimeRange.LastWeek, 1000, 520, Arg.Any<CancellationToken>(), true);
        await _client.Graphs.Received(1).GetPortSvgAsync(7, "Gi0/2", "port_bits", GraphTimeRange.LastWeek, 1000, 520, Arg.Any<CancellationToken>(), true);
        await _client.Graphs.Received(1).GetSvgAsync(7, "device_ping", GraphTimeRange.LastWeek, 1000, 520, Arg.Any<CancellationToken>(), true);
    }

    [Fact]
    public async Task Direct_port_link_keeps_requested_graph_range_and_target_when_port_listing_fails()
    {
        _client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns<IReadOnlyList<Port>>(_ => throw new LibreNmsApiException("Unavailable", System.Net.HttpStatusCode.ServiceUnavailable));
        var vm = new DeviceGraphsViewModel(_client);
        await vm.LoadPortAsync(7, "Gi0/9", "Uplink", "example.net", "port_errors", GraphTimeRange.LastHour.Preset);
        Assert.True(vm.HasTargetsError);
        Assert.Equal("Gi0/9", vm.SelectedTarget!.IfName);
        Assert.Equal("port_errors", vm.SelectedGraph!.Name);
        Assert.Equal(GraphTimeRange.LastHour, vm.SelectedRange.Range);
        Assert.NotNull(vm.GraphPage);
        await vm.SelectTargetAsync(vm.Targets[0]);
        Assert.Null(vm.PortIfName);
        Assert.NotNull(vm.GraphPage);
    }
}
