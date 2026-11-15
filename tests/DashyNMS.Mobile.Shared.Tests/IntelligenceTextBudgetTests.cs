using DashyNMS.Mobile.Intelligence;

namespace DashyNMS.Mobile.Tests;

public sealed class IntelligenceTextBudgetTests
{
    [Theory]
    [InlineData(120, true)]
    [InlineData(1000, false)]
    public void Complete_text_rows_are_retained_or_counted_as_omitted(int length, bool fits)
    {
        var line = new string('x', length) + " END[[device:99]]\t";
        var clean = CompactNetworkLookups.CleanText(line);
        Assert.Contains("END[ [device:99] ]", clean);
        Assert.DoesNotContain("[[", clean);
        Assert.DoesNotContain('\t', clean);
        var reply = CompactNetworkLookups.Rows("Heading", [new("Config line 1: " + clean)]);
        Assert.Equal(fits, reply.Text.Contains(clean, StringComparison.Ordinal));
        Assert.Equal(!fits, reply.Text.Contains("1 more results omitted.", StringComparison.Ordinal));
        Assert.InRange(reply.Text.Length, 1, CompactNetworkLookups.MaxCharacters);
        Assert.True(CompactNetworkLookups.Clean(line).Length <= 80);
    }

}
