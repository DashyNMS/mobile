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

    internal static SettingsViewModel NewViewModel(
        ISessionService? session = null, AppSettings? appSettings = null, SensorDefinitions? definitions = null, IClipboardText? clipboard = null,
        WhatsNew? whatsNew = null, RecordingNavigation? navigation = null)
    {
        var settings = Fakes.Settings(appSettings ?? new AppSettings());
        session ??= Substitute.For<ISessionService>();
        var coordinator = new AlertWatchCoordinator(
            session, settings, null!, Substitute.For<IBackgroundAlertScheduler>(), new InMemoryWatchStore(), new NoAppBadge(), new DashyNMS.Mobile.Widgets.NoHomeWidgets(), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AlertWatchCoordinator>.Instance);
        return new SettingsViewModel(
            session, settings, Substitute.For<IDialogService>(), navigation ?? new RecordingNavigation(), new RecordingNotifier(), coordinator,
            new NoAppBadge(), new InMemoryAppearance(), new DashyNMS.Mobile.Widgets.NoHomeWidgets(),
            whatsNew: whatsNew,
            diagnostics: new DiagnosticsLog(null, TimeProvider.System, "2.0.0 (412)", "Apple iPhone16,1 · iOS 26.1"),
            definitions: definitions, clipboard: clipboard)
        {
            CopiedFor = TimeSpan.Zero,
        };
    }

    [Fact]
    public void About_lists_the_app_Core_sensor_definitions_and_platform_with_their_state()
    {
        var vm = NewViewModel();

        Assert.Equal(["App", "Core", "Sensor definitions", "Platform"], vm.Versions.Select(v => v.Name));
        Assert.Equal(("2.0.0 (412)", "Installed"), (vm.Versions[0].Version, vm.Versions[0].State));
        Assert.NotEqual(ComponentVersions.Unavailable, vm.Versions[1].Version);
        Assert.Equal(("Unavailable", "Unavailable"), (vm.Versions[2].Version, vm.Versions[2].State));
        Assert.Equal("iOS 26.1", vm.Versions[3].Version);
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
    public async Task Copied_version_details_hold_no_server_device_token_or_source_address()
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
            var clipboard = Substitute.For<IClipboardText>();
            string? copied = null;
            clipboard.SetTextAsync(Arg.Do<string>(t => copied = t)).Returns(Task.CompletedTask);
            var vm = NewViewModel(session, appSettings, definitions, clipboard);

            await vm.CopyVersionDetailsCommand.ExecuteAsync(null);

            Assert.NotNull(copied);
            Assert.Contains("App: 2.0.0 (412) (Installed)", copied);
            Assert.Contains("Core: ", copied);
            Assert.Contains("Sensor definitions: ", copied);
            Assert.Contains("OS: ", copied);
            Assert.Contains("Architecture: ", copied);
            foreach (var secret in new[] { "nms.example.net", "secret-token", "core-sw-01", "example-owner", "private-definitions", folder })
            {
                Assert.DoesNotContain(secret, copied, StringComparison.OrdinalIgnoreCase);
            }

            Assert.Equal("Copy version details", vm.CopyVersionsText);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
