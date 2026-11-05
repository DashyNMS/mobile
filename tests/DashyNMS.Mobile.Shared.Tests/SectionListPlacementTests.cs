using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class SectionListPlacementTests
{
    [Theory]
    [InlineData(20)]  // side pill: H - pill bottom
    [InlineData(182)] // bottom bar and its glass actions
    [InlineData(46)]  // missing or top-wide pill: 12 + safe bottom
    public void Viewport_reaches_the_host_base_whatever_the_obstruction(double obstruction)
    {
        var placement = SectionListPlacement.Resolve(obstruction);

        Assert.Equal(0, placement.Viewport);
        Assert.Equal(obstruction, placement.Overlay);
    }

    [Fact]
    public void Footer_lifts_the_final_row_clear_of_the_obstruction_it_now_scrolls_beneath() =>
        Assert.Equal(SectionListPlacement.FooterGap + 182, SectionListPlacement.Resolve(182).Footer);

    [Fact]
    public void Docked_keyboard_stops_the_viewport_and_footer_adds_no_hidden_glass()
    {
        var placement = SectionListPlacement.Resolve(20, keyboardGap: 312);

        Assert.Equal(312, placement.Viewport);
        Assert.Equal(312, placement.Overlay);
        Assert.Equal(SectionListPlacement.FooterGap, placement.Footer);
    }

    [Fact]
    public void Keyboard_lower_than_the_obstruction_keeps_obstruction_clearance_in_the_footer()
    {
        var placement = SectionListPlacement.Resolve(182, keyboardGap: 100);

        Assert.Equal(100, placement.Viewport);
        Assert.Equal(182, placement.Overlay);
        Assert.Equal(SectionListPlacement.FooterGap + 82, placement.Footer);
    }

    [Fact]
    public void Visible_panel_reserves_its_measured_height_above_the_obstruction() =>
        Assert.Equal(300 + 20 + SectionListPlacement.PanelGap,
            SectionListPlacement.Resolve(20, panelHeight: 300).Footer);

    [Fact]
    public void Panel_shorter_than_the_normal_footer_leaves_the_footer_unchanged() =>
        Assert.Equal(SectionListPlacement.FooterGap + 182,
            SectionListPlacement.Resolve(182, panelHeight: 1).Footer);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Unmeasured_panel_reserves_nothing(double height) =>
        Assert.Equal(SectionListPlacement.FooterGap + 20,
            SectionListPlacement.Resolve(20, panelHeight: height).Footer);

    [Fact]
    public void Negative_measurements_never_shrink_the_viewport_or_footer()
    {
        var placement = SectionListPlacement.Resolve(-10, keyboardGap: -4);

        Assert.Equal(0, placement.Viewport);
        Assert.Equal(0, placement.Overlay);
        Assert.Equal(SectionListPlacement.FooterGap, placement.Footer);
    }
}
