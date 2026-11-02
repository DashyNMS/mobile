using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class SetupActionPlacementTests
{
    [Theory]
    [InlineData(0, 12)]
    [InlineData(34, 46)]
    public void Missing_pill_keeps_the_action_above_native_bottom_clearance(double inset, double expected) =>
        Assert.Equal(expected, SetupActionPlacement.BottomGap(800, inset, null, []));

    [Theory]
    [InlineData(0, 60)]
    [InlineData(500, 560)]
    public void Side_pill_aligns_the_action_bottom_on_either_side(double left, double right) =>
        Assert.Equal(80, SetupActionPlacement.BottomGap(800, 34,
            new(left, 300, right, 720), [new(left, 200, right, 280)]));

    [Fact]
    public void Bottom_bar_and_its_glass_actions_both_clear_the_action() =>
        Assert.Equal(182, SetupActionPlacement.BottomGap(800, 34,
            new(100, 720, 500, 780), [new(100, 630, 500, 690)]));

    [Fact]
    public void Top_bar_does_not_lift_the_action_from_the_page_base() =>
        Assert.Equal(46, SetupActionPlacement.BottomGap(800, 34,
            new(100, 12, 500, 72), [new(20, 12, 80, 72)]));

    [Fact]
    public void Keyboard_overrides_side_pill_and_dismissal_restores_alignment()
    {
        OccupiedArea pill = new(500, 300, 560, 720);
        Assert.Equal(312, SetupActionPlacement.BottomGap(800, 34, pill, [], 500));
        Assert.Equal(80, SetupActionPlacement.BottomGap(800, 34, pill, []));
    }

    [Fact]
    public void Keyboard_does_not_lower_an_action_already_above_it() =>
        Assert.Equal(182, SetupActionPlacement.BottomGap(800, 34,
            new(100, 720, 500, 780), [new(100, 630, 500, 690)], 750));

    [Theory]
    [InlineData(800, 720, 80)]
    [InlineData(500, 470, 34)]
    public void Resize_recomputes_alignment_and_preserves_home_indicator(double height, double bottom, double expected) =>
        Assert.Equal(expected, SetupActionPlacement.BottomGap(height, 34,
            new(500, 200, 560, bottom), []));

    [Fact]
    public void Zero_height_during_handler_replacement_never_produces_negative_padding() =>
        Assert.Equal(0, SetupActionPlacement.BottomGap(0, 34, null, [], -50));
}
