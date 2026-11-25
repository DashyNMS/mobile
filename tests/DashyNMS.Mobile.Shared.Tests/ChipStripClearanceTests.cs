using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class ChipStripClearanceTests
{
    [Fact]
    public void Standard_iphone_with_no_row_obstacles_runs_edge_to_edge()
    {
        var clear = ChipStripClearance.Resolve(0, 0);
        Assert.Equal(0, clear.Left);
        Assert.Equal(0, clear.Right);
    }

    [Fact]
    public void Duo_outer_portrait_ends_strip_12pt_before_clock_and_wifi()
    {
        var clear = ChipStripClearance.Resolve(besideLeft: 0, besideRight: 80);
        Assert.Equal(0, clear.Left);
        Assert.Equal(92, clear.Right);
    }

    [Fact]
    public void Duo_outer_landscape_ends_leading_edge_12pt_after_left_pill_or_camera()
    {
        var clear = ChipStripClearance.Resolve(besideLeft: 76, besideRight: 0);
        Assert.Equal(88, clear.Left);
        Assert.Equal(0, clear.Right);
    }

    [Fact]
    public void Split_view_left_pane_ignores_right_hand_pill_outside_its_pane()
    {
        // 951x669 split view: left list pane is 340pt wide, so 611pt sits to its right.
        // Right pill is 80pt wide -> 80 + 12 - 611 = 0, so the left pane is never clipped.
        var clear = ChipStripClearance.Resolve(besideLeft: 0, besideRight: 80, spaceLeftOfPane: 0, spaceRightOfPane: 611);
        Assert.Equal(0, clear.Left);
        Assert.Equal(0, clear.Right);
    }
}
