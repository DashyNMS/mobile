using DashyNMS.Mobile.Intelligence;

namespace DashyNMS.Mobile.Tests;

public sealed class IntelligenceStarterActivityTests
{
    [Fact]
    public void Desktop_rotation_shows_four_and_covers_twelve_before_repeating()
    {
        var rotation = new IntelligenceStarterQuestions();
        var first = rotation.Next(true, true);
        Assert.Equal(4, first.Count);
        var all = first.Concat(rotation.Next(true, true)).Concat(rotation.Next(true, true)).ToArray();
        Assert.Equal(12, all.Distinct().Count());
        Assert.Equal(first, rotation.Next(true, true));
    }
    [Fact]
    public void Conditions_and_tools_filter_questions_until_they_are_available()
    {
        var rotation = new IntelligenceStarterQuestions();
        var empty = Enumerable.Range(0, 4).SelectMany(_ => rotation.Next(false, false)).ToArray();
        Assert.DoesNotContain(empty, q => q.StartsWith("Which devices", StringComparison.Ordinal) || q.Contains("alert", StringComparison.OrdinalIgnoreCase));
        var onlyOverview = rotation.Next(true, true, new HashSet<string> { "network_overview" });
        Assert.Equal(new[] { "How is the network doing?" }, onlyOverview);
        var full = new IntelligenceStarterQuestions().Next(true, true);
        Assert.Contains("What changed in the last 24 hours?", full);
    }
    [Fact]
    public void Activity_matches_desktop_and_counts_unreadable_lookups()
    {
        var turn = new IntelligenceTurn("Question", "brief");
        turn.Lookups.Add(new("active_alerts"));
        Assert.Equal("Checking alerts…", IntelligenceActivity.Describe(turn));
        turn.Lookups[0].State = IntelligenceLookupState.Completed;
        turn.Lookups.Add(new("find_device") { State = IntelligenceLookupState.Failed });
        turn.State = IntelligenceTurnState.Completed;
        Assert.Equal("Looked at alerts and devices (1 couldn't be read)", IntelligenceActivity.Describe(turn));
    }
    [Fact]
    public void Every_catalogue_tool_has_named_activity()
    {
        foreach (var tool in CompactNetworkLookups.ReviewedTools)
        {
            Assert.NotEqual("Looking it up", IntelligenceActivity.Step(tool));
            Assert.NotEqual("the network", IntelligenceActivity.Subject(tool));
        }
    }
}
