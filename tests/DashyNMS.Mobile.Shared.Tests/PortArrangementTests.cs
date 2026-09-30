using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class PortArrangementTests
{
    private static Port Port(int index, string name, string oper = "up", string admin = "up", int? vlan = null, bool trunk = false, double traffic = 0, string? type = "ethernetCsmacd")
    {
        var port = new Port
        {
            PortId = index,
            DeviceId = 7,
            IfIndex = index,
            IfName = name,
            IfDescr = name,
            IfOperStatus = oper,
            IfAdminStatus = admin,
            IfVlan = vlan,
            IfType = type,
            IfInOctetsRate = traffic,
            IfOutOctetsRate = 0,
        };
        if (trunk)
        {
            port.Vlans.Add(new PortVlanMembership { Vlan = 10, Untagged = false });
            port.Vlans.Add(new PortVlanMembership { Vlan = 20, Untagged = false });
        }

        return port;
    }

    private static readonly SectionRow[] Rows =
    [
        DeviceSectionLoader.PortRow(Port(3, "Gi1/0/10", vlan: 20, traffic: 500)),
        DeviceSectionLoader.PortRow(Port(1, "Gi1/0/2", oper: "down", vlan: 10)),
        DeviceSectionLoader.PortRow(Port(2, "Gi1/0/1", vlan: 10, traffic: 900)),
        DeviceSectionLoader.PortRow(Port(4, "Te1/1/1", trunk: true, traffic: 100)),
        DeviceSectionLoader.PortRow(Port(5, "Vlan10", oper: "down", admin: "down", type: "l3ipvlan")),
    ];

    private static string[] Names(IReadOnlyList<SectionGroup> groups) => groups.SelectMany(g => g).Select(r => r.Title).ToArray();

    [Fact]
    public void By_default_one_list_in_port_id_order()
    {
        var groups = PortArrangement.Arrange(Rows, PortArrangement.SortOptions[0].Sort, PortArrangement.GroupingOptions[0].Grouping);

        Assert.Equal(["Ports"], groups.Select(g => g.Name)); // #92: by port ID, no grouping
        Assert.Equal(["Gi1/0/2", "Gi1/0/1", "Gi1/0/10", "Te1/1/1", "Vlan10"], Names(groups));
    }

    [Fact]
    public void Names_sort_with_their_numbers_as_numbers()
    {
        var groups = PortArrangement.Arrange(Rows, PortSort.Name, PortGrouping.None);

        Assert.Equal(["Gi1/0/1", "Gi1/0/2", "Gi1/0/10", "Te1/1/1", "Vlan10"], Names(groups));
    }

    [Fact]
    public void Grouped_by_vlan_with_trunks_and_ports_without_one_last()
    {
        var groups = PortArrangement.Arrange(Rows, PortSort.Traffic, PortGrouping.Vlan);

        Assert.Equal(["VLAN 10", "VLAN 20", "Trunk", "No VLAN"], groups.Select(g => g.Name));
        Assert.Equal(["Gi1/0/1", "Gi1/0/2"], groups[0].Select(r => r.Title)); // busiest first within
    }

    [Fact]
    public void Grouped_by_status_down_first_or_by_type()
    {
        Assert.Equal(["Down", "Up", "Shut down"], PortArrangement.Arrange(Rows, PortSort.PortId, PortGrouping.Status).Select(g => g.Name));
        Assert.Equal(["Ethernet", "VLAN interface"], PortArrangement.Arrange(Rows, PortSort.PortId, PortGrouping.Type).Select(g => g.Name));
    }

    [Fact]
    public async Task The_ports_page_remembers_the_choice_for_the_next_device()
    {
        var client = Fakes.Client();
        client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns([Port(2, "Gi0/2", vlan: 10), Port(1, "Gi0/1", vlan: 20)]);
        var preferences = new InMemoryPreferences();
        var vm = new DeviceSectionViewModel(new DeviceSectionLoader(client, Fakes.Settings()), new RecordingNavigation(), preferences);
        await vm.LoadAsync(7, DeviceSection.Ports);
        Assert.True(vm.IsPorts);
        Assert.Equal(["Gi0/1", "Gi0/2"], vm.Groups.SelectMany(g => g).Select(r => r.Title));

        vm.SelectedPortGrouping = vm.PortGroupingOptions.Single(o => o.Grouping == PortGrouping.Vlan);
        Assert.Equal(["VLAN 10", "VLAN 20"], vm.Groups.Select(g => g.Name));

        var next = new DeviceSectionViewModel(new DeviceSectionLoader(client, Fakes.Settings()), new RecordingNavigation(), preferences);
        Assert.Equal(PortGrouping.Vlan, next.SelectedPortGrouping.Grouping);
    }
}
