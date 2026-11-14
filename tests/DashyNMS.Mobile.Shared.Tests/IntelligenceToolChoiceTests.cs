using DashyNMS.Mobile.Intelligence;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// Each question's tools and lookup limit (#637), over the phone's real tool definitions, with each
/// definition costed as the conversation costs it where the model can't count.
/// </summary>
public sealed class IntelligenceToolChoiceTests
{
    private static readonly IReadOnlyList<IntelligenceLookup> Tools =
        new CompactNetworkLookups(Fakes.Client(), Fakes.Settings(), Fakes.Session()).Create();

    private static int Estimate(string text) => IntelligenceConversationViewModel.Estimate(text);

    private static IntelligenceBudget Budget(int context, int instructions = 600, int prompt = 40, int output = 300) => new(
        context, instructions, prompt, output, Estimate(IntelligenceConversationViewModel.SampleResult),
        Tools.ToDictionary(t => t.Name, t => Estimate(IntelligenceConversationViewModel.Definition(t.Name, t.Purpose, t.Parameters))));

    private static IntelligenceChoice Choose(string question, int context = 4096, string? spotlight = null, params string[] earlier)
        => IntelligenceToolChoice.Choose(question, earlier, spotlight, Tools, Budget(context));

    private static string[] Names(IntelligenceChoice choice) => choice.Tools.Select(t => t.Name).ToArray();

    public static TheoryData<string, string> StarterQuestions() => new()
    {
        { "How is the network doing?", "network_overview" },
        { "Which open alerts need attention?", "active_alerts" },
        { "Which devices are down?", "find_device" },
        { "Summarise the active alerts.", "active_alerts" },
        { "How many devices are up and down?", "network_overview" },
        { "Which alerts are unacknowledged?", "active_alerts" },
        { "Give me a brief network overview.", "network_overview" },
        { "Which devices need attention?", "find_device" },
        { "What are the most severe active alerts?", "active_alerts" },
    };

    [Fact]
    public void Every_starter_question_is_covered_here()
        => Assert.Equal(IntelligenceConversationViewModel.StarterSets.SelectMany(s => s).Order(), StarterQuestions().Select(r => (string)r[0]).Order());

    [Theory]
    [MemberData(nameof(StarterQuestions))]
    public void Each_starter_question_gets_the_tool_it_needs_within_4096_tokens(string question, string tool)
    {
        var choice = Choose(question);

        Assert.Contains(tool, Names(choice));
        Assert.Equal(2, choice.MaxLookups);
        var budget = Budget(4096);
        Assert.True(choice.Tools.Sum(t => budget.Tools[t.Name]) + choice.MaxLookups * budget.Result <= budget.Room || choice.Tools.Count == 1);
    }

    [Fact]
    public void A_question_naming_a_device_gets_the_device_tools()
    {
        var choice = Choose("What OS is core-sw1 running?");

        Assert.Equal("device_health", Names(choice)[0]);
    }

    [Theory]
    [InlineData("Which sensors are too hot?", "device_sensors")]
    [InlineData("Are the fans on 198.51.100.4 OK?", "device_sensors")]
    [InlineData("Find the switches at the lab site", "find_device")]
    public void A_question_gets_the_tool_its_words_point_to(string question, string tool)
        => Assert.Equal(tool, Names(Choose(question))[0]);

    [Fact]
    public void A_question_that_names_nothing_starts_with_the_overview_and_alerts()
    {
        var choice = Choose("Hello", 8192);

        Assert.Equal(["network_overview", "active_alerts"], Names(choice).Take(2));
    }

    [Fact]
    public void A_spotlight_chat_offers_the_device_tools_for_a_question_that_names_none()
    {
        var plain = Choose("Anything I should know?", 8192);
        var spotlit = Choose("Anything I should know?", 8192, spotlight: "core-sw1");

        Assert.DoesNotContain("device_health", Names(plain));
        Assert.Contains("device_health", Names(spotlit));
        Assert.Contains("device_sensors", Names(spotlit));
    }

    [Fact]
    public void A_follow_up_keeps_the_subject_of_the_question_before()
    {
        var choice = Choose("And its sensors?", 8192, null, "How is core-sw1 doing?");

        Assert.Equal("device_sensors", Names(choice)[0]);
        Assert.Contains("device_health", Names(choice));
    }

    [Fact]
    public void The_tools_a_description_names_come_along_when_they_fit()
    {
        var choice = Choose("How is the network doing?", 8192);

        // network_overview names find_device and active_alerts; the top_ tools aren't on the phone yet.
        Assert.Equal(["network_overview", "find_device", "active_alerts"], Names(choice).Take(3));
        Assert.Contains("top_interfaces", choice.NotOffered);
        Assert.DoesNotContain("find_device", choice.NotOffered);
    }

    [Fact]
    public void A_larger_context_gets_more_lookups_and_more_tools()
    {
        var small = Choose("Which open alerts need attention?", 4096);
        var large = Choose("Which open alerts need attention?", 8192);

        Assert.Equal(2, small.MaxLookups);
        Assert.True(large.MaxLookups > small.MaxLookups);
        Assert.True(large.Tools.Count >= small.Tools.Count);
    }

    [Fact]
    public void Private_cloud_computes_context_holds_every_tool_and_up_to_desktops_limit()
    {
        var choice = Choose("Which open alerts need attention?", 32_000);

        Assert.InRange(choice.MaxLookups, 3, IntelligenceToolChoice.DesktopLookups);
        Assert.Equal(IntelligenceToolChoice.DesktopLookups, IntelligenceToolChoice.Choose("x", [], null, Tools, Budget(1_000_000)).MaxLookups);
    }

    [Fact]
    public void A_context_too_small_for_more_still_gets_one_tool_and_two_lookups()
    {
        var choice = IntelligenceToolChoice.Choose("Which open alerts need attention?", [], null, Tools, Budget(4096, instructions: 3000));

        Assert.Equal("active_alerts", Assert.Single(choice.Tools).Name);
        Assert.Equal(2, choice.MaxLookups);
    }

    [Theory]
    [InlineData("Is OSPF up between the cores?")]
    [InlineData("Which alert template does the rule use?")]
    public void Short_cues_match_whole_words_only(string question)
    {
        var choice = Choose(question, 8192);

        Assert.DoesNotContain("device_health", Names(choice).Take(1));
        Assert.DoesNotContain("device_sensors", Names(choice).Take(1));
    }

    [Fact]
    public void Only_named_tools_the_question_doesnt_get_are_not_offered()
    {
        var choice = Choose("What OS is core-sw1 running?");

        Assert.All(choice.NotOffered, name => Assert.DoesNotContain(name, Names(choice)));
        Assert.All(choice.NotOffered, name => Assert.Contains(choice.Tools, t => t.NamedTools.Contains(name)));
    }
}
