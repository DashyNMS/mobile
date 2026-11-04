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
    public async Task Browser_link_uses_only_the_release_destination_and_does_not_mark_seen()
    {
        var launcher = Substitute.For<ILauncherService>();
        var vm = new WhatsNewViewModel(NewWhatsNew(), _navigation, launcher);
        await vm.OpenReleaseNotesCommand.ExecuteAsync(null);
        await launcher.Received(1).OpenAsync(new Uri("https://github.com/DashyNMS/mobile/releases"));
        Assert.Null(_appSettings.WhatsNewShownVersion);
        _settings.DidNotReceive().Save();
        Assert.Empty(_navigation.Visits);
    }

    [Fact]
    public async Task Completion_records_once_and_cannot_pop_twice_while_returning()
    {
        var navigation = Substitute.For<INavigationService>();
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        navigation.GoToAsync(Routes.Back).Returns(returned.Task);
        var vm = new WhatsNewViewModel(NewWhatsNew(), navigation);

        var first = vm.GotItCommand.ExecuteAsync(null);
        Assert.False(vm.CanDismiss);
        await vm.GotItCommand.ExecuteAsync(null);
        returned.SetResult();
        await first;

        _settings.Received(1).Save();
        await navigation.Received(1).GoToAsync(Routes.Back);
    }

    [Fact]
    public async Task Leaving_without_completion_keeps_update_due_next_launch()
    {
        _appSettings.ServerUrl = "https://nms.example.net/";
        _appSettings.WhatsNewShownVersion = "1.0.0";
        var launch = NewWhatsNew();
        await launch.ShowIfDueAsync(_navigation);
        _ = new WhatsNewViewModel(launch, _navigation);
        await launch.ShowIfDueAsync(_navigation);
        await NewWhatsNew().ShowIfDueAsync(_navigation);

        Assert.Equal(2, _navigation.Visits.Count);
        Assert.Equal("1.0.0", _appSettings.WhatsNewShownVersion);
        _settings.DidNotReceive().Save();
    }

    [Theory]
    [InlineData("")]
    [InlineData("# 2.0.0\n## New\nNo valid changes")]
    public void Missing_or_unparseable_notes_have_an_honest_empty_state(string notes)
    {
        var service = new WhatsNew(_settings, notes, "2.0.0");
        var vm = new WhatsNewViewModel(service, _navigation);
        Assert.True(vm.NotesUnavailable);
        Assert.Empty(vm.Groups);
        Assert.False(service.IsDue);
    }

    [Fact]
    public void Long_continued_text_and_markup_are_preserved_as_plain_text()
    {
        var url = "https://example.net/" + new string('a', 400);
        var text = "A long paragraph " + new string('b', 1000);
        var service = new WhatsNew(_settings,
            $"# 2.0.0\n## New\n- **Long change.** {text}\n  {url} <script>plain text</script> {{unknown}}", "2.0.0");
        var vm = new WhatsNewViewModel(service, _navigation);
        var row = Assert.Single(Assert.Single(vm.Groups).Rows);
        Assert.Equal(text + " " + url + " <script>plain text</script>", row.Text);
        Assert.False(row.HasImage);
    }

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

        Assert.Equal("Version 2.0.0", vm.Version);
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
