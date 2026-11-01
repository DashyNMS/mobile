using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class GlassButtonPlacementTests
{
    private static readonly OccupiedArea Safe = new(0, 24, 900, 600);

    [Fact]
    public void Buttons_inside_screen_but_under_an_activity_are_not_clear()
    {
        OccupiedArea[] regions = [new(800, 24, 900, 180)];
        Assert.False(GlassButtonPlacement.IsClear(new(820, 100, 880, 220), Safe, regions));
    }

    [Fact]
    public void Compact_group_moves_clear_of_activity_and_tab_bar()
    {
        OccupiedArea[] regions = [new(800, 24, 900, 180), new(820, 350, 880, 580)];
        var found = GlassButtonPlacement.Find(new(820, 150, 880, 270), Safe, regions);
        Assert.NotNull(found);
        Assert.True(GlassButtonPlacement.IsClear(found.Value, Safe, regions, 12));
        Assert.InRange(found.Value.Top, 192, 218);
    }

    [Fact]
    public void Full_column_moves_to_a_clear_neighbouring_position()
    {
        OccupiedArea[] regions = [new(800, 24, 900, 580)];
        var found = GlassButtonPlacement.Find(new(820, 100, 880, 220), Safe, regions);
        Assert.NotNull(found);
        Assert.True(found.Value.Right <= 788);
        Assert.True(GlassButtonPlacement.IsClear(found.Value, Safe, regions, 12));
    }

    [Fact]
    public void Hysteresis_requires_extra_clearance_before_expanding()
    {
        OccupiedArea[] regions = [new(800, 24, 900, 180)];
        var buttons = new OccupiedArea(820, 184, 880, 244);
        Assert.True(GlassButtonPlacement.IsClear(buttons, Safe, regions));
        Assert.False(GlassButtonPlacement.IsClear(buttons, Safe, regions, 8));
        Assert.True(GlassButtonPlacement.IsClear(buttons, Safe, [] , 8));
    }

    [Fact]
    public void Impossible_size_has_no_placement_and_empty_groups_are_allowed()
    {
        Assert.Null(GlassButtonPlacement.Find(new(0, 0, 1000, 700), Safe, []));
        Assert.True(GlassButtonPlacement.IsClear(new(0, 0, 0, 0), Safe, []));
    }
}
