using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

/// <summary>Batch 5: the in-repo security fixes (#3, #8, #9, #10).</summary>
public sealed class SecurityTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly RecordingNavigation _navigation = new();

    public SecurityTests() => _settings = Fakes.Settings(_appSettings);

    private SignInViewModel NewSignIn() => new(
        _session, _settings, _navigation, Fakes.Secrets(), new RecordingNotifier(), new NotificationRouter(_session, _navigation), _dialogs);

    [Theory]
    [InlineData("http://nms.example.com", true)]
    [InlineData("  HTTP://10.0.0.5 ", true)]
    [InlineData("https://nms.example.com", false)]
    [InlineData("nms.example.com", false)] // a bare name gets https://
    [InlineData("", false)]
    public void Only_an_explicit_http_address_is_cleartext(string address, bool cleartext) =>
        Assert.Equal(cleartext, SignInViewModel.IsCleartext(address));

    [Fact]
    public async Task Signing_in_over_http_asks_first_and_cancelling_doesnt_sign_in()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);
        var vm = NewSignIn();
        vm.ServerUrl = "http://nms.example.com";
        vm.ApiToken = "secret";

        await vm.SignInCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ConfirmAsync("Not a secure connection", Arg.Any<string>(), "Continue", "Cancel");
        await _session.DidNotReceiveWithAnyArgs().SignInAsync(default!, default!, default, default, default, default);
        Assert.Empty(_navigation.Visits);
    }

    [Fact]
    public async Task An_http_backup_address_asks_too()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        _session.SignInAsync(default!, default!, default, default, default, default)
            .ReturnsForAnyArgs(ConnectionTestResult.Success(new SystemInfo()));
        var vm = NewSignIn();
        vm.ServerUrl = "https://nms.example.com";
        vm.BackupAddress = "http://10.0.0.5";
        vm.ApiToken = "secret";

        await vm.SignInCommand.ExecuteAsync(null);

        await _dialogs.ReceivedWithAnyArgs(1).ConfirmAsync(default!, default!, default!, default!);
        await _session.ReceivedWithAnyArgs(1).SignInAsync(default!, default!, default, default, default, default);
    }

    [Fact]
    public async Task Signing_in_over_https_doesnt_ask()
    {
        _session.SignInAsync(default!, default!, default, default, default, default)
            .ReturnsForAnyArgs(ConnectionTestResult.Success(new SystemInfo()));
        var vm = NewSignIn();
        vm.ServerUrl = "nms.example.com";
        vm.ApiToken = "secret";

        await vm.SignInCommand.ExecuteAsync(null);

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
    }

    private SettingsViewModel NewSettings(DeviceBookmarks bookmarks, IShareService share, GraylogSetup? graylog = null) => new(
        _session, _settings, _dialogs, _navigation, new RecordingNotifier(), null!, new NoAppBadge(), new InMemoryAppearance(),
        new DashyNMS.Mobile.Widgets.NoHomeWidgets(), bookmarks, share, graylog);

    [Fact]
    public async Task Sign_out_forgets_the_token_devices_and_exports_but_keeps_the_address()
    {
        _appSettings.ServerUrl = "https://nms.example.com";
        var bookmarks = new DeviceBookmarks(_settings, TimeProvider.System);
        bookmarks.SetPinned(1, "core-sw", pinned: true);
        bookmarks.RecordViewed(2, "edge-rtr");
        var share = Substitute.For<IShareService>();
        _dialogs.ChooseAsync(default!, default!).ReturnsForAnyArgs(SettingsViewModel.SignOutChoice);

        await NewSettings(bookmarks, share).SignOutCommand.ExecuteAsync(null);

        _session.Received(1).SignOut(forgetToken: true);
        Assert.Empty(_appSettings.PinnedDevices);
        Assert.Empty(_appSettings.RecentlyViewedDevices);
        share.Received(1).ClearExports();
        Assert.Equal("https://nms.example.com", _appSettings.ServerUrl); // fills in the sign-in form
        Assert.Equal(Routes.SignIn, _navigation.Visits.Single().Route);
    }

    [Fact]
    public async Task Forget_everything_also_forgets_the_servers_and_the_Graylog_password()
    {
        _appSettings.ServerUrl = "https://nms.example.com";
        _appSettings.BackupServerAddress = "https://10.0.0.5";
        _appSettings.Graylog = new GraylogSettings { Enabled = true, Server = "graylog.example.com", Username = "admin" };
        var secrets = Fakes.Secrets();
        var passwords = new SecureGraylogPasswordProtector(secrets);
        passwords.Save("secret");
        var graylog = new GraylogSetup(Substitute.For<IGraylogApi>(), passwords, _settings, secrets, NullLogger<GraylogSetup>.Instance);
        _dialogs.ChooseAsync(default!, default!).ReturnsForAnyArgs(SettingsViewModel.ForgetEverythingChoice);

        await NewSettings(new DeviceBookmarks(_settings, TimeProvider.System), Substitute.For<IShareService>(), graylog)
            .SignOutCommand.ExecuteAsync(null);

        Assert.Null(_appSettings.ServerUrl);
        Assert.Null(_appSettings.BackupServerAddress);
        Assert.False(_appSettings.Graylog.Enabled);
        Assert.Null(passwords.Load());
    }

    [Fact]
    public async Task Cancelling_sign_out_changes_nothing()
    {
        var bookmarks = new DeviceBookmarks(_settings, TimeProvider.System);
        bookmarks.SetPinned(1, "core-sw", pinned: true);
        _dialogs.ChooseAsync(default!, default!).ReturnsForAnyArgs((string?)null);

        await NewSettings(bookmarks, Substitute.For<IShareService>()).SignOutCommand.ExecuteAsync(null);

        _session.DidNotReceiveWithAnyArgs().SignOut(default);
        Assert.Single(_appSettings.PinnedDevices);
    }

    [Theory]
    [InlineData(AlertSeverity.Critical, true, "Critical alert - unlock to see it")]
    [InlineData(AlertSeverity.Warning, true, "Warning - unlock to see it")]
    [InlineData(AlertSeverity.Critical, false, "An alert has changed - unlock to see it")]
    public void The_lock_screen_shows_how_serious_but_not_which_device_or_rule(AlertSeverity severity, bool problem, string expected)
    {
        var notification = new AlertNotification("alert-7", "Critical: core-fw-01", "BGP session down", "note", severity, problem, DeviceId: 3, AlertId: 7);

        Assert.Equal(expected, notification.LockScreenText);
        Assert.DoesNotContain("core-fw-01", notification.LockScreenText);
    }

    [Fact]
    public void A_summary_on_the_lock_screen_is_generic_too()
    {
        var summary = new AlertNotification(AlertNotification.SummaryTag, "5 new alerts", "core-fw-01, edge-rtr…", null, AlertSeverity.Critical, true, null);

        Assert.Equal("New alerts - unlock to see them", summary.LockScreenText);
    }

    [Fact]
    public void Settings_offers_hiding_notification_details_where_the_platform_allows()
    {
        var privacy = Substitute.For<INotificationPrivacy>();
        privacy.CanHideLockScreenDetails.Returns(true);
        privacy.HideLockScreenDetails.Returns(true);
        var vm = new SettingsViewModel(
            _session, _settings, _dialogs, _navigation, new RecordingNotifier(), null!, new NoAppBadge(), new InMemoryAppearance(),
            new DashyNMS.Mobile.Widgets.NoHomeWidgets(), privacy: privacy);

        Assert.True(vm.CanHideNotificationDetails);
        vm.HideNotificationDetails = false;

        privacy.Received(1).HideLockScreenDetails = false;
    }
}
