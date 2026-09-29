using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class SettingsAboutTests
{
    [Fact]
    public async Task The_about_card_opens_the_privacy_policy_and_support_pages_in_the_browser()
    {
        var settings = Fakes.Settings(new AppSettings());
        var session = Substitute.For<ISessionService>();
        var coordinator = new AlertWatchCoordinator(
            session, settings, null!, Substitute.For<IBackgroundAlertScheduler>(), new InMemoryWatchStore(), new NoAppBadge(), new DashyNMS.Mobile.Widgets.NoHomeWidgets(), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AlertWatchCoordinator>.Instance);
        var launcher = Substitute.For<ILauncherService>();
        var vm = new SettingsViewModel(
            session, settings, Substitute.For<IDialogService>(), new RecordingNavigation(), new RecordingNotifier(), coordinator,
            new NoAppBadge(), new InMemoryAppearance(), new DashyNMS.Mobile.Widgets.NoHomeWidgets(), launcher: launcher);

        await vm.OpenLinkCommand.ExecuteAsync(AppLinks.PrivacyPolicy);
        await vm.OpenLinkCommand.ExecuteAsync(AppLinks.Support);

        await launcher.Received(1).OpenAsync(new Uri("https://dashynms.pckp.net/mobile/privacy/"));
        await launcher.Received(1).OpenAsync(new Uri("https://dashynms.pckp.net/mobile/support/"));
    }
}
