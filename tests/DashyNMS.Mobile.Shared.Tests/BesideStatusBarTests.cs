using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class BesideStatusBarTests
{
    // A line across a 600-point page, inside the page's 16-point margins.
    private const double Left = 16;
    private const double Right = 584;
    private const double Height = 44;

    [Fact]
    public void A_status_bar_at_one_side_moves_the_line_up_beside_it()
    {
        // iPhone Duo's inner display: the clock and signal at the right only.
        var placement = BesideStatusBar.Place(60, Left, Right, Height, [new OccupiedArea(380, 12, 600, 48)]);

        Assert.NotNull(placement);
        Assert.Equal(8, placement.Value.Top);        // level with the clock: (12 + 48 - 44) / 2
        Assert.Equal(0, placement.Value.InsetStart);
        Assert.Equal(204, placement.Value.InsetEnd); // stops where the status bar starts
    }

    [Fact]
    public void A_camera_and_a_status_bar_leave_the_room_between_them()
    {
        var placement = BesideStatusBar.Place(60, Left, Right, Height,
        [
            new OccupiedArea(450, 12, 600, 48),
            new OccupiedArea(0, 8, 70, 52),
        ]);

        Assert.NotNull(placement);
        Assert.Equal(54, placement.Value.InsetStart);
        Assert.Equal(134, placement.Value.InsetEnd);
    }

    [Fact]
    public void An_ordinary_iphone_keeps_its_line_below_the_status_bar()
    {
        // A 393-point phone: clock, camera and signal leave only slivers.
        var placement = BesideStatusBar.Place(59, 16, 377, Height,
        [
            new OccupiedArea(30, 14, 110, 40),
            new OccupiedArea(130, 11, 255, 48),
            new OccupiedArea(280, 14, 370, 40),
        ]);

        Assert.Null(placement);
    }

    [Fact]
    public void Nothing_reported_keeps_the_line_where_it_is() =>
        Assert.Null(BesideStatusBar.Place(60, Left, Right, Height, []));

    [Fact]
    public void No_strip_at_the_top_keeps_the_line_where_it_is() =>
        Assert.Null(BesideStatusBar.Place(0, Left, Right, Height, [new OccupiedArea(380, 0, 600, 30)]));

    [Fact]
    public void Areas_below_the_strip_are_not_in_the_way()
    {
        // The home indicator, at the foot of the page.
        Assert.Null(BesideStatusBar.Place(60, Left, Right, Height, [new OccupiedArea(200, 800, 400, 830)]));
    }

    [Fact]
    public void A_status_bar_taller_than_the_line_sets_the_line_level_with_its_foot()
    {
        var placement = BesideStatusBar.Place(80, Left, Right, Height, [new OccupiedArea(380, 0, 600, 70)]);

        Assert.NotNull(placement);
        Assert.Equal(26, placement.Value.Top); // 70 - 44: what follows the line stays clear of it
    }

    [Fact]
    public void A_line_already_as_high_as_it_can_go_stays_put() =>
        Assert.Null(BesideStatusBar.Place(40, Left, Right, Height, [new OccupiedArea(380, 0, 600, 90)]));
}
