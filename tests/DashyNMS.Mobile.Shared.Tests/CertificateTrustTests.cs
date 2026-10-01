using System.Net.Security;
using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// "Allow untrusted certificate" now trusts only a certificate you've looked
/// at (DashyNMS/desktop#189): the app shows it, in Core's words, and asks.
/// </summary>
public sealed class CertificateTrustTests
{
    private static readonly CertificateDetails Certificate = new(
        "nms.example.com",
        "AB:CD:EF:01",
        "CN=nms.example.com",
        "CN=Example Internal CA",
        DateTime.Today.AddDays(-30),
        DateTime.Today.AddYears(1),
        SslPolicyErrors.RemoteCertificateChainErrors,
        ReplacesTrustedCertificate: false);

    private static readonly ConnectionTestResult Untrusted =
        ConnectionTestResult.Failure("LibreNMS's certificate isn't trusted yet.", untrustedCertificate: Certificate);

    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly RecordingNavigation _navigation = new();

    private SignInViewModel NewSignIn() => new(
        _session, Fakes.Settings(), _navigation, Fakes.Secrets(), new RecordingNotifier(), new NotificationRouter(_session, _navigation), _dialogs);

    private void Answer(bool trust) =>
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), "Trust", "Cancel").Returns(trust);

    [Fact]
    public async Task Signing_in_shows_the_certificate_and_trusting_it_tries_again()
    {
        Answer(trust: true);
        _session.SignInAsync(default!, default!, default, default, default, default)
            .ReturnsForAnyArgs(Untrusted, ConnectionTestResult.Success(new SystemInfo()));
        var vm = NewSignIn();
        vm.ServerUrl = "https://nms.example.com";
        vm.ApiToken = "secret";
        vm.AllowUntrustedCertificate = true;

        await vm.SignInCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ConfirmAsync(
            "Trust LibreNMS's certificate?",
            Arg.Is<string>(m => m.Contains("AB:CD:EF:01") && m.Contains("CN=Example Internal CA")),
            "Trust",
            "Cancel");
        _session.Received(1).TrustCertificate(Certificate);
        await _session.ReceivedWithAnyArgs(2).SignInAsync(default!, default!, default, default, default, default);
        Assert.Equal(Routes.Main, _navigation.Visits.Single().Route);
    }

    [Fact]
    public async Task Declining_trusts_nothing_and_says_why()
    {
        Answer(trust: false);
        _session.SignInAsync(default!, default!, default, default, default, default).ReturnsForAnyArgs(Untrusted);
        var vm = NewSignIn();
        vm.ServerUrl = "https://nms.example.com";
        vm.ApiToken = "secret";
        vm.AllowUntrustedCertificate = true;

        await vm.SignInCommand.ExecuteAsync(null);

        _session.DidNotReceiveWithAnyArgs().TrustCertificate(default!);
        await _session.ReceivedWithAnyArgs(1).SignInAsync(default!, default!, default, default, default, default);
        Assert.Equal("LibreNMS's certificate isn't trusted yet.", vm.ErrorMessage);
        Assert.Empty(_navigation.Visits);
    }

    [Fact]
    public async Task Reopening_the_app_asks_too_then_carries_on()
    {
        Answer(trust: true);
        _session.TryRestoreAsync(default).ReturnsForAnyArgs(Untrusted, ConnectionTestResult.Success(new SystemInfo()));
        var vm = NewSignIn();

        await vm.AppearingCommand.ExecuteAsync(null);

        _session.Received(1).TrustCertificate(Certificate);
        Assert.Equal(Routes.Main, _navigation.Visits.Single().Route);
    }

    [Fact]
    public async Task Graylogs_test_asks_and_the_trusted_certificate_is_saved_with_the_form()
    {
        var appSettings = new AppSettings();
        var settings = Fakes.Settings(appSettings);
        var passwords = new SecureGraylogPasswordProtector(Fakes.Secrets());
        var tester = Substitute.For<IGraylogConnectionTester>();
        var setup = new GraylogSetup(Substitute.For<IGraylogApi>(), passwords, settings, Fakes.Secrets(), NullLogger<GraylogSetup>.Instance);
        var vm = new GraylogSettingsViewModel(settings, setup, passwords, tester, _dialogs, _navigation)
        {
            Enabled = true,
            Server = "graylog.example.com",
            Username = "admin",
            PasswordInput = "secret",
            AllowUntrustedCertificate = true,
        };
        Answer(trust: true);
        tester.TestAsync(Arg.Any<GraylogConnection>(), Arg.Any<CancellationToken>()).Returns(
            _ => throw new GraylogApiException("Graylog's certificate isn't trusted yet.") { UntrustedCertificate = Certificate },
            _ => 2);

        await vm.TestCommand.ExecuteAsync(null);

        Assert.True(vm.TestSucceeded);
        await tester.Received(1).TestAsync(Arg.Is<GraylogConnection>(c => c.TrustedCertificates.Contains("AB:CD:EF:01")), Arg.Any<CancellationToken>());

        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(["AB:CD:EF:01"], appSettings.Graylog.TrustedCertificates);
    }
}
