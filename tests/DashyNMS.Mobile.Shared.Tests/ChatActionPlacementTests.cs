using DashyNMS.Mobile.Intelligence;

namespace DashyNMS.Mobile.Tests;

public sealed class ChatActionPlacementTests
{
    [Theory]
    [InlineData(820, 1180)] // iPad Air in portrait
    [InlineData(744, 1133)] // iPad mini in portrait
    [InlineData(680, 760)]  // a big, nearly square inner display in portrait
    public void A_big_portrait_window_puts_the_actions_at_the_top(double width, double height) =>
        Assert.Equal(ChatActionPlace.HeaderGlass, ChatActionPlacement.Resolve(true, width, height));

    [Theory]
    [InlineData(393, 852)]   // a phone in portrait
    [InlineData(375, 700)]   // a phone-sized outer display
    [InlineData(320, 1180)]  // an iPad app in narrow Split View
    [InlineData(852, 393)]   // a phone in landscape
    [InlineData(1180, 820)]  // an iPad in landscape
    [InlineData(760, 680)]   // a big inner display in landscape
    [InlineData(700, 700)]   // square is not portrait
    public void Phone_sized_and_landscape_windows_keep_the_tab_bar_glass(double width, double height) =>
        Assert.Equal(ChatActionPlace.TabBarGlass, ChatActionPlacement.Resolve(true, width, height));

    [Fact]
    public void Without_glass_the_actions_stay_as_header_icons() =>
        Assert.Equal(ChatActionPlace.HeaderIcons, ChatActionPlacement.Resolve(false, 820, 1180));

    [Theory]
    [InlineData(-1, -1)] // MAUI's size before the first layout
    [InlineData(0, 0)]
    [InlineData(double.NaN, 1180)]
    public void An_unmeasured_page_keeps_the_tab_bar_glass(double width, double height) =>
        Assert.Equal(ChatActionPlace.TabBarGlass, ChatActionPlacement.Resolve(true, width, height));
}
