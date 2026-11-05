using DashyNMS.Mobile.DeviceSections;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class PortPresentationTests
{
    [Theory]
    [InlineData("up", "up", false, RowStatus.Ok, "Up")]
    [InlineData("up", "down", true, RowStatus.Ok, "Up")]
    [InlineData("down", "up", false, RowStatus.Warning, "Down")]
    [InlineData("down", "down", false, RowStatus.Inactive, "Disabled")]
    [InlineData("down", "up", true, RowStatus.Inactive, "Disabled")]
    [InlineData(null, null, false, RowStatus.None, "Unknown")]
    public void Display_palette_preserves_source_classification(string? oper, string? admin, bool disabled, RowStatus visual, string label)
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfOperStatus = oper, IfAdminStatus = admin,
            Disabled = disabled, IfInOctetsRate = 0, IfOutOctetsRate = 0, IfInErrorsRate = 0, IfOutErrorsRate = 0 });
        Assert.Equal(visual, row.PortVisualStatus);
        Assert.Equal(label, row.PortVisualLabel);
        Assert.StartsWith(row.Title + ", " + label + ", ", row.PortFactsDescription);
        if (oper == "up") Assert.Equal("Up", row.Port!.StatusName);
        if (label == "Disabled") Assert.Equal("Shut down", row.Port!.StatusName);
    }

    [Fact]
    public void Errors_override_up_with_partial_missing_readings_and_actual_graph_identity()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfDescr = "Synthetic interface", IfName = "Gi0/7",
            IfOperStatus = "up", IfInErrorsRate = 2, IfOutErrorsRate = null, IfInOctetsRate = 10 });
        Assert.Equal(RowStatus.Critical, row.PortVisualStatus);
        Assert.Equal(RowStatus.Warning, row.Status);
        Assert.Equal("Up · errors", row.PortVisualLabel);
        Assert.Equal("Gi0/7", row.LinkPortIfName);
        Assert.Equal("80 b/s", row.PortTrafficIn);
        Assert.Equal("Unavailable", row.PortTrafficOut);
        Assert.Equal("2/s", row.PortErrorsIn);
        Assert.True(row.PortErrorsInPositive);
        Assert.Equal("Unavailable", row.PortErrorsOut);
        Assert.False(row.PortErrorsOutPositive);
        Assert.False(row.Matches("Administrative state"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.MaxValue)]
    public void Incomplete_or_overflowing_traffic_never_claims_green(double? rate)
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfOperStatus = "up", IfInOctetsRate = rate,
            IfOutOctetsRate = 0, IfInErrorsRate = 0, IfOutErrorsRate = 0 });
        Assert.Equal(RowStatus.None, row.PortVisualStatus);
        Assert.Equal("Unavailable", row.PortTrafficIn);
        Assert.Equal("0/s", row.PortErrorsIn);
        Assert.False(row.PortErrorsInPositive);
        Assert.Contains("ifName not reported", row.PortActionDescription);
    }

    [Fact]
    public void Small_positive_errors_do_not_round_to_a_reported_zero()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfOperStatus = "up", IfInErrorsRate = 0.001 });
        Assert.Equal(RowStatus.Critical, row.PortVisualStatus);
        Assert.NotEqual("0/s", row.PortErrorsIn);
        Assert.True(row.PortErrorsInPositive);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(361, 1)]
    [InlineData(687, 1)]
    [InlineData(688, 2)]
    [InlineData(1400, 2)]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    public void Ports_show_one_card_per_row_and_at_most_two_when_both_are_comfortable(double width, int count) =>
        Assert.Equal(count, PortColumns.Count(width, 8));

    [Fact]
    public void Compact_card_speaks_only_the_visible_facts_in_order()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/1", IfAlias = "Synthetic uplink", IfOperStatus = "up",
            IfAdminStatus = "up", IfSpeed = 1_000_000_000, IfInOctetsRate = 15_500_000, IfOutOctetsRate = 4_750_000,
            IfInErrorsRate = 0, IfOutErrorsRate = 0,
            Vlans = [new() { Vlan = 30 }, new() { Vlan = 10, Untagged = true }, new() { Vlan = 20 }, new() { Vlan = 10 }] }, true);
        Assert.Equal("Gi0/1, Synthetic uplink, Up, 1 Gb/s, Traffic in: 124 Mb/s, Traffic out: 38 Mb/s, "
            + "Errors in: 0/s, Errors out: 0/s, VLANs: 10, 20, 30, Open interface graphs", row.PortActionDescription);
        Assert.DoesNotContain("Administrative", row.PortActionDescription);
        Assert.DoesNotContain("Monitoring", row.PortActionDescription);
    }

    [Fact]
    public void Missing_speed_and_non_up_traffic_stay_explicit_rather_than_zero()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/2", IfOperStatus = "down", IfInOctetsRate = 0, IfOutOctetsRate = 0 });
        Assert.Equal("Speed unavailable", row.PortSpeedText);
        Assert.Equal("Not shown", row.PortTrafficIn);
        Assert.Equal("Unavailable", row.PortErrorsIn);
        Assert.Equal(RowStatus.Warning, row.PortVisualStatus);
    }

    [Fact]
    public void Direct_vlan_alone_is_labelled_for_what_it_is()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/3", IfOperStatus = "up", IfVlan = 10 });
        Assert.Equal("Access/native VLAN", row.PortVlanLabel);
        Assert.Equal("10", row.PortVlanText);
        Assert.Equal(10, row.Port!.Vlan);
    }

    [Fact]
    public void Device_memberships_replace_the_direct_vlan_without_inventing_tags()
    {
        // A trunk can report ifVlan 1; with memberships on the device, a port
        // reporting none says so rather than claiming that VLAN.
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/4", IfOperStatus = "up", IfVlan = 1 }, true);
        Assert.Equal("VLANs", row.PortVlanLabel);
        Assert.Equal("Not reported", row.PortVlanText);
        var none = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/5", IfOperStatus = "up", Vlans = [new() { Vlan = 0 }] });
        Assert.Equal("Not reported", none.PortVlanText);
    }

    [Fact]
    public void Overflowing_error_sum_still_shows_red_for_positive_source_rates()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfOperStatus = "up",
            IfInErrorsRate = double.MaxValue, IfOutErrorsRate = double.MaxValue,
            IfInOctetsRate = 0, IfOutOctetsRate = 0 });
        Assert.Equal(RowStatus.Critical, row.PortVisualStatus);
        Assert.Equal("Up · errors", row.PortVisualLabel);
        Assert.True(row.PortErrorsInPositive && row.PortErrorsOutPositive);
    }
}
