using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Tests;

/// <summary>When the app asks before acting - the same policy as desktop's (#146).</summary>
public sealed class ConfirmationsTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(5, false)]
    [InlineData(6, true)]
    [InlineData(50, true)]
    public void A_bulk_action_asks_only_above_five(int count, bool asks) =>
        Assert.Equal(asks, Confirmations.AsksForBulk(count));

    [Fact]
    public void Matches_desktops_wording() =>
        Assert.Equal("This can't be undone.", Confirmations.CannotBeUndone);
}
