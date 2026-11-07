using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class TabSettlePolicyTests
{
    [Fact]
    public void No_action_once_the_pill_is_found()
    {
        var settle = new TabSettlePolicy();
        settle.Resize(900, 600);
        Assert.Equal(TabSettleAction.None, settle.Check(true, true, true, pillFound: true).Action);
        Assert.True(settle.IsFinished);
        Assert.Equal(TabSettleAction.None, settle.Check(true, true, true, pillFound: false).Action);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void No_action_when_tabs_are_hidden_or_the_view_is_narrow(bool showTabs, bool isWide)
    {
        var settle = new TabSettlePolicy();
        settle.Resize(600, 900);
        Assert.Equal(TabSettleAction.None, settle.Check(showTabs, isWide, true, false).Action);
        Assert.False(settle.IsFinished);
        Assert.Equal(0, settle.Attempt);
    }

    [Fact]
    public void Waits_for_the_controllers_without_using_an_attempt()
    {
        var settle = new TabSettlePolicy();
        settle.Resize(900, 600);
        Assert.Equal(TabSettleAction.Wait, settle.Check(true, true, controllersPresent: false, false).Action);
        Assert.Equal(0, settle.Attempt);
        Assert.Equal(TabSettleStage.Refresh, settle.Check(true, true, true, false).Stage);
    }

    [Fact]
    public void Stages_escalate_in_order_on_a_widening_schedule()
    {
        var settle = new TabSettlePolicy();
        settle.Resize(900, 600);
        var steps = Enumerable.Range(0, 3).Select(_ => settle.Check(true, true, true, false)).ToList();

        Assert.All(steps, step => Assert.Equal(TabSettleAction.Nudge, step.Action));
        Assert.Equal([TabSettleStage.Refresh, TabSettleStage.ToggleBar, TabSettleStage.FlipTraits], steps.Select(s => s.Stage));
        Assert.Equal([150d, 250d, 600d], steps.Select(s => s.NextCheck.TotalMilliseconds));
    }

    [Fact]
    public void Gives_up_at_the_attempt_limit_and_stays_quiet()
    {
        var settle = new TabSettlePolicy();
        settle.Resize(900, 600);
        for (var i = 0; i < 3; i++)
            settle.Check(true, true, true, false);

        Assert.Equal(TabSettleAction.GiveUp, settle.Check(true, true, true, false).Action);
        Assert.True(settle.IsFinished);
        Assert.Equal(TabSettleAction.None, settle.Check(true, true, true, false).Action);
    }

    [Fact]
    public void A_size_change_starts_the_escalation_again()
    {
        var settle = new TabSettlePolicy();
        settle.Resize(900, 600);
        for (var i = 0; i < 4; i++)
            settle.Check(true, true, true, false);

        Assert.False(settle.Resize(900, 600));
        Assert.True(settle.IsFinished);
        Assert.True(settle.Resize(1000, 700));
        Assert.False(settle.IsFinished);
        Assert.Equal(TabSettleStage.Refresh, settle.Check(true, true, true, false).Stage);
    }

    [Fact]
    public void Repeated_size_changes_cannot_nudge_without_end()
    {
        var settle = new TabSettlePolicy();
        var nudges = 0;
        for (var size = 0; size < 20; size++)
        {
            settle.Resize(900 + size, 600);
            while (settle.Check(true, true, true, false).Action == TabSettleAction.Nudge)
                nudges++;
        }

        Assert.Equal(TabSettlePolicy.MostNudges, nudges);
    }

    [Fact]
    public void Decide_ignores_a_negative_attempt() =>
        Assert.Equal(TabSettleStage.Refresh, TabSettlePolicy.Decide(-1, true, true, true, false).Stage);
}
