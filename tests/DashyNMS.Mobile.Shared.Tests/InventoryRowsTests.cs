using DashyNMS.Mobile.DeviceSections;

namespace DashyNMS.Mobile.Tests;

public sealed class InventoryRowsTests
{
    private static readonly SectionRow[] Rows =
    [
        new("Chassis") { InventoryIndex = 1 },
        new("Module") { InventoryIndex = 2, Depth = 1 },
        new("Optic") { InventoryIndex = 3, Depth = 2, Serial = "TEST-001", Detail = "Serial TEST-001" },
        new("Port") { InventoryIndex = 4, Depth = 1, IsInventoryPort = true },
        new("Other chassis") { InventoryIndex = 5 },
    ];

    [Fact]
    public void Collapse_hides_descendants_but_keeps_other_roots()
    {
        var visible = InventoryRows.Visible(Rows, new HashSet<long> { 1 }, false, "");
        Assert.Equal(new[] { "Chassis", "Other chassis" }, visible.Select(r => r.Title));
        Assert.True(visible[0].CanExpand);
        Assert.False(visible[0].IsExpanded);
    }

    [Fact]
    public void Search_reveals_matching_descendant_and_ancestors_without_changing_collapsed_state()
    {
        var collapsed = new HashSet<long> { 1, 2 };
        var visible = InventoryRows.Visible(Rows, collapsed, false, "TEST-001");
        Assert.Equal(new[] { "Chassis", "Module", "Optic" }, visible.Select(r => r.Title));
        Assert.True(visible[0].IsExpanded);
        Assert.Equal(2, InventoryRows.Visible(Rows, collapsed, false, "").Count);
    }

    [Fact]
    public void Ports_are_hidden_until_requested_and_search_respects_the_toggle()
    {
        Assert.DoesNotContain(InventoryRows.Visible(Rows, new HashSet<long>(), false, ""), r => r.IsInventoryPort);
        Assert.Contains(InventoryRows.Visible(Rows, new HashSet<long>(), true, ""), r => r.IsInventoryPort);
        Assert.Empty(InventoryRows.Visible(Rows, new HashSet<long>(), false, "Port"));
    }

    [Fact]
    public void Parent_with_only_hidden_ports_has_no_expansion_control()
    {
        SectionRow[] rows = [Rows[0], Rows[3]];
        Assert.False(InventoryRows.Visible(rows, new HashSet<long>(), false, "")[0].CanExpand);
        Assert.True(InventoryRows.Visible(rows, new HashSet<long>(), true, "")[0].CanExpand);
    }
}
