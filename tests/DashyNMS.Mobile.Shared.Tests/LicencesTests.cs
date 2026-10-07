using DashyNMS.Mobile.Map;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Tests;

public sealed class LicencesTests
{
    private sealed class FakeTexts(bool android) : ILicenceTexts
    {
        public bool IsAndroid { get; } = android;

        public List<string> Read { get; } = [];

        public Task<string> ReadAsync(string licenceFile)
        {
            Read.Add(licenceFile);
            return Task.FromResult("text of " + licenceFile);
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Couldn't find the repository root.");
    }

    [Fact]
    public void Every_licence_file_ships_with_the_app()
    {
        var folder = Path.Combine(RepoRoot(), "src", "DashyNMS.Mobile", "Resources", "Raw", OpenSourceNotices.LicencesFolder);

        foreach (var file in OpenSourceNotices.All.Select(n => n.LicenceFile).OfType<string>().Distinct())
        {
            Assert.True(File.Exists(Path.Combine(folder, file)), file + " is missing from Resources/Raw/licences");
        }
    }

    [Fact]
    public void Every_notice_has_a_copyright_a_licence_and_an_https_link()
    {
        Assert.All(OpenSourceNotices.All, n =>
        {
            Assert.False(string.IsNullOrWhiteSpace(n.Copyright));
            Assert.False(string.IsNullOrWhiteSpace(n.Licence));
            Assert.Equal(Uri.UriSchemeHttps, n.Link.Scheme);
        });
    }

    [Fact]
    public void AndroidX_and_Kotlin_are_listed_only_on_Android()
    {
        var ios = OpenSourceNotices.For(android: false).Select(n => n.Name).ToList();
        var android = OpenSourceNotices.For(android: true).Select(n => n.Name).ToList();

        Assert.DoesNotContain("AndroidX", ios);
        Assert.DoesNotContain("Kotlin standard library", ios);
        Assert.Contains("AndroidX", android);
        Assert.Contains("Leaflet", ios);
        Assert.Contains("OpenStreetMap", ios);
    }

    [Fact]
    public async Task A_licence_is_read_when_first_shown_and_only_then()
    {
        var texts = new FakeTexts(android: false);
        var vm = new LicencesViewModel(texts);
        var leaflet = vm.Items.Single(i => i.Name == "Leaflet");

        Assert.Empty(texts.Read);
        Assert.Equal("Show licence", leaflet.ToggleText);

        await leaflet.ToggleCommand.ExecuteAsync(null);
        Assert.True(leaflet.IsExpanded);
        Assert.Equal("text of bsd-2-clause-leaflet.txt", leaflet.LicenceText);
        Assert.Equal("Hide licence", leaflet.ToggleText);

        await leaflet.ToggleCommand.ExecuteAsync(null);
        await leaflet.ToggleCommand.ExecuteAsync(null);
        Assert.Single(texts.Read);
    }

    [Fact]
    public void Map_data_has_its_attribution_rather_than_a_licence_text()
    {
        var osm = new LicencesViewModel(new FakeTexts(false)).Items.Single(i => i.Name == "OpenStreetMap");

        Assert.False(osm.HasLicenceText);
        Assert.Equal("Open Database License (ODbL) · www.openstreetmap.org/copyright", osm.LicenceLine);
    }

    [Fact]
    public void The_trademark_line_names_LibreNMS_and_Graylog() =>
        Assert.Equal(
            "DashyNMS isn't affiliated with LibreNMS or Graylog. LibreNMS and Graylog are trademarks of their respective owners.",
            new LicencesViewModel(new FakeTexts(false)).Trademarks);

    [Theory]
    [InlineData("https://www.openstreetmap.org/copyright", true)]
    [InlineData("https://leafletjs.com/", true)]
    [InlineData("https://dashynms.pckp.net/mobile/", false)] // the page itself, loading
    [InlineData("dashynms-map://pins/3", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///index.html", false)]
    [InlineData(null, false)]
    public void Only_web_links_away_from_the_map_open_in_the_browser(string? url, bool opens) =>
        Assert.Equal(opens, MapHtml.ExternalLink(url) is not null);

    [Fact]
    public void The_map_names_the_app_to_tile_servers()
    {
        Assert.Equal("DashyNMS-Mobile/1.1.0 (+https://dashynms.pckp.net/mobile/)", MapHtml.UserAgent("1.1.0"));
        Assert.Equal("https://dashynms.pckp.net/mobile/", MapHtml.BaseUrl.AbsoluteUri);
    }
}
