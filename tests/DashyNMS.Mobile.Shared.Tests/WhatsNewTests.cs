using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Tests;

/// <summary>What's new after an update, and in About (#159), as desktop's (DashyNMS/desktop#244).</summary>
public sealed class WhatsNewTests
{
    private const string Notes = """
        # 2.0.0
        Released 30 November 2026

        ## New
        - **Alert groups.** Faults grouped by device and rule. {alerts}

        ## Fixed
        - **Missing readings.** A dash and no colour. {unknown-icon}
        """;

    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly RecordingNavigation _navigation = new();

    public WhatsNewTests() => _settings = Fakes.Settings(_appSettings);

    private WhatsNew NewWhatsNew(string version = "2.0.0") => new(_settings, Notes, version);

    [Fact]
    public async Task An_update_shows_it_once_and_reopening_the_app_doesnt_show_it_again()
    {
        _appSettings.ServerUrl = "https://nms.example.net/";
        _appSettings.WhatsNewShownVersion = "1.1.0";
        var launch = NewWhatsNew();
        launch.MarkSeenIfFreshInstall();

        await launch.ShowIfDueAsync(_navigation);
        await launch.ShowIfDueAsync(_navigation); // another page, same launch
        Assert.Equal([Routes.WhatsNew], _navigation.Visits.Select(v => v.Route));

        await new WhatsNewViewModel(launch, _navigation).GotItCommand.ExecuteAsync(null);
        Assert.Equal("2.0.0", _appSettings.WhatsNewShownVersion);
        _settings.Received().Save();

        var nextLaunch = NewWhatsNew();
        await nextLaunch.ShowIfDueAsync(_navigation);
        Assert.Equal(2, _navigation.Visits.Count); // just Got it's Back
    }

    [Fact]
    public async Task An_update_from_1_0_which_recorded_nothing_still_shows_it()
    {
        _appSettings.ServerUrl = "https://nms.example.net/";
        var launch = NewWhatsNew();
        launch.MarkSeenIfFreshInstall();

        await launch.ShowIfDueAsync(_navigation);

        Assert.Equal(Routes.WhatsNew, _navigation.Visits.Single().Route);
    }

    [Fact]
    public async Task A_fresh_install_never_shows_it_unprompted()
    {
        var launch = NewWhatsNew();
        launch.MarkSeenIfFreshInstall(); // before sign-in saves a server
        _appSettings.ServerUrl = "https://nms.example.net/";

        await launch.ShowIfDueAsync(_navigation);
        await NewWhatsNew().ShowIfDueAsync(_navigation);

        Assert.Empty(_navigation.Visits);
    }

    [Fact]
    public void A_preview_build_doesnt_show_it()
    {
        _appSettings.ServerUrl = "https://nms.example.net/";

        Assert.False(NewWhatsNew("2.0.0-preview.3").IsDue);
    }

    [Fact]
    public async Task Settings_About_reopens_it_at_any_time()
    {
        _appSettings.WhatsNewShownVersion = "2.0.0";
        var whatsNew = NewWhatsNew();
        var vm = SettingsAboutTests.NewViewModel(whatsNew: whatsNew, navigation: _navigation);

        Assert.True(vm.HasWhatsNew);
        Assert.Equal("Alert groups and Missing readings", vm.WhatsNewSummary);
        await vm.OpenWhatsNewCommand.ExecuteAsync(null);

        Assert.Equal(Routes.WhatsNew, _navigation.Visits.Single().Route);
    }

    [Fact]
    public void The_sheet_shows_the_release_its_date_and_its_sections_with_the_apps_glyphs()
    {
        var vm = new WhatsNewViewModel(NewWhatsNew(), _navigation);

        Assert.Equal("What's new in 2.0.0", vm.Title);
        Assert.Equal("Released 30 November 2026", vm.Released);
        Assert.Equal(["New", "Fixed"], vm.Groups.Select(g => g.Name));
        Assert.Equal(("Alert groups", "Faults grouped by device and rule.", "tab_alerts.png"),
            (vm.Groups[0].Rows[0].Title, vm.Groups[0].Rows[0].Text, vm.Groups[0].Rows[0].Image));
        Assert.False(vm.Groups[1].Rows[0].HasImage);
    }

    [Fact]
    public void The_embedded_notes_are_this_releases()
    {
        var whatsNew = new WhatsNew(_settings);

        Assert.NotNull(whatsNew.Notes);
        Assert.Equal(whatsNew.CurrentVersion, whatsNew.Notes.Version);
        Assert.All(whatsNew.Notes.Sections.SelectMany(s => s.Items), i => Assert.NotNull(WhatsNewViewModel.ImageFor(i.Icon)));
    }
}
