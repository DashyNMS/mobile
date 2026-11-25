using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class PageActionPlacementTests
{
    [Theory]
    [InlineData(669, 951)]  // Duo inner portrait
    [InlineData(820, 1180)] // iPad Air in portrait
    [InlineData(744, 1133)] // iPad mini in portrait
    [InlineData(680, 760)]  // regular nearly-square portrait
    public void Regular_portrait_windows_place_actions_beside_the_title(double width, double height) =>
        Assert.Equal(PageActionPlace.HeaderGlass, PageActionPlacement.Resolve(true, width, height));

    [Theory]
    [InlineData(402, 874)]  // iPhone 18 Pro portrait
    [InlineData(440, 956)]  // iPhone 18 Pro Max portrait
    [InlineData(874, 402)]  // iPhone 18 Pro landscape
    [InlineData(956, 440)]  // iPhone 18 Pro Max landscape
    [InlineData(466, 678)]  // Duo outer portrait
    [InlineData(678, 466)]  // Duo outer landscape
    [InlineData(951, 669)]  // Duo inner landscape
    [InlineData(320, 1180)] // narrow Split View
    [InlineData(700, 700)]  // square is not portrait
    public void Compact_and_landscape_windows_keep_actions_in_tab_bar_glass(double width, double height) =>
        Assert.Equal(PageActionPlace.TabBarGlass, PageActionPlacement.Resolve(true, width, height));

    [Fact]
    public void Without_glass_actions_stay_as_header_icons() =>
        Assert.Equal(PageActionPlace.HeaderIcons, PageActionPlacement.Resolve(false, 669, 951));

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(0, 0)]
    [InlineData(double.NaN, 951)]
    public void Unmeasured_windows_keep_tab_bar_glass(double width, double height) =>
        Assert.Equal(PageActionPlace.TabBarGlass, PageActionPlacement.Resolve(true, width, height));

    [Fact]
    public void Tight_title_lane_falls_back_to_tab_bar_glass()
    {
        // Safe lane 320pt with 3 glass buttons (132pt) + 12pt gap leaves 176pt < 180pt minimum for a long title.
        Assert.Equal(PageActionPlace.TabBarGlass,
            PageActionPlacement.Resolve(true, 669, 951, headerLaneWidth: 320, titleWidth: 260, glassWidth: 132));

        // Same lane with a short title ("Chats", 90pt) still fits beside glass.
        Assert.Equal(PageActionPlace.HeaderGlass,
            PageActionPlacement.Resolve(true, 669, 951, headerLaneWidth: 320, titleWidth: 90, glassWidth: 132));

        // Roomy Duo inner portrait lane (637pt) fits long title + 3 glass buttons cleanly.
        Assert.Equal(PageActionPlace.HeaderGlass,
            PageActionPlacement.Resolve(true, 669, 951, headerLaneWidth: 637, titleWidth: 260, glassWidth: 132));
    }
}
