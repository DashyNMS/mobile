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
    public void Service_summaries_describe_enablement_without_exposing_url_credentials()
    {
        var settings = new AppSettings();
        settings.Graylog.Enabled = true;
        settings.Graylog.Server = "https://example-user:synthetic-password@logs.example.net/private?token=synthetic";
        settings.Unimus.Enabled = true;
        settings.Unimus.Url = "";
        var vm = NewViewModel(appSettings: settings);
        Assert.Equal("On · logs.example.net", vm.GraylogStatusText);
        Assert.Equal("On · server address unavailable", vm.UnimusStatusText);
    }

    [Fact]
    public async Task Cancelling_sign_out_keeps_the_session_and_does_not_navigate()
    {
        var session = Substitute.For<ISessionService>();
        var navigation = new RecordingNavigation();
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>()).Returns((string?)null);
        var vm = NewViewModel(session: session, navigation: navigation, dialogs: dialogs);
        await vm.SignOutCommand.ExecuteAsync(null);
        session.DidNotReceive().SignOut(Arg.Any<bool>());
        Assert.Empty(navigation.Visits);
    }

    [Fact]
    public void Disconnected_summary_does_not_claim_a_secure_connection()
    {
        var vm = NewViewModel();
        Assert.Equal("Not signed in", vm.ServerHost);
        Assert.Equal("No active connection", vm.ServerSummary);
    }

    [Fact]
    public void Unknown_server_version_is_explicit_and_does_not_change_transport_warning()
    {
        var session = Substitute.For<ISessionService>();
        session.Connection.Returns(new LibreNmsConnection(new Uri("http://nms.example.net/"), "synthetic-token"));
        var vm = NewViewModel(session);
        Assert.Equal("LibreNMS unknown · not secure (http)", vm.ServerSummary);
    }

    [Theory]
    [InlineData(SettingsSection.Server)]
    [InlineData(SettingsSection.Appearance)]
    [InlineData(SettingsSection.Devices)]
    [InlineData(SettingsSection.AlertChecks)]
    [InlineData(SettingsSection.Notifications)]
    [InlineData(SettingsSection.LockScreen)]
    [InlineData(SettingsSection.Diagnostics)]
    public async Task Sections_keep_their_relative_route_and_section_identity(SettingsSection section)
    {
        var navigation = new RecordingNavigation();
        var vm = NewViewModel(navigation: navigation);
        await vm.OpenSectionCommand.ExecuteAsync(section);
        Assert.Equal(Routes.SettingsSection, navigation.Visits.Single().Route);
        Assert.Equal(section, navigation.Visits.Single().Parameters![Routes.SettingsSectionParameter]);
    }

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

    internal static SettingsViewModel NewViewModel(
        ISessionService? session = null, AppSettings? appSettings = null, SensorDefinitions? definitions = null,
        WhatsNew? whatsNew = null, RecordingNavigation? navigation = null, DiagnosticsConsent? consent = null,
        IDialogService? dialogs = null)
    {
        var settings = Fakes.Settings(appSettings ?? new AppSettings());
        session ??= Substitute.For<ISessionService>();
        var coordinator = new AlertWatchCoordinator(
            session, settings, null!, Substitute.For<IBackgroundAlertScheduler>(), new InMemoryWatchStore(), new NoAppBadge(), new DashyNMS.Mobile.Widgets.NoHomeWidgets(), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AlertWatchCoordinator>.Instance);
        return new SettingsViewModel(
            session, settings, dialogs ?? Substitute.For<IDialogService>(), navigation ?? new RecordingNavigation(), new RecordingNotifier(), coordinator,
            new NoAppBadge(), new InMemoryAppearance(), new DashyNMS.Mobile.Widgets.NoHomeWidgets(),
            whatsNew: whatsNew,
            diagnostics: new DiagnosticsLog(null, TimeProvider.System, "2.0.0 (412)", "Apple iPhone16,1 · iOS 26.1"),
            definitions: definitions, consent: consent);
    }

    [Fact]
    public void About_lists_the_app_Core_and_sensor_definitions_with_their_state()
    {
        var vm = NewViewModel();

        Assert.Equal(["App", "Core", "Sensor definitions"], vm.Versions.Select(v => v.Name));
        Assert.Equal(("2.0.0 (412)", "Installed"), (vm.Versions[0].Version, vm.Versions[0].State));
        Assert.NotEqual(ComponentVersions.Unavailable, vm.Versions[1].Version);
        Assert.Equal(("Unavailable", "Unavailable"), (vm.Versions[2].Version, vm.Versions[2].State));
    }

    [Fact]
    public async Task The_sensor_definitions_state_follows_the_store()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dashynms-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            using var definitions = new SensorDefinitions(folder, new HttpClient(new FakeGitHub()));
            var vm = NewViewModel(definitions: definitions);
            Assert.Equal("Disabled", vm.Versions[2].State);

            await definitions.Store.ConfigureAsync(new DesktopNMS.Core.Sensors.SensorDefinitionOptions { Enabled = true });
            Assert.Equal("Not downloaded", vm.Versions[2].State);

            await definitions.CheckAsync(indexOnly: true);
            Assert.Equal(("1.0.0", "Enabled"), (vm.Versions[2].Version, vm.Versions[2].State));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task The_versions_hold_no_server_device_token_or_source_address()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dashynms-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            // A custom source that can't be reached: Core's status then names it, and must not be copied.
            using var definitions = new SensorDefinitions(folder, new HttpClient(new FakeGitHub { Offline = true }));
            await definitions.Store.ConfigureAsync(new DesktopNMS.Core.Sensors.SensorDefinitionOptions
            {
                Enabled = true, UseCustomRepository = true, CustomRepository = "https://github.com/example-owner/private-definitions",
            });
            await definitions.CheckAsync(indexOnly: true);
            var session = Substitute.For<ISessionService>();
            session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "secret-token"));
            var appSettings = new AppSettings();
            appSettings.PinnedDevices.Add(new PinnedDevice { DeviceId = 1, DisplayName = "core-sw-01" });
            var vm = NewViewModel(session, appSettings, definitions);

            // What About shows, and the diagnostics header repeats.
            var shown = string.Join("\n", vm.Versions.Select(c => $"{c.Name}: {c.Version} ({c.State})"));
            Assert.Contains("App: 2.0.0 (412) (Installed)", shown);
            foreach (var secret in new[] { "nms.example.net", "secret-token", "core-sw-01", "example-owner", "private-definitions", folder })
            {
                Assert.DoesNotContain(secret, shown, StringComparison.OrdinalIgnoreCase);
            }

        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
