using System.Net;
using System.Text;
using DashyNMS.Mobile.Help;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Help;

namespace DashyNMS.Mobile.Tests;

/// <summary>Moving about Help (#668). The small guide here is made up; the bundled one is checked on its own.</summary>
public sealed class HelpViewModelTests : IDisposable
{
    private readonly RecordingNavigation _navigation = new();
    private readonly OpenedLinks _launcher = new();
    private readonly CountingHandler _web = new();
    private readonly HelpGuideService _guides;

    public HelpViewModelTests() => _guides = new HelpGuideService(HelpProduct.Mobile, HelpRelease.ForVersion("2.0.0"),
        new HelpFileSet(new Dictionary<string, string>
        {
            ["docs/guide/README.md"] = "# Guide\n\n## Tasks\n\n- [Widgets](widgets.md)\n- [Fixes](fixes.md)",
            ["docs/guide/widgets.md"] = "# Widgets\n\nSee [fixes](fixes.md#stuck), [the site](https://example.net/a), [gone](gone.md).\n\n## Remove\n\nUnpin it.",
            ["docs/guide/fixes.md"] = "# Fixes\n\n## Stuck\n\nRestart the gizmo.",
        }.Select(file => new KeyValuePair<string, byte[]>(file.Key, Encoding.UTF8.GetBytes(file.Value)))),
        http: new HttpClient(_web));

    public void Dispose() => _guides.Dispose();

    private HelpViewModel Contents(bool mayRefresh = false) => new(_guides, _navigation, new Policy(mayRefresh));

    private HelpArticleViewModel Article(string page, string? anchor = null)
    {
        var article = new HelpArticleViewModel(_guides, _navigation, _launcher);
        article.Open(new HelpTopic(page, anchor));
        return article;
    }

    [Fact]
    public void Contents_are_grouped_and_say_which_release_they_describe()
    {
        var help = Contents();

        Assert.Equal(["Guide", "Widgets", "Fixes"], help.Rows.Select(row => row.Title));
        Assert.Equal([null, "Tasks", null], help.Rows.Select(row => row.Group));
        Assert.Equal([false, false, true], help.Rows.Select(row => row.HasLine));
        Assert.Equal("Help for DashyNMS Mobile 2.0.0, saved in the app", help.ReleaseText);
    }

    [Fact]
    public async Task A_row_opens_its_page_so_back_returns_to_the_list()
    {
        var help = Contents();
        await help.OpenCommand.ExecuteAsync(help.Rows[2]);

        var visit = Assert.Single(_navigation.Visits);
        Assert.Equal(Routes.HelpArticle, visit.Route);
        Assert.Equal(new HelpTopic("Fixes"), visit.Parameters![Routes.HelpTopicParameter]);
    }

    [Fact]
    public async Task Search_finds_a_heading_and_opens_there_and_clearing_it_restores_the_contents()
    {
        var help = Contents();
        help.SearchText = "gizmo";

        var row = Assert.Single(help.Rows);
        Assert.Equal("Fixes", row.Title);
        Assert.StartsWith("Stuck\n", row.Summary, StringComparison.Ordinal);
        Assert.Equal("1 match for “gizmo”", help.ResultsText);
        await help.OpenCommand.ExecuteAsync(row);
        Assert.Equal(new HelpTopic("Fixes", "stuck"), _navigation.Visits[0].Parameters![Routes.HelpTopicParameter]);

        help.ClearSearchCommand.Execute(null);
        Assert.Equal(3, help.Rows.Count);
        Assert.False(help.IsSearching);
    }

    [Fact]
    public void Nothing_found_is_not_an_empty_guide()
    {
        var help = Contents();
        help.SearchText = "zeppelin";

        Assert.True(help.ShowNoResults);
        Assert.False(help.IsEmpty);
        Assert.Equal("Nothing in the guide matches “zeppelin”.", help.ResultsText);
    }

    [Fact]
    public async Task A_link_to_another_page_opens_it_in_the_app()
    {
        await Article("Widgets").ActivateCommand.ExecuteAsync("fixes.md#stuck");

        Assert.Equal(new HelpTopic("Fixes", "stuck"), Assert.Single(_navigation.Visits).Parameters![Routes.HelpTopicParameter]);
        Assert.Empty(_launcher.Opened);
    }

    [Fact]
    public async Task A_link_outside_the_guide_opens_the_browser()
    {
        await Article("Widgets").ActivateCommand.ExecuteAsync("https://example.net/a");

        Assert.Equal(new Uri("https://example.net/a"), Assert.Single(_launcher.Opened));
        Assert.Empty(_navigation.Visits);
    }

    [Theory]
    [InlineData("gone.md")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/hosts")]
    [InlineData("../../../../etc/hosts")]
    public async Task A_link_that_goes_nowhere_or_is_unsafe_does_nothing(string target)
    {
        var article = Article("Widgets");
        await article.ActivateCommand.ExecuteAsync(target);

        Assert.False(HelpArticleViewModel.Opens(article.Resolve(target)) && _launcher.Opened.Count > 0);
        Assert.Empty(_navigation.Visits);
        Assert.Empty(_launcher.Opened);
    }

    [Fact]
    public async Task A_link_to_a_heading_on_the_same_page_does_not_stack_the_page_on_itself()
    {
        var article = Article("Widgets");
        HelpArticleShown? shown = null;
        article.Shown += (_, e) => shown = e;
        await article.ActivateCommand.ExecuteAsync("#remove");

        Assert.Equal("remove", shown!.Anchor);
        Assert.Empty(_navigation.Visits);
    }

    [Fact]
    public void A_topic_the_guide_does_not_have_opens_its_front_page_at_the_top()
    {
        var article = Article("Written-next-year", "somewhere");

        Assert.Equal("Guide", article.Title);
        Assert.False(article.IsMissing);
    }

    [Fact]
    public void Nothing_is_requested_when_refreshing_is_not_allowed()
    {
        var help = Contents(mayRefresh: false);
        help.Appearing();
        help.Disappearing();

        Assert.Equal(0, _web.Requests);
    }

    [Fact]
    public async Task With_no_network_the_bundled_pages_stay()
    {
        var help = Contents(mayRefresh: true);
        help.Appearing();
        for (var i = 0; i < 100 && _web.Requests == 0; i++) await Task.Delay(20);

        Assert.True(_web.Requests > 0);
        Assert.Equal(3, help.Rows.Count);
        Assert.Equal(HelpGuideSource.Bundled, _guides.Source);
    }

    [Fact]
    public void A_search_with_more_matches_than_it_shows_says_so()
    {
        using var guides = new HelpGuideService(HelpProduct.Mobile, null, new HelpFileSet(Enumerable.Range(1, 30)
            .Select(i => new KeyValuePair<string, byte[]>($"docs/guide/page-{i}.md", Encoding.UTF8.GetBytes($"# Page {i}\n\nThe gizmo, again.")))
            .Append(new KeyValuePair<string, byte[]>("docs/guide/README.md", Encoding.UTF8.GetBytes("# Guide")))));
        var help = new HelpViewModel(guides, _navigation, new Policy(false)) { SearchText = "gizmo" };

        Assert.Equal(20, help.Rows.Count);
        Assert.Equal("The first 20 matches for “gizmo”. Add words to narrow it.", help.ResultsText);
    }

    [Fact]
    public void The_bundled_guide_is_whole()
    {
        var guide = HelpGuide.Load(HelpProduct.Mobile, HelpRelease.ForVersion("2.0.0"), BundledHelp.Files);

        Assert.True(guide.Pages.Count > 15);
        Assert.DoesNotContain(guide.Contents, group => group.Name == "More");
        foreach (var page in guide.Pages)
        {
            foreach (var block in All(HelpBlocks.Read(page.Markdown)))
            {
                if (block is HelpImageBlock image)
                {
                    Assert.True(guide.ResolveImage(page, image.Target) is not null, $"{page.Path}: {image.Target}");
                    Assert.False(string.IsNullOrWhiteSpace(image.AltText), $"{page.Path}: {image.Target} has no description");
                }

                foreach (var link in Spans(block).Select(span => span.Link).OfType<string>().Distinct())
                {
                    Assert.False(guide.ResolveLink(page, link) is HelpBrokenLink, $"{page.Path}: {link}");
                }
            }
        }
    }

    [Fact]
    public void Every_part_of_the_app_has_a_page_the_guide_really_has()
    {
        var guide = HelpGuide.Load(HelpProduct.Mobile, null, BundledHelp.Files);

        foreach (var topic in HelpTopics.All)
        {
            var page = guide.Find(topic.PageId);
            Assert.True(page is not null, topic.PageId);
            Assert.True(topic.Anchor is null || page!.FindHeading(topic.Anchor) is not null, $"{topic.PageId}#{topic.Anchor}");
        }
    }

    private static IEnumerable<HelpBlock> All(IEnumerable<HelpBlock> blocks) => blocks.SelectMany(block => block switch
    {
        HelpListBlock list => All(list.Items.SelectMany(item => item)).Prepend(block),
        HelpQuoteBlock quote => All(quote.Blocks).Prepend(block),
        _ => [block],
    });

    private static IEnumerable<HelpSpan> Spans(HelpBlock block) => block switch
    {
        HelpParagraphBlock paragraph => paragraph.Spans,
        HelpHeadingBlock heading => heading.Spans,
        HelpTableBlock table => table.Rows.SelectMany(row => row.SelectMany(cell => cell)),
        _ => [],
    };

    private sealed class Policy(bool mayRefresh) : IHelpRefreshPolicy
    {
        public bool MayRefresh => mayRefresh;
    }

    private sealed class OpenedLinks : ILauncherService
    {
        public List<Uri> Opened { get; } = [];

        public Task OpenAsync(Uri uri)
        {
            Opened.Add(uri);
            return Task.CompletedTask;
        }

        public Task<bool> TryOpenAppAsync(Uri uri) => Task.FromResult(false);
    }

    /// <summary>No network: every request fails, and is counted.</summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Requests);
            throw new HttpRequestException("offline", null, HttpStatusCode.ServiceUnavailable);
        }
    }
}
