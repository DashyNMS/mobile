using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.SignIn;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class SignInWithLibreNmsTests
{
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly RecordingNavigation _navigation = new();
    private readonly IWebSignIn _web = Substitute.For<IWebSignIn>();
    private readonly ICertificateProbe _probe = Substitute.For<ICertificateProbe>();

    public SignInWithLibreNmsTests()
    {
        _web.DeviceName.Returns("Tom's iPhone");
        _probe.ProbeAsync(default!, default, default!, default).ReturnsForAnyArgs(new ProbeResult(true));
    }

    private SignInViewModel NewViewModel(string server = "nms.example.com", bool withWeb = true)
    {
        var vm = new SignInViewModel(
            _session, Fakes.Settings(), _navigation, Fakes.Secrets(), new RecordingNotifier(), new NotificationRouter(_session, _navigation),
            webSignIn: withWeb ? _web : null, probe: _probe);
        vm.ServerUrl = server;
        return vm;
    }

    [Fact]
    public void Offers_LibreNMS_first()
    {
        var vm = NewViewModel();

        Assert.True(vm.ShowsWebSignIn);
        Assert.False(vm.ShowsTokenEntry);
    }

    [Fact]
    public void Without_a_web_view_asks_for_a_token()
    {
        var vm = NewViewModel(withWeb: false);

        Assert.False(vm.ShowsWebSignIn);
        Assert.True(vm.ShowsTokenEntry);
        Assert.False(vm.ShowsLibreNmsLink);
    }

    [Fact]
    public void Can_switch_to_a_token_and_back()
    {
        var vm = NewViewModel();

        vm.UseTokenCommand.Execute(null);
        Assert.True(vm.ShowsTokenEntry);
        Assert.True(vm.ShowsLibreNmsLink);

        vm.UseLibreNmsCommand.Execute(null);
        Assert.True(vm.ShowsWebSignIn);
    }

    [Fact]
    public async Task Signs_in_with_the_token_it_created_named_after_the_phone()
    {
        WebSignInRequest? asked = null;
        _web.SignInAsync(default!).ReturnsForAnyArgs(call =>
        {
            asked = call.Arg<WebSignInRequest>();
            return new WebSignInResult(WebSignInOutcome.Token, "12|AbCdEfGhIjKlMnOpQrStUvWxYz0123456789abcd");
        });
        _session.SignInAsync(default!, default!, default, default, default, default)
            .ReturnsForAnyArgs(ConnectionTestResult.Success(new SystemInfo()));
        var vm = NewViewModel();

        await vm.SignInWithLibreNmsCommand.ExecuteAsync(null);

        Assert.StartsWith("DashyNMS · Tom's iPhone · ", asked!.Flow.TokenName);
        Assert.Equal("https://nms.example.com/api-access", asked.Flow.TokensPage.AbsoluteUri);
        await _session.Received().SignInAsync("nms.example.com", "12|AbCdEfGhIjKlMnOpQrStUvWxYz0123456789abcd", Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>(), Arg.Any<string?>());
        Assert.Contains(_navigation.Visits, v => v.Route == Routes.Main);
        Assert.Empty(vm.ApiToken);
    }

    [Fact]
    public async Task Falls_back_to_a_token_when_the_account_cant_create_one()
    {
        _web.SignInAsync(default!).ReturnsForAnyArgs(new WebSignInResult(WebSignInOutcome.NotAllowed));
        var vm = NewViewModel();

        await vm.SignInWithLibreNmsCommand.ExecuteAsync(null);

        Assert.True(vm.ShowsTokenEntry);
        Assert.Contains("can't create API tokens", vm.ErrorMessage);
        Assert.Empty(_navigation.Visits);
    }

    [Fact]
    public async Task Cancelling_changes_nothing()
    {
        _web.SignInAsync(default!).ReturnsForAnyArgs(WebSignInResult.Cancelled);
        var vm = NewViewModel();

        await vm.SignInWithLibreNmsCommand.ExecuteAsync(null);

        Assert.True(vm.ShowsWebSignIn);
        Assert.False(vm.HasError);
        await _session.DidNotReceiveWithAnyArgs().SignInAsync(default!, default!, default, default, default, default);
    }

    [Fact]
    public async Task Never_over_plain_http()
    {
        var vm = NewViewModel("http://nms.example.com");

        await vm.SignInWithLibreNmsCommand.ExecuteAsync(null);

        Assert.True(vm.ShowsTokenEntry);
        Assert.Contains("https://", vm.ErrorMessage);
        await _web.DidNotReceiveWithAnyArgs().SignInAsync(default!);
    }

    [Fact]
    public async Task Doesnt_open_the_page_when_the_server_cant_be_reached()
    {
        _probe.ProbeAsync(default!, default, default!, default).ReturnsForAnyArgs(new ProbeResult(false, ErrorMessage: "Couldn't reach nms.example.com."));
        var vm = NewViewModel();

        await vm.SignInWithLibreNmsCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't reach nms.example.com.", vm.ErrorMessage);
        await _web.DidNotReceiveWithAnyArgs().SignInAsync(default!);
    }

    [Fact]
    public async Task An_untrusted_certificate_isnt_used_without_saying_so()
    {
        // No dialog service here, so the prompt can only be declined.
        var certificate = new DesktopNMS.Core.Security.CertificateDetails(
            "nms.example.com", "AB:CD", "CN=nms", "CN=nms", DateTime.Today, DateTime.Today.AddYears(1), System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors, false);
        _probe.ProbeAsync(default!, default, default!, default).ReturnsForAnyArgs(new ProbeResult(false, certificate));
        var vm = NewViewModel();

        await vm.SignInWithLibreNmsCommand.ExecuteAsync(null);

        Assert.Contains("certificate wasn't trusted", vm.ErrorMessage);
        await _web.DidNotReceiveWithAnyArgs().SignInAsync(default!);
        _session.DidNotReceiveWithAnyArgs().TrustCertificate(default!);
    }
}

public sealed class EasierTokenTests
{
    private const string Token = "12|AbCdEfGhIjKlMnOpQrStUvWxYz0123456789abcd";

    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly RecordingNavigation _navigation = new();
    private readonly ILauncherService _launcher = Substitute.For<ILauncherService>();
    private readonly IClipboardText _clipboard = Substitute.For<IClipboardText>();

    private SignInViewModel NewViewModel(string server = "nms.example.com") => new(
        _session, Fakes.Settings(), _navigation, Fakes.Secrets(), new RecordingNotifier(), new NotificationRouter(_session, _navigation),
        launcher: _launcher, clipboard: _clipboard)
    {
        ServerUrl = server,
    };

    [Fact]
    public void The_hint_is_Cores_as_desktop_says_it() =>
        Assert.Equal(SignInHelp.TokenHint, NewViewModel().TokenHint);

    [Fact]
    public async Task An_empty_token_says_where_to_get_one()
    {
        var vm = NewViewModel();

        await vm.SignInCommand.ExecuteAsync(null);

        Assert.Equal(SignInHelp.MissingToken, vm.ErrorMessage);
        await _session.DidNotReceiveWithAnyArgs().SignInAsync(default!, default!, default, default, default, default);
    }

    [Fact]
    public async Task Opens_the_servers_API_Tokens_page()
    {
        var vm = NewViewModel("https://example.com:8443/librenms");

        Assert.True(vm.CanOpenTokensPage);
        await vm.OpenTokensPageCommand.ExecuteAsync(null);

        await _launcher.Received(1).OpenAsync(new Uri("https://example.com:8443/librenms/api-access"));
    }

    [Fact]
    public void No_tokens_page_until_there_is_an_address()
    {
        var vm = NewViewModel(string.Empty);

        Assert.False(vm.CanOpenTokensPage);
        Assert.False(vm.OpenTokensPageCommand.CanExecute(null));

        vm.ServerUrl = "nms.example.com";
        Assert.True(vm.CanOpenTokensPage);
    }

    [Fact]
    public void Offers_copied_text_without_reading_it()
    {
        _clipboard.HasText.Returns(true);
        var vm = NewViewModel();

        vm.CheckClipboard();

        Assert.True(vm.CanPasteToken);
        _ = _clipboard.DidNotReceive().GetTextAsync();
    }

    [Theory]
    [InlineData(Token)]
    [InlineData("  Bearer " + Token + "\n")]
    public async Task Uses_a_copied_token(string copied)
    {
        _clipboard.GetTextAsync().Returns(copied);
        var vm = NewViewModel();

        await vm.PasteTokenCommand.ExecuteAsync(null);

        Assert.Equal(Token, vm.ApiToken);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task Copied_text_that_isnt_a_token_isnt_used()
    {
        _clipboard.GetTextAsync().Returns("https://nms.example.com/api-access");
        var vm = NewViewModel();

        await vm.PasteTokenCommand.ExecuteAsync(null);

        Assert.Empty(vm.ApiToken);
        Assert.Contains("isn't an API token", vm.ErrorMessage);
    }
}
