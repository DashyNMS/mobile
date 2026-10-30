using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Configuration;
using NSubstitute;

namespace DashyNMS.Mobile.Tests;

/// <summary>Asking once whether to share error reports and usage (#216), as desktop does (DashyNMS/desktop#62, #498).</summary>
public sealed class DiagnosticsConsentTests
{
    private readonly InMemoryPreferences _preferences = new();
    private readonly RecordingNavigation _navigation = new();

    private DiagnosticsConsent Launch(bool available = true) => new(_preferences, available);

    [Fact]
    public async Task Nothing_is_shared_before_a_choice_and_the_sheet_is_shown_once_a_launch()
    {
        var consent = Launch();

        Assert.False(consent.ShareErrors);
        Assert.False(consent.ShareUsage);
        Assert.True(await consent.ShowIfDueAsync(_navigation));
        Assert.False(await consent.ShowIfDueAsync(_navigation)); // another page, same launch

        Assert.Equal([Routes.DiagnosticsSharing], _navigation.Visits.Select(v => v.Route));
    }

    [Fact]
    public async Task Leaving_the_sheet_without_choosing_asks_again_at_the_next_launch()
    {
        await Launch().ShowIfDueAsync(_navigation);

        var next = Launch(); // the app was force-quit with the sheet open
        Assert.True(await next.ShowIfDueAsync(_navigation));
        Assert.False(next.ShareErrors || next.ShareUsage);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Allow_selected_shares_what_was_ticked_and_isnt_asked_again(bool errors, bool usage)
    {
        var consent = Launch();
        var vm = new DiagnosticsSharingViewModel(consent, _navigation) { ShareErrors = errors, ShareUsage = usage };

        await vm.AllowSelectedCommand.ExecuteAsync(null);

        Assert.Equal((errors, usage), (consent.ShareErrors, consent.ShareUsage));
        Assert.Equal(Routes.Back, _navigation.Visits.Last().Route);
        Assert.False(await Launch().ShowIfDueAsync(_navigation));
    }

    [Fact]
    public void Both_start_unticked_and_allow_selected_needs_one_ticked()
    {
        var vm = new DiagnosticsSharingViewModel(Launch(), _navigation);

        Assert.False(vm.ShareErrors || vm.ShareUsage);
        Assert.False(vm.AllowSelectedCommand.CanExecute(null));
        vm.ShareUsage = true;
        Assert.True(vm.AllowSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Dont_share_is_a_choice_too_so_it_isnt_asked_again()
    {
        var consent = Launch();

        await new DiagnosticsSharingViewModel(consent, _navigation) { ShareErrors = true }.DeclineCommand.ExecuteAsync(null);

        Assert.False(consent.ShareErrors || consent.ShareUsage);
        Assert.False(await Launch().ShowIfDueAsync(_navigation));
    }

    [Fact]
    public async Task A_build_without_a_reporting_key_never_asks_and_shares_nothing()
    {
        _preferences.Set(DiagnosticsConsent.ErrorsKey, bool.TrueString);
        _preferences.Set(DiagnosticsConsent.UsageKey, bool.TrueString);
        _preferences.Set(DiagnosticsConsent.NoticeKey, "1");
        var consent = Launch(available: false);

        Assert.False(await consent.ShowIfDueAsync(_navigation));
        Assert.False(consent.ShareErrors || consent.ShareUsage);
        Assert.False(SettingsAboutTests.NewViewModel(consent: consent).CanChooseSharing);
    }

    [Fact]
    public async Task A_choice_under_an_older_notice_shares_nothing_until_asked_again()
    {
        _preferences.Set(DiagnosticsConsent.ErrorsKey, bool.TrueString);
        _preferences.Set(DiagnosticsConsent.UsageKey, bool.TrueString);
        _preferences.Set(DiagnosticsConsent.NoticeKey, "0");
        var consent = Launch();

        Assert.False(consent.ShareErrors || consent.ShareUsage);
        Assert.True(await consent.ShowIfDueAsync(_navigation));
    }

    [Fact]
    public void Desktops_consent_in_Cores_settings_isnt_the_phones()
    {
        var appSettings = new AppSettings
        {
            DiagnosticsSharing = DiagnosticsSharingPreference.ErrorsAndUsage,
            DiagnosticsConsentVersion = 2,
        };
        var consent = Launch();
        var vm = SettingsAboutTests.NewViewModel(appSettings: appSettings, consent: consent);

        Assert.True(consent.NeedsChoice);
        Assert.False(vm.ShareErrorReports || vm.ShareUsage);
    }

    [Fact]
    public void Settings_switches_apply_at_once_one_at_a_time_and_tell_the_collectors()
    {
        var consent = Launch();
        consent.Choose(errors: true, usage: true);
        var vm = SettingsAboutTests.NewViewModel(consent: consent);
        var changes = 0;
        consent.Changed += (_, _) => changes++;

        vm.ShareUsage = false;

        Assert.Equal((true, false), (consent.ShareErrors, consent.ShareUsage));
        Assert.Equal("Error reports", vm.SharingSummary);
        Assert.Equal(1, changes);

        vm.ShareUsage = false; // a binding writing back its own value
        Assert.Equal(1, changes);

        vm.ShareErrorReports = false;
        Assert.Equal("Nothing shared", vm.SharingSummary);
        Assert.Equal((false, false), (Launch().ShareErrors, Launch().ShareUsage)); // remembered
    }

    [Fact]
    public void Answering_the_sheet_updates_Settings_open_behind_it()
    {
        var consent = Launch();
        var vm = SettingsAboutTests.NewViewModel(consent: consent);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        consent.Choose(errors: true, usage: false);

        Assert.Contains(nameof(SettingsViewModel.SharingSummary), raised);
        Assert.Equal("Error reports", vm.SharingSummary);
        Assert.True(vm.CanChooseSharing && vm.HasDiagnosticsCard);
    }

    [Fact]
    public async Task The_privacy_policy_opens_from_the_sheet()
    {
        var launcher = Substitute.For<ILauncherService>();

        await new DiagnosticsSharingViewModel(Launch(), _navigation, launcher).OpenPrivacyPolicyCommand.ExecuteAsync(null);

        await launcher.Received(1).OpenAsync(AppLinks.PrivacyPolicy);
    }
}
