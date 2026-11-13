using DashyNMS.Mobile.Intelligence;

namespace DashyNMS.Mobile.Tests;

public sealed class IntelligenceElapsedTests
{
    [Theory]
    [InlineData(0, "0s")]
    [InlineData(4.9, "4s")]
    [InlineData(59, "59s")]
    [InlineData(65, "1m 05s")]
    [InlineData(-3, "0s")]
    public void Elapsed_time_reads_in_whole_seconds(double seconds, string expected) =>
        Assert.Equal(expected, IntelligenceElapsed.Format(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void A_running_answer_counts_up_from_when_it_was_asked()
    {
        var asked = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal("Answering… 3s", IntelligenceElapsed.Running(asked, asked.AddSeconds(3)));
    }

    [Fact]
    public void Only_a_completed_answer_with_a_known_finish_says_how_long_it_took()
    {
        var turn = new IntelligenceTurn("q", "balanced") { AskedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero) };
        Assert.Null(IntelligenceElapsed.Answered(turn)); // still running
        turn.State = IntelligenceTurnState.Completed;
        Assert.Null(IntelligenceElapsed.Answered(turn)); // a reopened saved chat
        turn.AnsweredAt = turn.AskedAt.AddSeconds(6);
        Assert.Equal("Answered in 6s", IntelligenceElapsed.Answered(turn));
        turn.State = IntelligenceTurnState.Stopped;
        Assert.Null(IntelligenceElapsed.Answered(turn));
    }
}
