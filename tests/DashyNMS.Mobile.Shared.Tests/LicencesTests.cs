using DashyNMS.Mobile.Map;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Licences;

namespace DashyNMS.Mobile.Tests;

public sealed class LicencesTests
{
    private sealed class FakePlatform(NoticePlatforms platform) : INoticePlatform
    {
        public NoticePlatforms Platform { get; } = platform;
    }

    private static LicencesViewModel Page(NoticePlatforms platform = NoticePlatforms.iOS) => new(new FakePlatform(platform));

    [Fact]
    public void Every_licence_text_the_phone_names_is_in_Core()
    {
        foreach (var file in PhoneNotices.Own.Select(n => n.LicenceFile).OfType<string>().Distinct())
        {
            Assert.Contains(file, OpenSourceNotices.LicenceFiles);
            Assert.False(string.IsNullOrWhiteSpace(OpenSourceNotices.ReadLicence(file)), file + " is empty");
        }
    }

    [Fact]
    public void Every_notice_has_a_copyright_a_licence_and_an_https_link()
    {
        Assert.All(PhoneNotices.Own.Concat(OpenSourceNotices.Shared), n =>
        {
            Assert.False(string.IsNullOrWhiteSpace(n.Copyright));
            Assert.False(string.IsNullOrWhiteSpace(n.Licence));
            Assert.Equal(Uri.UriSchemeHttps, n.Link.Scheme);
        });
    }

    [Fact]
    public void The_page_lists_Cores_shared_entries_and_the_phones_own()
    {
        var names = Page().Items.Select(i => i.Name).ToList();

        Assert.Contains(".NET", names);          // Core's
        Assert.Contains("OpenStreetMap", names); // Core's
        Assert.Contains(".NET MAUI", names);     // the phone's
        Assert.Contains("Leaflet", names);       // the phone's
        Assert.DoesNotContain("WebView2", names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AndroidX_and_Kotlin_are_listed_only_on_Android()
    {
        var ios = Page(NoticePlatforms.iOS).Items.Select(i => i.Name).ToList();
        var android = Page(NoticePlatforms.Android).Items.Select(i => i.Name).ToList();

        Assert.DoesNotContain("AndroidX", ios);
        Assert.DoesNotContain("Kotlin standard library", ios);
        Assert.Contains("AndroidX", android);
        Assert.Contains("Leaflet", android);
    }

    [Fact]
    public void A_licence_is_read_from_Core_when_shown()
    {
        var leaflet = Page().Items.Single(i => i.Name == "Leaflet");

        Assert.Null(leaflet.LicenceText);
        Assert.Equal("Show licence", leaflet.ToggleText);

        leaflet.ToggleCommand.Execute(null);

        Assert.True(leaflet.IsExpanded);
        Assert.Contains("Volodymyr Agafonkin", leaflet.LicenceText);
        Assert.Equal("Hide licence", leaflet.ToggleText);
    }

    [Fact]
    public void Map_data_has_its_attribution_rather_than_a_licence_text()
    {
        var osm = Page().Items.Single(i => i.Name == "OpenStreetMap");

        Assert.False(osm.HasLicenceText);
        Assert.Equal("Open Database License (ODbL) · www.openstreetmap.org/copyright", osm.LicenceLine);
    }

    [Fact]
    public void The_trademark_line_is_Cores() =>
        Assert.Equal(OpenSourceNotices.Trademarks, Page().Trademarks);

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
