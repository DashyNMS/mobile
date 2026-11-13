using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class ChatComposerPlacementTests
{
    [Fact]
    public void A_landscape_bottom_bar_puts_the_composer_beside_it_level_with_its_base()
    {
        // A phone in landscape: pill on the left of the bottom row, glass at its right end.
        var lane = ChatComposerPlacement.Resolve(852, 393, 21, 75, 777,
            new(75, 330, 255, 380), [new(267, 330, 367, 380)]);
        Assert.Equal(13, lane.Gap);
        Assert.Equal(75, lane.Clearance);
        Assert.Equal(379, lane.LaneLeft);
        Assert.Equal(777, lane.LaneRight);
    }

    [Fact]
    public void The_wider_free_side_is_used()
    {
        var lane = ChatComposerPlacement.Resolve(1000, 400, 0, 16, 984, new(600, 340, 900, 390), []);
        Assert.Equal(16, lane.LaneLeft);
        Assert.Equal(588, lane.LaneRight);
    }

    [Fact]
    public void A_phone_in_portrait_keeps_the_composer_above_the_bar()
    {
        var lane = ChatComposerPlacement.Resolve(393, 852, 34, 16, 377,
            new(16, 780, 300, 830), [new(312, 780, 377, 830)]);
        Assert.Null(lane.LaneLeft);
        Assert.Equal(SetupActionPlacement.BottomGap(852, 34, new(16, 780, 300, 830), [new(312, 780, 377, 830)]), lane.Gap);
        Assert.Equal(lane.Gap, lane.Clearance);
    }

    [Fact]
    public void A_side_pill_aligns_the_base_through_the_shared_rule()
    {
        var lane = ChatComposerPlacement.Resolve(1180, 800, 20, 100, 1164, new(0, 300, 60, 720), []);
        Assert.Null(lane.LaneLeft);
        Assert.Equal(80, lane.Gap);
    }

    [Fact]
    public void A_docked_keyboard_takes_the_whole_column_above_it()
    {
        var lane = ChatComposerPlacement.Resolve(852, 393, 21, 75, 777, new(75, 330, 375, 380), [], keyboardTop: 200);
        Assert.Null(lane.LaneLeft);
        Assert.Equal(205, lane.Gap);
    }

    [Fact]
    public void Without_a_measured_pill_the_composer_clears_the_native_inset() =>
        Assert.Equal(new ChatComposerLane(46, 46, null, null), ChatComposerPlacement.Resolve(393, 852, 34, 16, 377, null, []));
}
