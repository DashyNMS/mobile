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

    /// <summary>Group by VLAN lists Trunk first and No VLAN last, with the busiest port first inside a group.</summary>
    [Fact]
    public void Grouped_by_vlan_with_trunks_first_and_ports_without_one_last()
    {
        var groups = PortArrangement.Arrange(Rows, PortSort.Traffic, PortGrouping.Vlan);

        Assert.Equal(["Trunk", "VLAN 10", "VLAN 20", "No VLAN"], groups.Select(g => g.Name));
        Assert.Equal(["Gi1/0/1", "Gi1/0/2"], groups[1].Select(r => r.Title)); // busiest first within
    }

    /// <summary>A port with an ifVlan and typed memberships, as LibreNMS reports them; each tuple is a VLAN number and whether it is untagged.</summary>
    private static Port Member(int index, string name, int? ifVlan, params (int Vlan, bool Untagged)[] vlans)
    {
        var port = Port(index, name, vlan: ifVlan);
        foreach (var (vlan, untagged) in vlans)
        {
            port.Vlans.Add(new PortVlanMembership { Vlan = vlan, Untagged = untagged });
        }

        return port;
    }

    /// <summary>The VLAN groups for ports on a device that does or does not report memberships, built through the loader so the card text is the real one.</summary>
    private static IReadOnlyList<SectionGroup> ByVlan(bool deviceReportsMemberships, params Port[] ports) =>
        PortArrangement.Arrange(ports.Select(p => DeviceSectionLoader.PortRow(p, deviceReportsMemberships)), PortSort.PortId, PortGrouping.Vlan);

    /// <summary>A trunk with a stray ifVlan groups as Trunk, matching its card (desktop#571).</summary>
    [Fact]
    public void A_port_carrying_several_vlans_is_a_trunk_whatever_its_ifvlan()
    {
        // DashyNMS/desktop#571: ifVlan 1 with memberships 10 and 20 sat under "VLAN 1" while the card listed 10 and 20.
        var groups = ByVlan(true, Member(1, "Gi1/0/1", 1, (10, true), (20, false)));

        var group = Assert.Single(groups);
        Assert.Equal("Trunk", group.Name);
        Assert.Equal("10, 20", Assert.Single(group).PortVlanText);
    }

    /// <summary>Ports carrying only one VLAN share its group, tagged or untagged, and repeated or zero memberships don't make a trunk.</summary>
    [Fact]
    public void A_single_tagged_vlan_groups_with_the_untagged_ports_on_it()
    {
        var groups = ByVlan(true,
            Member(1, "Gi1/0/1", null, (10, true)),
            Member(2, "Gi1/0/2", 1, (10, false)),
            Member(3, "Gi1/0/3", null, (10, false), (10, true), (0, false)));

        var group = Assert.Single(groups);
        Assert.Equal("VLAN 10", group.Name);
        Assert.Equal(["Gi1/0/1", "Gi1/0/2", "Gi1/0/3"], group.Select(r => r.Title)); // duplicates count once, so no trunk
    }

    /// <summary>Groups run Trunk, then VLANs by number (2, 10, 100, not as text), then No VLAN.</summary>
    [Fact]
    public void Vlan_groups_put_trunks_first_then_vlans_by_number_then_no_vlan()
    {
        var groups = ByVlan(true,
            Member(1, "Gi1/0/1", 1, (10, false), (20, false)),
            Member(2, "Gi1/0/2", 5),
            Member(3, "Gi1/0/3", null, (100, true)),
            Member(4, "Gi1/0/4", null, (2, true)),
            Member(5, "Gi1/0/5", 1, (10, false)),
            Member(6, "Gi1/0/6", null, (2, false), (100, false)));

        Assert.Equal(["Trunk", "VLAN 2", "VLAN 10", "VLAN 100", "No VLAN"], groups.Select(g => g.Name));
        Assert.Equal(["Gi1/0/1", "Gi1/0/6"], groups[0].Select(r => r.Title));
    }

    /// <summary>A device that reports no memberships falls back to each port's ifVlan, named Access/native VLAN.</summary>
    [Fact]
    public void Without_memberships_on_the_device_a_port_groups_by_its_own_vlan()
    {
        // ifVlan 1 on an uplink is all there is to go on, so it is just VLAN 1.
        var groups = ByVlan(false, Member(1, "Gi1/0/1", 1), Member(2, "Gi1/0/2", 30), Member(3, "Gi1/0/3", null));

        Assert.Equal(["VLAN 1", "VLAN 30", "No VLAN"], groups.Select(g => g.Name));
        Assert.Equal("Access/native VLAN", groups[0].Single().PortVlanLabel);
    }

    /// <summary>Once a device reports memberships, a port reporting none is No VLAN, even with an ifVlan.</summary>
    [Fact]
    public void A_port_without_memberships_on_a_device_with_them_has_no_vlan_group()
    {
        var group = Assert.Single(ByVlan(true, Member(1, "Gi1/0/1", 30)));

        Assert.Equal("No VLAN", group.Name);
        Assert.Equal("Not reported", group.Single().PortVlanText);
    }

    /// <summary>Whatever the device reports, every port's group agrees with the VLANs its card lists.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Every_port_sits_under_the_vlans_its_card_lists(bool deviceReportsMemberships)
    {
        var groups = ByVlan(deviceReportsMemberships,
            Member(1, "Gi1/0/1", 1, (10, true), (20, false)),
            Member(2, "Gi1/0/2", 1, (10, false)),
            Member(3, "Gi1/0/3", 30),
            Member(4, "Gi1/0/4", null),
            Member(5, "Gi1/0/5", null, (0, true)));

        foreach (var group in groups)
        {
            foreach (var row in group)
            {
                var listed = row.PortVlanText == "Not reported" ? [] : row.PortVlanText!.Split(", ").Select(int.Parse).ToArray();
                var expected = listed.Length switch
                {
                    > 1 => "Trunk",
                    1 => $"VLAN {listed[0]}",
                    _ => "No VLAN",
                };
                Assert.Equal(expected, group.Name);
            }
        }
    }

    /// <summary>Sort by VLAN uses the group order, so a sorted list and a grouped one agree.</summary>
    [Fact]
    public void Sorting_by_vlan_follows_the_group_order()
    {
        var rows = new[]
        {
            Member(1, "Gi1/0/1", null),
            Member(2, "Gi1/0/2", null, (100, true)),
            Member(3, "Gi1/0/3", null, (10, true), (20, false)),
            Member(4, "Gi1/0/4", null, (2, true)),
        }.Select(p => DeviceSectionLoader.PortRow(p, true));

        var groups = PortArrangement.Arrange(rows, PortSort.Vlan, PortGrouping.None);

        Assert.Equal(["Gi1/0/3", "Gi1/0/4", "Gi1/0/2", "Gi1/0/1"], Names(groups));
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

    [Fact]
    public async Task Sort_and_filter_and_search_are_one_at_a_time_and_the_list_says_why_it_is_short()
    {
        var client = Fakes.Client();
        client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns([Port(2, "Gi0/2", vlan: 10), Port(1, "Gi0/1", vlan: 20)]);
        var vm = new DeviceSectionViewModel(new DeviceSectionLoader(client, Fakes.Settings()), new RecordingNavigation(), new InMemoryPreferences());
        await vm.LoadAsync(7, DeviceSection.Ports);
        Assert.False(vm.HasPanelFilters);
        Assert.False(vm.HasFiltersText);

        vm.IsSearchOpen = true;
        vm.ToggleFilterPanelCommand.Execute(null);
        Assert.True(vm.IsFilterPanelOpen);
        Assert.False(vm.IsSearchOpen);

        vm.SelectedPortSort = vm.PortSortOptions.Single(o => o.Sort == PortSort.Name);
        vm.SelectedPortGrouping = vm.PortGroupingOptions.Single(o => o.Grouping == PortGrouping.Vlan);
        Assert.True(vm.HasPanelFilters);
        Assert.False(vm.HasFiltersText);

        vm.ToggleFilterPanelCommand.Execute(null);
        Assert.True(vm.HasFiltersText);
        Assert.Equal("Sorted by Name \u00b7 Grouped by VLAN", vm.FiltersText);

        vm.IsSearchOpen = true;
        Assert.False(vm.IsFilterPanelOpen);
        vm.SearchText = "Gi0/1";
        vm.DismissSearch();
        Assert.Equal("Matching \u201cGi0/1\u201d \u00b7 Sorted by Name \u00b7 Grouped by VLAN", vm.FiltersText);
    }
}
