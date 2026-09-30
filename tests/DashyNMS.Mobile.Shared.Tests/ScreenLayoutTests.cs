using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class ScreenLayoutTests
{
    [Theory]
    [InlineData(390, WidthClass.Compact)]   // iPhone in portrait
    [InlineData(320, WidthClass.Compact)]   // iPad Slide Over, or a third of the screen
    [InlineData(744, WidthClass.Regular)]   // iPad mini in portrait
    [InlineData(820, WidthClass.Regular)]   // iPad Air in portrait
    [InlineData(956, WidthClass.Regular)]   // iPhone Pro Max in landscape
    [InlineData(1180, WidthClass.Wide)]     // iPad Air in landscape
    [InlineData(1440, WidthClass.Wide)]     // a Mac window
    public void Widths_fall_into_their_class(double width, WidthClass expected) =>
        Assert.Equal(expected, ScreenLayout.Classify(width));

    [Theory]
    [InlineData(390, false)]
    [InlineData(507, false)] // iPad half of Split View
    [InlineData(744, true)]
    [InlineData(1180, true)]
    public void Lists_show_their_detail_beside_them_once_there_is_room(double width, bool splits) =>
        Assert.Equal(splits, ScreenLayout.SplitsListAndDetail(width));

    [Fact]
    public void The_detail_pane_always_gets_at_least_as_much_room_as_the_list()
    {
        foreach (var width in new[] { ScreenLayout.SplitFrom, ScreenLayout.WideFrom, 1366d })
        {
            var list = ScreenLayout.ListPaneWidth(width);
            Assert.True(width - list >= list, $"{width}: list {list}");
        }
    }
}
