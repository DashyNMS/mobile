using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.SignIn;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class WebTokenSignInTests
{
    private static readonly Uri Root = new("https://nms.example.com/");
    private const string SanctumToken = "12|AbCdEfGhIjKlMnOpQrStUvWxYz0123456789abcd";

    private static WebTokenSignIn NewFlow() => new(Root, "DashyNMS · iPhone · 7 Oct 2026");

    private static Uri At(string path) => new(Root, path);

    [Fact]
    public void Starts_on_the_API_Tokens_page() =>
        Assert.Equal("https://nms.example.com/api-access", NewFlow().TokensPage.AbsoluteUri);

    [Fact]
    public void Starts_under_an_install_in_a_sub_path() =>
        Assert.Equal("https://example.com/librenms/api-access", new WebTokenSignIn(new Uri("https://example.com/librenms/"), "x").TokensPage.AbsoluteUri);

    [Fact]
    public void Leaves_the_login_page_to_the_user() =>
        Assert.Equal(WebSignInAction.Wait, NewFlow().Next(At("login"), "login").Action);

    [Fact]
    public void Submits_the_form_once_then_reads_the_token()
    {
        var flow = NewFlow();

        Assert.Equal(WebSignInAction.Submit, flow.Next(At("api-access"), "form").Action);

        var done = flow.Next(At("api-access"), "token:" + SanctumToken);
        Assert.Equal(WebSignInAction.Done, done.Action);
        Assert.Equal(SanctumToken, done.Token);
    }

    [Fact]
    public void Fails_when_the_form_comes_back_without_a_token()
    {
        var flow = NewFlow();
        flow.Next(At("api-access"), "form");

        Assert.Equal(WebSignInAction.Failed, flow.Next(At("api-access"), "form").Action);
    }

    [Fact]
    public void Goes_to_the_tokens_page_when_signing_in_lands_on_the_dashboard() =>
        Assert.Equal(WebSignInAction.GoToTokensPage, NewFlow().Next(At("overview"), "signed-in").Action);

    [Fact]
    public void Stops_sending_the_page_back_after_a_few_tries()
    {
        var flow = NewFlow();
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(WebSignInAction.GoToTokensPage, flow.Next(At("overview"), "signed-in").Action);
        }

        Assert.Equal(WebSignInAction.Wait, flow.Next(At("overview"), "signed-in").Action);
    }

    [Theory]
    [InlineData("signed-in")]
    [InlineData("other")]
    public void A_tokens_page_without_the_form_means_no_API_access(string probe) =>
        Assert.Equal(WebSignInAction.NotAllowed, NewFlow().Next(At("api-access"), probe).Action);

    [Fact]
    public void Two_factor_is_left_to_the_user() =>
        Assert.Equal(WebSignInAction.Wait, NewFlow().Next(At("2fa"), "other").Action);

    [Fact]
    public void Another_site_is_never_driven()
    {
        // A sign-on provider's page, even one that looks like the form.
        Assert.Equal(WebSignInAction.Wait, NewFlow().Next(new Uri("https://sso.example.com/api-access"), "form").Action);
        Assert.Equal(WebSignInAction.Wait, NewFlow().Next(new Uri("http://nms.example.com/api-access"), "form").Action);
        Assert.Equal(WebSignInAction.Wait, NewFlow().Next(new Uri("https://nms.example.com:8443/api-access"), "form").Action);
    }

    [Fact]
    public void Something_that_isnt_a_token_is_a_failure() =>
        Assert.Equal(WebSignInAction.Failed, NewFlow().Next(At("api-access"), "token:<b>oops</b>").Action);

    [Theory]
    [InlineData(SanctumToken, true)]
    [InlineData("3|librenms_AbCdEfGhIjKlMnOpQrStUvWxYz0123456789abcd1a2b3c4d", true)]
    [InlineData("0123456789abcdef0123456789abcdef", true)]
    [InlineData("0123456789abcdef", false)]
    [InlineData("12|short", false)]
    [InlineData("12|has space in it and is long enough", false)]
    [InlineData("", false)]
    public void Recognises_tokens(string text, bool expected) =>
        Assert.Equal(expected, WebTokenSignIn.LooksLikeToken(text));

    [Theory]
    [InlineData("\"form\"", "form")]
    [InlineData("form", "form")]
    [InlineData("\"token:12|abc\"", "token:12|abc")]
    [InlineData("null", "")]
    [InlineData(null, "")]
    public void Reads_script_results_from_either_platform(string? raw, string expected) =>
        Assert.Equal(expected, WebTokenSignIn.Unwrap(raw));

    [Fact]
    public void Names_the_token_after_the_phone_and_the_day() =>
        Assert.Equal("DashyNMS · Tom's iPhone · 7 Oct 2026", WebTokenSignIn.NameFor("Tom's iPhone", new DateTime(2026, 10, 7)));

    [Fact]
    public void Names_a_nameless_phone_and_keeps_within_LibreNMS_limit()
    {
        Assert.Equal("DashyNMS · phone · 7 Oct 2026", WebTokenSignIn.NameFor(" ", new DateTime(2026, 10, 7)));
        Assert.Equal(255, WebTokenSignIn.NameFor(new string('x', 400), new DateTime(2026, 10, 7)).Length);
    }

    [Fact]
    public void The_submit_script_quotes_the_name_safely()
    {
        var script = new WebTokenSignIn(Root, "Tom's \"phone\"</script>").SubmitScript;

        Assert.Contains("d.value=\"Tom\\u0027s \\u0022phone\\u0022\\u003C/script\\u003E\"", script);
    }
}

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
