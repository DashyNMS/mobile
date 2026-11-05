using DashyNMS.Mobile.DeviceSections;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class PortPresentationTests
{
    [Theory]
    [InlineData("up", "up", false, false, RowStatus.Ok, "Up", "Up")]
    [InlineData("down", "up", false, false, RowStatus.Warning, "Down", "Down")]
    [InlineData("down", "down", false, false, RowStatus.Inactive, "Disabled", "Shut down")]
    [InlineData("up", "down", false, false, RowStatus.Inactive, "Disabled", "Shut down")]
    [InlineData("down", "up", true, false, RowStatus.Inactive, "Not monitored", "Not monitored")]
    [InlineData("up", "up", true, false, RowStatus.Inactive, "Not monitored", "Not monitored")]
    [InlineData("up", "up", false, true, RowStatus.Inactive, "Not monitored", "Not monitored")]
    [InlineData("down", "up", false, true, RowStatus.Inactive, "Not monitored", "Not monitored")]
    [InlineData(null, null, false, false, RowStatus.Inactive, "Unknown", "Unknown")]
    [InlineData("testing", "up", false, false, RowStatus.Inactive, "Unknown", "Unknown")]
    public void Every_port_gets_its_colour_class_and_a_spoken_label(string? oper, string? admin, bool disabled, bool ignore,
        RowStatus visual, string label, string statusName)
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfOperStatus = oper, IfAdminStatus = admin, Disabled = disabled, Ignore = ignore,
            IfInOctetsRate = 0, IfOutOctetsRate = 0, IfInErrorsDelta = 0, IfOutErrorsDelta = 0 });
        Assert.Equal(visual, row.PortVisualStatus);
        Assert.Equal(label, row.PortVisualLabel);
        Assert.Equal(statusName, row.Port!.StatusName);
        // Colour is never the only signal: the label is spoken first after the name.
        Assert.StartsWith(row.Title + ", " + label + ", ", row.PortFactsDescription);
    }

    [Fact]
    public void Shut_down_and_not_monitored_stay_distinct_though_both_grey()
    {
        var shut = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/20", IfOperStatus = "down", IfAdminStatus = "down" });
        var unmonitored = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/21", IfOperStatus = "down", IfAdminStatus = "up", Disabled = true });
        var ignored = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/22", IfOperStatus = "down", IfAdminStatus = "up", Ignore = true });
        Assert.All(new[] { shut, unmonitored, ignored }, r => Assert.Equal(RowStatus.Inactive, r.PortVisualStatus));
        Assert.Equal("Disabled", shut.PortVisualLabel);
        Assert.Equal("Not monitored", unmonitored.PortVisualLabel);
        Assert.Equal("Not monitored", ignored.PortVisualLabel);
        Assert.NotEqual(shut.Port!.StatusRank, unmonitored.Port!.StatusRank);
    }

    [Fact]
    public void Live_error_deltas_mark_a_port_errored_when_core_carries_no_rates()
    {
        // Core's device-ports request carries ifInErrors_delta/ifOutErrors_delta, not the rates.
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/8", IfOperStatus = "up", IfAdminStatus = "up",
            IfInOctetsRate = 100, IfOutOctetsRate = 100, IfInErrorsDelta = 3, IfOutErrorsDelta = 0 });
        Assert.Equal(RowStatus.Critical, row.PortVisualStatus);
        Assert.Equal("Up · errors", row.PortVisualLabel);
        Assert.Equal("3 in last poll", row.PortErrorsIn);
        Assert.Equal("0 in last poll", row.PortErrorsOut);
        Assert.True(row.PortErrorsInPositive);
        Assert.False(row.PortErrorsOutPositive);
        Assert.Equal("Errors", row.Port!.StatusName);
        Assert.Equal(0, row.Port.StatusRank);
        Assert.Contains("3 errors in last poll", row.Detail);
    }

    [Fact]
    public void Zero_error_deltas_without_rates_are_green()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/9", IfOperStatus = "up", IfAdminStatus = "up",
            IfInOctetsRate = 100, IfOutOctetsRate = 100, IfInErrorsDelta = 0, IfOutErrorsDelta = 0 });
        Assert.Equal(RowStatus.Ok, row.PortVisualStatus);
        Assert.Equal("Up", row.PortVisualLabel);
        Assert.Equal(RowStatus.Ok, row.Status);
    }

    [Fact]
    public void An_enabled_down_port_with_errors_is_red_but_still_counted_down()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/10", IfOperStatus = "down", IfAdminStatus = "up", IfOutErrorsDelta = 12 });
        Assert.Equal(RowStatus.Critical, row.PortVisualStatus);
        Assert.Equal("Down · errors", row.PortVisualLabel);
        Assert.Equal("Down", row.Value);
        Assert.Equal(RowStatus.Critical, row.Status);
    }

    [Fact]
    public void Shut_down_or_not_monitored_ports_with_errors_stay_grey()
    {
        var shut = DeviceSectionLoader.PortRow(new Port { IfOperStatus = "down", IfAdminStatus = "down", IfInErrorsDelta = 4 });
        var ignored = DeviceSectionLoader.PortRow(new Port { IfOperStatus = "up", IfAdminStatus = "up", Ignore = true, IfInErrorsRate = 4 });
        Assert.Equal(RowStatus.Inactive, shut.PortVisualStatus);
        Assert.Equal(RowStatus.Inactive, ignored.PortVisualStatus);
        Assert.Equal("Not monitored", ignored.PortVisualLabel);
    }

    [Fact]
    public void Up_without_any_error_reading_is_grey_and_says_why()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/11", IfOperStatus = "up", IfAdminStatus = "up", IfInOctetsRate = 1, IfOutOctetsRate = 1 });
        Assert.Equal(RowStatus.Inactive, row.PortVisualStatus);
        Assert.Equal("Up · errors unavailable", row.PortVisualLabel);
        Assert.Equal("Unavailable", row.PortErrorsIn);
        Assert.Equal("Up", row.Port!.StatusName);
    }

    [Fact]
    public void A_valid_rate_is_preferred_and_an_invalid_one_falls_back_to_the_delta()
    {
        Assert.Equal((double?)0, PortStatus.ErrorReading(0, 5));
        Assert.Equal((double?)5, PortStatus.ErrorReading(-1, 5));
        Assert.Equal((double?)5, PortStatus.ErrorReading(double.NaN, 5));
        Assert.Null(PortStatus.ErrorReading(null, -1));
        Assert.Null(PortStatus.ErrorReading(null, null));
    }

    [Fact]
    public void Status_sort_and_grouping_lead_with_errored_then_down()
    {
        SectionRow Row(int index, string oper, string admin = "up", bool disabled = false, long errors = 0) =>
            DeviceSectionLoader.PortRow(new Port { IfIndex = index, IfName = $"Gi0/{index}", IfOperStatus = oper, IfAdminStatus = admin,
                Disabled = disabled, IfInErrorsDelta = errors, IfOutErrorsDelta = 0 });
        SectionRow[] rows =
        [
            Row(1, "testing"), Row(2, "up", disabled: true), Row(3, "down", "down"),
            Row(4, "up"), Row(5, "down"), Row(6, "up", errors: 2),
        ];
        Assert.Equal(["Errors", "Down", "Up", "Shut down", "Not monitored", "Unknown"],
            PortArrangement.Arrange(rows, PortSort.PortId, PortGrouping.Status).Select(g => g.Name));
        Assert.Equal(["Gi0/6", "Gi0/5", "Gi0/4", "Gi0/3", "Gi0/2", "Gi0/1"],
            PortArrangement.Arrange(rows, PortSort.Status, PortGrouping.None).SelectMany(g => g).Select(r => r.Title));
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
    public void Incomplete_traffic_stays_unavailable_without_hiding_the_up_state(double? rate)
    {
        // Traffic is a reading, not a status: the port is still up and error-free.
        var row = DeviceSectionLoader.PortRow(new Port { IfOperStatus = "up", IfInOctetsRate = rate,
            IfOutOctetsRate = 0, IfInErrorsRate = 0, IfOutErrorsRate = 0 });
        Assert.Equal(RowStatus.Ok, row.PortVisualStatus);
        Assert.Equal("Up", row.PortVisualLabel);
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
