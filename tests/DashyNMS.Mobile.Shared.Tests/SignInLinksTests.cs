using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.SignIn;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

public sealed class SignInLinksTests
{
    private const string Token = "0123456789abcdef0123456789ABCDEF";
    private const string Fingerprint = "AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99";

    private readonly AppSettings _appSettings = new();
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly RecordingNavigation _navigation = new();
    private readonly SignInLinks _links;

    public SignInLinksTests()
    {
        _links = new SignInLinks(_session, Fakes.Settings(_appSettings), Fakes.Secrets(), _dialogs, _navigation,
            new RecordingNotifier(), new NotificationRouter(_session, _navigation), NullLogger<SignInLinks>.Instance);
        _session.SignInAsync(default!, default!, default, default, default, default)
            .ReturnsForAnyArgs(ConnectionTestResult.Success(new SystemInfo()));
    }

    private static string Link(string server = "https://nms.example.net/", Uri? backup = null, params string[] certs) =>
        SignInLink.Create(new Uri(server), Token, backup, certs).ToString();

    private void Answer(bool yes) =>
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), "Sign in", "Cancel").Returns(yes);

    [Fact]
    public async Task Asks_first_naming_the_server_and_backup_then_signs_in_keeping_the_token()
    {
        Answer(true);

        var signedIn = await _links.OpenAsync(Link(backup: new Uri("https://nms-backup.example.net/")));

        Assert.True(signedIn);
        await _dialogs.Received(1).ConfirmAsync(
            "Sign in to nms.example.net?",
            Arg.Is<string>(m => m.Contains("https://nms-backup.example.net/", StringComparison.Ordinal)),
            "Sign in", "Cancel");
        await _session.Received(1).SignInAsync("https://nms.example.net/", Token, false, true, Arg.Any<CancellationToken>(), "https://nms-backup.example.net/");
        Assert.Equal(Routes.Main, _navigation.Visits.Single().Route);
    }

    [Fact]
    public async Task Turned_down_it_never_touches_the_session()
    {
        Answer(false);
        _session.IsConnected.Returns(true);
        _session.Connection.Returns(new LibreNmsConnection(new Uri("https://old.example.net/"), Token));

        Assert.False(await _links.OpenAsync(Link()));

        await _dialogs.Received(1).ConfirmAsync(Arg.Any<string>(),
            Arg.Is<string>(m => m.EndsWith("This replaces your sign-in to old.example.net.", StringComparison.Ordinal)), "Sign in", "Cancel");
        await _session.DidNotReceiveWithAnyArgs().SignInAsync(default!, default!, default, default, default, default);
        _session.DidNotReceiveWithAnyArgs().SignOut(default);
        Assert.Empty(_navigation.Visits);
    }

    [Fact]
    public async Task Only_the_links_certificates_are_trusted_and_forgotten_if_it_fails()
    {
        Answer(true);
        _session.SignInAsync(default!, default!, default, default, default, default)
            .ReturnsForAnyArgs(ConnectionTestResult.Failure("LibreNMS rejected the API token.", isAuthenticationFailure: true));

        Assert.False(await _links.OpenAsync(Link(certs: Fingerprint)));

        await _session.Received(1).SignInAsync(Arg.Any<string>(), Token, true, true, Arg.Any<CancellationToken>(), null);
        Assert.Empty(_appSettings.TrustedCertificates);
        await _dialogs.Received(1).AlertAsync("Can't sign in", "LibreNMS rejected the API token.");
    }

    [Fact]
    public async Task A_link_that_cant_be_read_says_why_and_asks_nothing()
    {
        Assert.False(await _links.OpenAsync("dashynms://sign-in?v=2&server=https%3A%2F%2Fnms.example.net%2F&token=" + Token));

        await _dialogs.Received(1).AlertAsync("Can't sign in", "This sign-in link is from a newer DashyNMS. Update DashyNMS and scan it again.");
        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Plain_http_is_warned_about_before_signing_in()
    {
        Answer(true);
        _dialogs.ConfirmAsync("Not a secure connection", Arg.Any<string>(), "Continue", "Cancel").Returns(false);

        Assert.False(await _links.OpenAsync(Link("http://nms.example.net/")));

        await _session.DidNotReceiveWithAnyArgs().SignInAsync(default!, default!, default, default, default, default);
    }

    [Fact]
    public void Only_sign_in_links_are_taken_from_the_widgets_and_notifications()
    {
        Assert.True(SignInLinks.IsSignInLink("dashynms://sign-in?v=1"));
        Assert.False(SignInLinks.IsSignInLink("dashynms://alerts"));
        Assert.False(SignInLinks.IsSignInLink(null));
    }
}
