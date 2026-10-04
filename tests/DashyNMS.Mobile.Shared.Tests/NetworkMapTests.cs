using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class NetworkMapViewModelTests
{
    private readonly ILibreNmsClient _client = Fakes.Client(devices:
    [
        Fakes.Device(1, "core-sw", location: "London"),
        Fakes.Device(2, "dist-sw", location: "Leeds"),
        Fakes.Device(3, "access-sw", up: false, location: "Leeds"),
        Fakes.Device(4, "lab-server", location: "Leeds"),
    ]);

    private readonly RecordingNavigation _navigation = new();

    public NetworkMapViewModelTests()
    {
        _client.Links.ListAllAsync(Arg.Any<CancellationToken>()).Returns(
        [
            // core-sw <-> dist-sw, reported from both ends: one cable.
            new NetworkLink { LocalDeviceId = 1, LocalPortId = 11, RemoteDeviceId = 2, RemotePortId = 21, RemotePort = "Te1/0/1" },
            new NetworkLink { LocalDeviceId = 2, LocalPortId = 21, RemoteDeviceId = 1, RemotePortId = 11, RemotePort = "Te1/1/1" },
            // dist-sw -> access-sw, from one end only.
            new NetworkLink { LocalDeviceId = 2, LocalPortId = 22, RemoteDeviceId = 3, RemotePort = "Gi0/49" },
            // A phone LibreNMS doesn't monitor: left out.
            new NetworkLink { LocalDeviceId = 3, LocalPortId = 31, RemoteDeviceId = null, RemotePort = "Port 1" },
        ]);
        _client.Ports.ListAllNamesAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Port { PortId = 11, IfName = "Te1/1/1" },
            new Port { PortId = 21, IfName = "Te1/0/1" },
            new Port { PortId = 22, IfName = "Gi1/0/48" },
        ]);
    }

    private async Task<NetworkMapViewModel> Loaded()
    {
        var vm = new NetworkMapViewModel(_client, Fakes.Settings(), _navigation);
        await vm.RefreshCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Draws_monitored_devices_joined_by_their_links_one_line_per_pair()
    {
        var vm = await Loaded();

        Assert.Equal(["access-sw", "core-sw", "dist-sw"], vm.Nodes.Select(n => n.Name).Order());
        Assert.Equal(2, vm.Edges.Count);
        Assert.Equal("3 devices · 2 connections", vm.SummaryText);
        Assert.True(vm.Edges.Single(e => e.Touches(vm.Nodes.Single(n => n.Name == "access-sw"))).IsToOfflineDevice); // dotted
        Assert.DoesNotContain(vm.Nodes, n => n.Name == "lab-server"); // no links: left out by default
    }

    [Fact]
    public async Task Filtering_by_location_keeps_only_the_links_inside_it()
    {
        var vm = await Loaded();
        Assert.Equal(["All locations", "Leeds (3)", "London (1)"], vm.LocationOptions.Select(o => o.Label));

        vm.SelectedLocation = vm.LocationOptions.Single(o => o.Key == "Leeds");
        await vm.RebuildAsync();

        Assert.Equal(["access-sw", "dist-sw"], vm.Nodes.Select(n => n.Name).Order());
        Assert.Single(vm.Edges);

        vm.ToggleUnlinkedDevicesCommand.Execute(null);
        await vm.RebuildAsync();
        Assert.Contains(vm.Nodes, n => n.Name == "lab-server"); // shown when asked, as on desktop
    }

    [Fact]
    public async Task Filtering_by_group_as_desktop_does_and_with_a_location_needs_both()
    {
        _client.DeviceGroups.GetMembershipByDeviceAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<int, IReadOnlyList<string>>
        {
            [1] = ["Core"],
            [2] = ["Core", "Switches"],
            [3] = ["Switches"],
        });
        var vm = await Loaded();
        Assert.Equal(["All groups", "Core (2)", "Switches (2)"], vm.GroupOptions.Select(o => o.Label));

        vm.SelectedGroup = vm.GroupOptions.Single(o => o.Key == "Core");
        await vm.RebuildAsync();
        Assert.Equal(["core-sw", "dist-sw"], vm.Nodes.Select(n => n.Name).Order());

        // With Leeds too, only dist-sw is in both: no links left to draw.
        vm.SelectedLocation = vm.LocationOptions.Single(o => o.Key == "Leeds");
        await vm.RebuildAsync();
        Assert.True(vm.IsEmpty);
        Assert.Equal("No links between devices in this group at this location.", vm.EmptyText);
    }

    [Fact]
    public async Task Search_selects_and_centres_the_best_match_and_lists_its_connections()
    {
        var vm = await Loaded();
        NetworkNode? centred = null;
        vm.CenterOnRequested += (_, node) => centred = node;

        vm.SearchText = "dist";

        Assert.Equal("dist-sw", vm.SelectedNode?.Name);
        Assert.Same(vm.SelectedNode, centred);
        Assert.Equal(
            ["access-sw Gi1/0/48 → Gi0/49", "core-sw Te1/0/1 → Te1/1/1"],
            vm.SelectedConnections.Select(c => $"{c.NeighbourName} {c.Ports}"));

        // Tapping a connection goes to that device.
        vm.SelectNeighbourCommand.Execute(vm.SelectedConnections[1]);
        Assert.Equal("core-sw", vm.SelectedNode?.Name);

        await vm.OpenSelectedDeviceCommand.ExecuteAsync(null);
        var visit = Assert.Single(_navigation.Visits);
        Assert.Equal(Routes.DeviceDetail, visit.Route);
        Assert.Equal(1, visit.Parameters![Routes.DeviceIdParameter]);
    }

    private sealed class MemoryLayouts : DesktopNMS.Core.Topology.IMapLayoutStore
    {
        private readonly Dictionary<string, Dictionary<int, DesktopNMS.Core.Topology.MapPoint>> _saved = [];

        public IReadOnlyDictionary<int, DesktopNMS.Core.Topology.MapPoint> Get(string scopeKey) =>
            _saved.TryGetValue(scopeKey, out var positions) ? new(positions) : new Dictionary<int, DesktopNMS.Core.Topology.MapPoint>();

        public void Save(string scopeKey, IReadOnlyDictionary<int, DesktopNMS.Core.Topology.MapPoint> positions) =>
            _saved[scopeKey] = new(positions);

        public void Clear(string scopeKey) => _saved.Remove(scopeKey);
    }

    [Fact]
    public async Task A_dragged_device_stays_where_it_was_put_until_the_layout_is_reset()
    {
        var layouts = new MemoryLayouts();
        var vm = new NetworkMapViewModel(_client, Fakes.Settings(), _navigation, layouts);
        await vm.RefreshCommand.ExecuteAsync(null);

        var core = vm.Nodes.Single(n => n.Name == "core-sw");
        core.X = 1234;
        core.Y = -567;
        vm.NodeMoved(core);

        var again = new NetworkMapViewModel(_client, Fakes.Settings(), _navigation, layouts);
        await again.RefreshCommand.ExecuteAsync(null);
        var kept = again.Nodes.Single(n => n.Name == "core-sw");
        Assert.Equal((1234.0, -567.0), (kept.X, kept.Y)); // #86, as desktop remembers it

        await again.ResetLayoutCommand.ExecuteAsync(null);
        Assert.NotEqual(1234.0, again.Nodes.Single(n => n.Name == "core-sw").X);
    }

    [Fact]
    public async Task Neighbours_librenms_doesnt_monitor_join_their_switch_when_asked()
    {
        _client.Ports.ListAllStatusAsync(Arg.Any<CancellationToken>()).Returns(
            [new Port { PortId = 31, DeviceId = 3, IfName = "Gi0/1", IfOperStatus = "up", IfAdminStatus = "up" }]);
        var vm = await Loaded();
        Assert.DoesNotContain(vm.Nodes, n => n.IsNeighbour); // off by default

        vm.ToggleNeighboursCommand.Execute(null);
        await vm.RebuildAsync();

        var phone = Assert.Single(vm.Nodes, n => n.IsNeighbour);
        Assert.Equal("Port 1", phone.Name);           // unnamed: its port
        Assert.True(phone.DeviceId < 0);             // an id of the map's own, never a device's
        Assert.Equal(DeviceState.Down, phone.State);  // follows its switch, access-sw, which is down
        Assert.Contains(vm.Edges, e => e.Touches(phone) && e.Other(phone).Name == "access-sw");
        Assert.EndsWith("· 1 neighbour", vm.SummaryText);

        vm.Select(phone);
        Assert.False(vm.HasSelectedDevice); // nothing to open
    }

    [Fact]
    public void Is_a_network_page_in_more()
    {
        Assert.Equal("Network", AppPages.Group(AppPage.NetworkMap));
        Assert.Equal("Network map", AppPages.Title(AppPage.NetworkMap));
        Assert.Equal("tab_networkmap.png", AppPages.Icon(AppPage.NetworkMap));
        Assert.Equal(Routes.NetworkMap, AppPages.PushRoute(AppPage.NetworkMap));
    }

    [Fact]
    public async Task Location_and_group_are_chips_that_ask_which()
    {
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ChooseAsync("Location", Arg.Any<IReadOnlyList<string>>()).Returns("Leeds (3)");
        var vm = new NetworkMapViewModel(_client, Fakes.Settings(), _navigation, dialogs: dialogs);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Location ▾", vm.LocationChipText);
        Assert.False(vm.IsLocationFiltered);

        await vm.ChooseLocationCommand.ExecuteAsync(null);

        await dialogs.Received(1).ChooseAsync("Location", Arg.Is<IReadOnlyList<string>>(l => l.SequenceEqual(new[] { "All locations", "Leeds (3)", "London (1)" })));
        Assert.Equal("Leeds", vm.SelectedLocation.Key);
        Assert.Equal("Leeds ▾", vm.LocationChipText);
        Assert.True(vm.IsLocationFiltered);

        // Cancelled: nothing changes.
        dialogs.ChooseAsync("Location", Arg.Any<IReadOnlyList<string>>()).Returns((string?)null);
        await vm.ChooseLocationCommand.ExecuteAsync(null);
        Assert.Equal("Leeds", vm.SelectedLocation.Key);
    }

    [Fact]
    public async Task The_selected_devices_card_starts_folded_and_opens_to_its_connections()
    {
        var vm = await Loaded();

        vm.Select(vm.Nodes.Single(n => n.Name == "dist-sw"));
        Assert.False(vm.IsSelectionExpanded);
        Assert.Equal("2 connections ▾", vm.ConnectionsText);

        vm.ToggleSelectionExpandedCommand.Execute(null);
        Assert.True(vm.IsSelectionExpanded);
        Assert.Equal("2 connections ▴", vm.ConnectionsText);

        // Another device starts folded again.
        vm.Select(vm.Nodes.Single(n => n.Name == "core-sw"));
        Assert.False(vm.IsSelectionExpanded);
        Assert.Equal("1 connection ▾", vm.ConnectionsText);
    }

    [Fact]
    public async Task Reset_layout_asks_first()
    {
        var dialogs = Substitute.For<IDialogService>();
        var layouts = Substitute.For<DesktopNMS.Core.Topology.IMapLayoutStore>();
        layouts.Get(Arg.Any<string>()).Returns(new Dictionary<int, DesktopNMS.Core.Topology.MapPoint>());
        var vm = new NetworkMapViewModel(_client, Fakes.Settings(), _navigation, layouts, dialogs: dialogs);
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.ResetLayoutCommand.ExecuteAsync(null); // declined
        layouts.DidNotReceive().Clear(Arg.Any<string>());

        dialogs.ConfirmDestructiveAsync(default!, default!, default!).ReturnsForAnyArgs(true);
        await vm.ResetLayoutCommand.ExecuteAsync(null);
        layouts.Received(1).Clear(vm.ScopeKey);
        await dialogs.Received().ConfirmDestructiveAsync("Reset layout", Arg.Is<string>(m => m.Contains("\"All devices\"") && m.EndsWith(Confirmations.CannotBeUndone)), "Reset layout");
    }
}
