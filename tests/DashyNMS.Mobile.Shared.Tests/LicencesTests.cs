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
    public void Lucide_resource_retains_complete_pinned_upstream_text()
    {
        var text = PhoneNotices.ReadLicence(PhoneNotices.LucideLicence);
        Assert.NotNull(text);
        // Git may convert resource line endings on Windows; compare the pinned LF text.
        Assert.Equal("B495047BD93A9B06913511076F504DABA17D5BBEB3E0650F3BB53A4220329C57",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n")))));
        Assert.Contains("Lucide (including Feather-derived icons)", Page().Items.Select(item => item.Name));
    }

    [Fact]
    public void Every_licence_text_the_phone_names_is_embedded()
    {
        foreach (var file in PhoneNotices.Own.Select(n => n.LicenceFile).OfType<string>().Distinct())
        {
            Assert.False(string.IsNullOrWhiteSpace(PhoneNotices.ReadLicence(file)), file + " is empty");
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
    public void The_trademark_line_includes_project_and_upstream_marks() =>
        Assert.Equal(PhoneNotices.TrademarkReservation + " " + OpenSourceNotices.Trademarks, Page().Trademarks);

    [Theory]
    [InlineData(NoticePlatforms.iOS)]
    [InlineData(NoticePlatforms.Android)]
    public void Every_platform_notice_and_full_resource_survives_expansion_and_reopening(NoticePlatforms platform)
    {
        var page = Page(platform);
        Assert.Equal(PhoneNotices.For(platform), page.Items.Select(item => item.Notice));
        foreach (var item in page.Items)
        {
            Assert.Equal(item.Notice.Link.AbsoluteUri, item.LinkText);
            Assert.Equal(item.Notice.Licence, item.LicenceName);
            Assert.Equal(item.Notice.Copyright, item.Copyright);
            if (item.Notice.LicenceFile is not { } file) continue;
            var complete = PhoneNotices.ReadLicence(file);
            Assert.False(string.IsNullOrWhiteSpace(complete));
            item.ToggleCommand.Execute(null);
            Assert.Equal(complete, item.LicenceText); // Includes all whitespace and the final notice.
            item.ToggleCommand.Execute(null);
            Assert.False(item.IsExpanded);
            item.ToggleCommand.Execute(null);
            Assert.Equal(complete, item.LicenceText);
            Assert.Null(item.LoadError);
        }
    }

    [Fact]
    public void Missing_resource_keeps_retry_available_without_substituting_legal_text()
    {
        var item = new LicenceItem(new OpenSourceNotice("Example", "Test", "Copyright Example",
            "Example licence", new Uri("https://example.net/licence"), "missing-test-resource.txt"));
        item.ToggleCommand.Execute(null);
        Assert.Null(item.LicenceText);
        Assert.False(item.IsExpanded);
        Assert.NotNull(item.LoadError);
        Assert.Equal("Retry licence for Example", item.ToggleDescription);
        item.ToggleCommand.Execute(null);
        Assert.Null(item.LicenceText);
        Assert.False(item.IsExpanded);
    }

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
