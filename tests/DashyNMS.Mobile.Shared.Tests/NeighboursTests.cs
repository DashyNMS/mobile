using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class NeighboursViewModelTests
{
    private readonly ILibreNmsClient _client = Fakes.Client(devices:
    [
        Fakes.Device(1, "core-sw"),
        Fakes.Device(2, "edge-rtr"),
        Fakes.Device(3, "access-sw", up: false),
    ]);

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly RecordingNavigation _navigation = new();

    public NeighboursViewModelTests()
    {
        _client.Links.ListAllAsync(Arg.Any<CancellationToken>()).Returns(
        [
            // core-sw Gi0/1 <-> edge-rtr Gi0/24, reported from both ends.
            new NetworkLink { Id = 1, LocalDeviceId = 1, LocalPortId = 11, RemoteDeviceId = 2, RemotePortId = 21, Protocol = "lldp" },
            new NetworkLink { Id = 2, LocalDeviceId = 2, LocalPortId = 21, RemoteDeviceId = 1, RemotePortId = 11, Protocol = "lldp" },
            // core-sw Gi0/2 <-> access-sw (down).
            new NetworkLink { Id = 3, LocalDeviceId = 1, LocalPortId = 12, RemoteDeviceId = 3, RemotePortId = 31, Protocol = "cdp" },
            // core-sw Gi0/3 -> a phone LibreNMS doesn't monitor.
            new NetworkLink { Id = 4, LocalDeviceId = 1, LocalPortId = 13, RemoteHostname = "SEP001122", RemotePort = "Port 1", RemotePlatform = "Cisco IP Phone", Protocol = "cdp" },
        ]);
        _client.Ports.ListAllStatusAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Port { PortId = 11, DeviceId = 1, IfName = "Gi0/1", IfOperStatus = "up" },
            new Port { PortId = 12, DeviceId = 1, IfName = "Gi0/2", IfOperStatus = "down" },
            new Port { PortId = 13, DeviceId = 1, IfName = "Gi0/3", IfOperStatus = "up" },
            new Port { PortId = 21, DeviceId = 2, IfName = "Gi0/24", IfOperStatus = "up" },
            new Port { PortId = 31, DeviceId = 3, IfName = "Fa0/1", IfOperStatus = "down" },
        ]);
    }

    private async Task<NeighboursViewModel> Loaded()
    {
        var settings = Fakes.Settings();
        var vm = new NeighboursViewModel(new NeighbourDirectory(_client, settings), settings, _navigation);
        await vm.RefreshCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task One_row_per_cable_with_down_ends_first()
    {
        var vm = await Loaded();

        // As Devices' rows (#98): the neighbour, then what it is and where it plugs in.
        Assert.Equal(["access-sw", "edge-rtr", "SEP001122"], vm.Links.Select(l => l.Row.Title));
        var down = vm.Links[0];
        Assert.True(down.IsProblem);
        Assert.Equal(RowStatus.Critical, down.Row.Status);
        Assert.Equal("Down: access-sw, core-sw Gi0/2, access-sw Fa0/1", down.Row.Detail);
        Assert.Equal("core-sw Gi0/1 ↔ Gi0/24", vm.Links[1].Row.Subtitle);
        Assert.Equal("LLDP", vm.Links[1].Row.Value);
        Assert.Equal("core-sw Gi0/3 ↔ Port 1", vm.Links[2].PortsText);
        Assert.Equal("Cisco IP Phone · not in LibreNMS", vm.Links[2].NoteText);
        Assert.Equal(down.Row.Detail, down.NoteText);
        Assert.Equal("3 neighbours", vm.CountText);
    }

    [Fact]
    public async Task Down_only_and_search_narrow_it()
    {
        var vm = await Loaded();

        // Up and Down both on to start; Up off leaves the down ones.
        Assert.True(vm.ShowUp && vm.ShowDown);
        Assert.Equal((2, 1), (vm.UpCount, vm.DownCount));
        vm.ToggleUpCommand.Execute(null);
        Assert.Equal(["access-sw"], vm.Links.Select(l => l.Row.Title));
        Assert.Equal("1 of 3 neighbours", vm.CountText);

        vm.ToggleUpCommand.Execute(null);
        vm.ToggleDownCommand.Execute(null);
        Assert.DoesNotContain(vm.Links, l => l.IsProblem);

        vm.ToggleDownCommand.Execute(null);
        vm.SearchText = "phone";
        Assert.Equal(["SEP001122"], vm.Links.Select(l => l.Row.Title));

        vm.SearchText = "port 1"; // an LLDP field the row doesn't show by itself
        Assert.Equal(["SEP001122"], vm.Links.Select(l => l.Row.Title));
    }

    [Fact]
    public async Task A_link_opens_on_its_own_page()
    {
        var vm = await Loaded();

        await vm.OpenCommand.ExecuteAsync(vm.Links[1]);

        var visit = _navigation.Visits.Single();
        Assert.Equal(Routes.NeighbourLink, visit.Route);
        Assert.Same(vm.Links[1], visit.Parameters![Routes.NeighbourLinkParameter]);
        await _dialogs.DidNotReceiveWithAnyArgs().ChooseAsync(default!, default!);
    }

    [Fact]
    public async Task The_link_page_shows_both_ends_and_opens_either_device()
    {
        var vm = await Loaded();
        var page = new NeighbourLinkViewModel(_navigation);

        page.Load(vm.Links[0]);

        Assert.Equal("Down", page.StatusText);
        Assert.Equal(("core-sw", "Gi0/2", "port down"), (page.Link!.Local.Name, page.Link.Local.Port, page.Link.Local.StateText));
        Assert.Equal(("access-sw", "Fa0/1", "device down"), (page.Link.Remote.Name, page.Link.Remote.Port, page.Link.Remote.StateText));
        Assert.Equal("Open access-sw", page.Link.Remote.OpenText);

        await page.OpenRemoteCommand.ExecuteAsync(null);
        await page.OpenLocalCommand.ExecuteAsync(null);
        Assert.Equal([3, 1], _navigation.Visits.Select(v => (int)v.Parameters![Routes.DeviceIdParameter]));
    }

    [Fact]
    public async Task A_neighbour_LibreNMS_doesnt_poll_shows_what_it_announces()
    {
        var vm = await Loaded();
        var page = new NeighbourLinkViewModel(_navigation);

        page.Load(vm.Links[2]);

        Assert.Equal("Up", page.StatusText);
        Assert.False(page.Link!.Remote.CanOpen);
        Assert.Equal("CDP · Cisco IP Phone", page.SubtitleText);
        Assert.Contains(new KeyValuePair<string, string>("System name", "SEP001122"), page.Fields);
        Assert.Contains(new KeyValuePair<string, string>("Port ID", "Port 1"), page.Fields);
        Assert.DoesNotContain(page.Fields, f => f.Key == "MAC address");
    }
}
