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
    public void Tab_column_controls_are_allowed_inside_the_side_content_inset()
    {
        var screen = new OccupiedArea(0, 24, 900, 600);
        var content = new OccupiedArea(0, 24, 800, 580);
        var actions = new OccupiedArea(820, 40, 880, 160);
        OccupiedArea[] obstacles = [new(820, 350, 880, 580)];
        Assert.False(GlassButtonPlacement.IsClear(actions, content, obstacles));
        Assert.True(GlassButtonPlacement.IsClear(actions, screen, obstacles));
    }

    [Fact]
    public void Overflow_cannot_relocate_into_the_middle_of_the_screen()
    {
        var bar = new OccupiedArea(820, 350, 880, 580);
        OccupiedArea[] obstacles = [new(800, 24, 900, 345), bar];
        Assert.Null(GlassButtonPlacement.FindByBar(new(820, 150, 880, 270), Safe, bar, obstacles));
    }

    [Fact]
    public void Overflow_moves_below_activity_within_the_tab_column()
    {
        var bar = new OccupiedArea(820, 350, 880, 580);
        OccupiedArea[] obstacles = [new(800, 24, 900, 180), bar];
        var found = GlassButtonPlacement.FindByBar(new(820, 150, 880, 270), Safe, bar, obstacles);
        Assert.NotNull(found);
        Assert.Equal(820, found.Value.Left);
        Assert.Equal(880, found.Value.Right);
        Assert.True(found.Value.Top >= 180);
        Assert.True(found.Value.Bottom <= 338);
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

    [Fact]
    public void ResolveLane_returns_expanded_when_full_group_and_search_fit()
    {
        var bar = new OccupiedArea(12, 260, 76, 440);
        var search = new OccupiedArea(12, 184, 76, 248);
        var actions = new OccupiedArea(12, 36, 76, 164);
        var safe = new OccupiedArea(0, 24, 678, 466);

        var result = GlassButtonPlacement.ResolveLane(search, actions, hasBack: true, hasOverflow: true, safe, bar, [bar]);

        Assert.Equal(GlassLaneMode.Expanded, result.Mode);
        Assert.Equal(actions, result.ActionsFrame);
    }

    [Fact]
    public void ResolveLane_returns_two_targets_when_two_44pt_buttons_fit_in_short_vertical_lane()
    {
        // Duo outer landscape (678x466): left side pill leaving 100pt above it (enough for two 44pt targets = 88pt, not 3x64pt).
        var safe = new OccupiedArea(0, 24, 678, 466);
        var bar = new OccupiedArea(12, 136, 76, 440);
        var wantedSearch = new OccupiedArea(12, 60, 76, 124);
        var wantedActions = new OccupiedArea(12, -80, 76, 48);

        var result = GlassButtonPlacement.ResolveLane(wantedSearch, wantedActions, hasBack: true, hasOverflow: true, safe, bar, [bar]);

        Assert.Equal(GlassLaneMode.TwoTargets, result.Mode);
        Assert.NotNull(result.ActionsFrame);
        Assert.Equal(44, result.ActionsFrame.Value.Right - result.ActionsFrame.Value.Left);
        Assert.Equal(88, result.ActionsFrame.Value.Bottom - result.ActionsFrame.Value.Top);
    }

    [Fact]
    public void ResolveLane_returns_one_target_when_only_one_44pt_button_fits()
    {
        // Only 56pt free between safe.Top (24) and bar.Top - 12 (80): fits one 44pt target, not two.
        var safe = new OccupiedArea(0, 24, 678, 466);
        var bar = new OccupiedArea(12, 92, 76, 440);
        var wantedSearch = new OccupiedArea(12, 16, 76, 80);
        var wantedActions = new OccupiedArea(12, -120, 76, 4);

        var result = GlassButtonPlacement.ResolveLane(wantedSearch, wantedActions, hasBack: true, hasOverflow: true, safe, bar, [bar]);

        Assert.Equal(GlassLaneMode.OneTarget, result.Mode);
        Assert.NotNull(result.ActionsFrame);
        Assert.Equal(44, result.ActionsFrame.Value.Bottom - result.ActionsFrame.Value.Top);
    }

    [Fact]
    public void ResolveLane_returns_zero_targets_when_no_44pt_button_fits()
    {
        // Only 30pt free above bar: even one 44pt target cannot fit.
        var safe = new OccupiedArea(0, 24, 678, 466);
        var bar = new OccupiedArea(12, 66, 76, 440);
        var wantedSearch = new OccupiedArea(12, -10, 76, 54);
        var wantedActions = new OccupiedArea(12, -150, 76, -22);

        var result = GlassButtonPlacement.ResolveLane(wantedSearch, wantedActions, hasBack: true, hasOverflow: true, safe, bar, [bar]);

        Assert.Equal(GlassLaneMode.ZeroTargets, result.Mode);
        Assert.Null(result.ActionsFrame);
    }

    [Fact]
    public void ResolveLane_applies_8pt_hysteresis_before_stepping_up_and_respects_large_dynamic_type_target()
    {
        var safe = new OccupiedArea(0, 24, 678, 466);
        // 56pt lane above bar (24..80); obstacle at top down to 32 leaves 48pt (fits 44pt with 0 gap, NOT with 8pt hysteresis).
        var bar = new OccupiedArea(12, 92, 76, 440);
        OccupiedArea[] obstacles = [new(0, 0, 100, 32), bar];
        var wanted = new OccupiedArea(12, 32, 76, 160);

        var fromZero = GlassButtonPlacement.ResolveLane(new(0, 0, 0, 0), wanted, hasBack: true, hasOverflow: false, safe, bar, obstacles, previousMode: GlassLaneMode.ZeroTargets);
        Assert.Equal(GlassLaneMode.ZeroTargets, fromZero.Mode);

        // At maximum Dynamic Type (e.g. 56pt minimum target), 48pt free space also yields ZeroTargets.
        var largeText = GlassButtonPlacement.ResolveLane(new(0, 0, 0, 0), wanted, hasBack: true, hasOverflow: false, safe, bar, obstacles, previousMode: GlassLaneMode.Expanded, minTarget: 56);
        Assert.Equal(GlassLaneMode.ZeroTargets, largeText.Mode);
    }
}
