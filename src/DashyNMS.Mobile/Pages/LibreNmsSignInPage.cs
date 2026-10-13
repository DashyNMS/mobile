using DesktopNMS.Core.Security;
using DesktopNMS.Core.SignIn;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// "Sign in with LibreNMS" (#162): the server's own website in a web view.
/// The user signs in there however their server signs people in; the page
/// then drives the API Tokens page with <see cref="WebTokenSignIn"/> and hands
/// back the token it creates. Shown modally by <see cref="Adapters.WebSignIn"/>.
/// </summary>
/// <remarks>
/// While the app is working (opening the page, creating the token) the web
/// view is hidden behind a busy card, so the new token never shows on screen.
/// A self-signed server's certificate is accepted only when its fingerprint
/// is one the user already trusted for the API (#189). The platform hooks at
/// the bottom also report each page that starts loading and whether the token
/// form's POST went out, which MAUI's WebView doesn't (#178).
/// </remarks>
public sealed class LibreNmsSignInPage : ContentPage
{
    /// <summary>
    /// How long the tokens page is left before the form is submitted, for
    /// LibreNMS's own scripts to finish - its timezone check can reload the
    /// page straight after signing in (desktop#293, #178).
    /// </summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(1);

    private readonly WebSignInRequest _request;
    private readonly ILogger _logger;
    private readonly TaskCompletionSource<WebSignInResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly WebView _web;
    private readonly Controls.BusyCard _busy;

    // One page is handled at a time; one that finishes loading meanwhile is
    // picked up straight after, not dropped.
    private bool _handling;
    private bool _recheck;

    // Counts the pages that start loading: a page a newer load has overtaken
    // is never acted on. Bumped by the platform hooks.
    private int _navigation;

    // Whether the token form's POST went out after the last submit. Set from
    // Android's network thread, hence volatile.
    private bool _awaitingPost;
    private volatile bool _postSent;

    public LibreNmsSignInPage(WebSignInRequest request, ILogger logger)
    {
        _request = request;
        _logger = logger;
        Title = "Sign in with LibreNMS";

        var cancel = new Button { Text = "Cancel", Style = (Style)Application.Current!.Resources["LinkButton"], HorizontalOptions = LayoutOptions.Start };
        cancel.Clicked += (_, _) => Finish(WebSignInResult.Cancelled);

        var title = new Label { Text = "Sign in with LibreNMS", FontFamily = "BodySemibold", FontSize = 16, HorizontalTextAlignment = TextAlignment.Center };
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);

        // The server's name under the title, so it's clear whose page this is.
        var heading = new VerticalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                title,
                new Label { Text = request.Flow.TokensPage.Host, Style = (Style)Application.Current.Resources["Faint"], HorizontalTextAlignment = TextAlignment.Center },
            },
        };

        var header = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
            Padding = new Thickness(12, 8),
            MinimumHeightRequest = 52,
        };
        header.Add(cancel, 0);
        header.Add(heading, 1);

        // The tokens page is opened once the platform hooks are in (HookWebView),
        // so the first page is counted too.
        _web = new WebView { Opacity = 0 };
        _web.Navigated += OnNavigated;
        _web.HandlerChanged += (_, _) => HookWebView();

        _busy = new Controls.BusyCard { Text = "Opening LibreNMS…", VerticalOptions = LayoutOptions.Start, Margin = new Thickness(16, 24, 16, 0) };

        var body = new Grid();
        body.Add(_web);
        body.Add(_busy);

        var page = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
        page.Add(header, 0, 0);
        page.Add(body, 0, 1);
        Content = page;
    }

    public Task<WebSignInResult> Result => _result.Task;

    /// <summary>Android's back button cancels, as the Cancel button does.</summary>
    protected override bool OnBackButtonPressed()
    {
        Finish(WebSignInResult.Cancelled);
        return true;
    }

    private async void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (_result.Task.IsCompleted)
        {
            return;
        }

        if (e.Result is WebNavigationResult.Failure or WebNavigationResult.Timeout)
        {
            _logger.LogInformation("Web sign-in: {Page} didn't load ({Result})", Describe(e.Url), e.Result);
            if (!_handling)
            {
                Finish(new WebSignInResult(WebSignInOutcome.Failed));
            }

            return;
        }

        if (e.Result != WebNavigationResult.Success)
        {
            // Cancelled - a redirect replaced it; the next page reports in.
            return;
        }

        _logger.LogDebug("Web sign-in: loaded {Page} (page {Navigation})", Describe(e.Url), _navigation);
        _loadedUrl = e.Url;
        if (_handling)
        {
            _recheck = true;
            return;
        }

        _handling = true;
        try
        {
            do
            {
                _recheck = false;
                await HandlePageAsync(_navigation, _loadedUrl);
            }
            while (_recheck && !_result.Task.IsCompleted);
        }
        finally
        {
            _handling = false;
        }
    }

    // The address of the page that last finished loading, for a recheck.
    private string? _loadedUrl;

    private async Task HandlePageAsync(int navigation, string? url)
    {
        Uri.TryCreate(url, UriKind.Absolute, out var location);

        // Scripts run on the server's own pages only, never a sign-on provider's.
        string? probe = null;
        if (_request.Flow.IsOnServer(location))
        {
            probe = await RunScriptAsync(WebTokenSignIn.ProbeScript);
        }

        // Another page is already on its way - it reports in when it's loaded.
        if (navigation != _navigation)
        {
            _logger.LogDebug("Web sign-in: the page changed while it was being checked");
            return;
        }

        // A submit with no POST before this page: a reload landed on top of
        // it (desktop#293), so the form gets submitted again.
        if (_awaitingPost)
        {
            _awaitingPost = false;
            if (!_postSent)
            {
                _logger.LogInformation("Web sign-in: the form's submit never went out");
                _request.Flow.SubmitNotSent();
            }
        }

        var step = _request.Flow.Next(location, probe);
        _logger.LogDebug("Web sign-in: page is {Answer}, next step {Step}", probe is null ? "(not run - not the server)" : Classify(WebTokenSignIn.Unwrap(probe)), step.Action);

        switch (step.Action)
        {
            case WebSignInAction.Wait:
                ShowPage();
                break;

            case WebSignInAction.GoToTokensPage:
                Working("Opening API Tokens…");
                _web.Source = new UrlWebViewSource { Url = _request.Flow.TokensPage.AbsoluteUri };
                break;

            case WebSignInAction.Submit:
                await SubmitAsync(navigation);
                break;

            case WebSignInAction.Done:
                Finish(new WebSignInResult(WebSignInOutcome.Token, step.Token));
                break;

            case WebSignInAction.NotAllowed:
                Finish(new WebSignInResult(WebSignInOutcome.NotAllowed));
                break;

            case WebSignInAction.Failed:
                Finish(new WebSignInResult(WebSignInOutcome.Failed));
                break;
        }
    }

    /// <summary>
    /// Submits the token form once the page has settled. If LibreNMS reloads
    /// it first, or the submit doesn't run, it's left for the next load to
    /// submit again (desktop#293, #178).
    /// </summary>
    private async Task SubmitAsync(int navigation)
    {
        Working("Creating a token for DashyNMS…");
        await Task.Delay(SettleTime);

        if (_result.Task.IsCompleted)
        {
            return;
        }

        if (navigation != _navigation)
        {
            _logger.LogInformation("Web sign-in: the page reloaded before the form was submitted - submitting on the new page");
            _request.Flow.SubmitSkipped();
            return;
        }

        _postSent = false;
        _awaitingPost = true;

        var submitted = WebTokenSignIn.Unwrap(await RunScriptAsync(_request.Flow.SubmitScript));
        _logger.Log(submitted == "submitted" ? LogLevel.Debug : LogLevel.Information, "Web sign-in: submit script answered {Answer}", Classify(submitted));

        if (submitted != "submitted")
        {
            _awaitingPost = false;
            _request.Flow.SubmitNotSent();

            // Nothing else is loading, so no page will come to try again on.
            if (navigation == _navigation)
            {
                Finish(new WebSignInResult(WebSignInOutcome.Failed));
            }
        }
    }

    /// <summary>A page started loading - from the platform hooks, on the main thread.</summary>
    private void PageStarted(string? url)
    {
        _navigation++;
        _logger.LogDebug("Web sign-in: loading {Page} (page {Navigation})", Describe(url), _navigation);
    }

    /// <summary>
    /// A POST is on its way - from the platform hooks, on any thread. Only
    /// the token form's, to the tokens page on the server, counts.
    /// </summary>
    private void PostStarting(string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && _request.Flow.IsOnServer(uri)
            && string.Equals(uri.AbsolutePath.TrimEnd('/'), _request.Flow.TokensPage.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            _postSent = true;
        }
    }

    /// <summary>A page's address for the log: scheme, host, port and path, never the query (it can carry codes).</summary>
    private static string Describe(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Path) : "(none)";

    /// <summary>
    /// What a page script answered, for the log - only ever one of a fixed set
    /// of words, never text from the page, so a token can't reach the log.
    /// </summary>
    private static string Classify(string? answer) => answer switch
    {
        null or "" => "nothing",
        _ when answer.StartsWith("token:", StringComparison.Ordinal) => "token",
        "form" or "login" or "signed-in" or "other" or "submitted" or "no-form" => answer,
        _ => "unrecognised",
    };

    private async Task<string?> RunScriptAsync(string script)
    {
        try
        {
            return await _web.EvaluateJavaScriptAsync(script);
        }
        catch (Exception)
        {
            // A page that went away mid-script: its replacement reports in.
            return null;
        }
    }

    private void ShowPage()
    {
        _busy.IsVisible = false;
        _web.Opacity = 1;
    }

    private void Working(string text)
    {
        _busy.Text = text;
        _busy.IsVisible = true;
        _web.Opacity = 0;
    }

    private void Finish(WebSignInResult result)
    {
        if (_result.TrySetResult(result))
        {
            _logger.LogInformation("Web sign-in: finished - {Outcome}", result.Outcome);
            Working(result.Outcome == WebSignInOutcome.Token ? "Signing in…" : "Closing…");
        }
    }

    /// <summary>
    /// Puts the platform hooks in, then opens the tokens page. The hooks tell
    /// the page when a page starts loading and when a POST goes out - which
    /// MAUI's WebView doesn't (#178) - and let the web view accept the
    /// server's certificate when the user has trusted that exact one (#189).
    /// </summary>
    private void HookWebView()
    {
        if (_web.Handler is null)
        {
            return;
        }

#if IOS
        if (_web.Handler is Microsoft.Maui.Handlers.IWebViewHandler handler && handler.PlatformView is WebKit.WKWebView platform)
        {
            platform.NavigationDelegate = new SignInNavigationDelegate(handler, this);
        }
#elif ANDROID
        if (_web.Handler is Microsoft.Maui.Handlers.WebViewHandler handler && handler.PlatformView is Android.Webkit.WebView platform)
        {
            platform.SetWebViewClient(new SignInWebViewClient(handler, this));
        }
#endif

        if (_web.Source is null)
        {
            _logger.LogInformation("Web sign-in: starting at {Page}", Describe(_request.Flow.TokensPage.AbsoluteUri));
            _web.Source = new UrlWebViewSource { Url = _request.Flow.TokensPage.AbsoluteUri };
        }
    }

    /// <summary>
    /// Whether the web view may accept a certificate the system turned down:
    /// only the server's, and only when it's one the user already trusted for
    /// the API. The web view can't ask on its own, so the sign-in page asks
    /// first (<see cref="ICertificateProbe"/>), and nothing else - another
    /// host, another certificate - gets through.
    /// </summary>
    private bool IsTrustedCertificate(string? host, byte[]? certificate)
    {
        var trusted = _request.AllowUntrustedCertificate
            && _request.Flow.IsServerHost(host)
            && CertificateTrust.IsTrusted(_request.TrustedCertificates, certificate);
        _logger.LogInformation("Web sign-in: certificate error on {Host}, {Action}", host, trusted ? "trusted" : "refused");
        return trusted;
    }

#if IOS
    /// <summary>
    /// MAUI's own navigation delegate, plus the certificate check it leaves to
    /// the system, and the page starts and POSTs the sign-in page watches.
    /// </summary>
    private sealed class SignInNavigationDelegate : Microsoft.Maui.Platform.MauiWebViewNavigationDelegate
    {
        private readonly LibreNmsSignInPage _page;

        public SignInNavigationDelegate(Microsoft.Maui.Handlers.IWebViewHandler handler, LibreNmsSignInPage page)
            : base(handler)
        {
            _page = page;
        }

        [Foundation.Export("webView:didStartProvisionalNavigation:")]
        public void DidStartProvisionalNavigation(WebKit.WKWebView webView, WebKit.WKNavigation navigation) =>
            _page.PageStarted(webView.Url?.AbsoluteString);

        /// <summary>
        /// WebKit asks this, when it's there, in place of the plain
        /// decidePolicyForNavigationAction MAUI answers - so the POST is
        /// noted, then MAUI decides as it always does.
        /// </summary>
        [Foundation.Export("webView:decidePolicyForNavigationAction:preferences:decisionHandler:")]
        public void DecidePolicyWithPreferences(
            WebKit.WKWebView webView,
            WebKit.WKNavigationAction navigationAction,
            WebKit.WKWebpagePreferences preferences,
            Action<WebKit.WKNavigationActionPolicy, WebKit.WKWebpagePreferences> decisionHandler)
        {
            if (navigationAction.TargetFrame?.MainFrame != false
                && string.Equals(navigationAction.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                _page.PostStarting(navigationAction.Request.Url?.AbsoluteString);
            }

            DecidePolicy(webView, navigationAction, policy => decisionHandler(policy, preferences));
        }

        [Foundation.Export("webView:didReceiveAuthenticationChallenge:completionHandler:")]
        public void DidReceiveAuthenticationChallenge(
            WebKit.WKWebView webView,
            Foundation.NSUrlAuthenticationChallenge challenge,
            Action<Foundation.NSUrlSessionAuthChallengeDisposition, Foundation.NSUrlCredential> completionHandler)
        {
            var space = challenge.ProtectionSpace;
            if (space.AuthenticationMethod == Foundation.NSUrlProtectionSpace.AuthenticationMethodServerTrust
                && space.ServerSecTrust is { } trust
                && !trust.Evaluate(out _)
                && _page.IsTrustedCertificate(space.Host, Leaf(trust)))
            {
                completionHandler(Foundation.NSUrlSessionAuthChallengeDisposition.UseCredential, new Foundation.NSUrlCredential(trust));
                return;
            }

            completionHandler(Foundation.NSUrlSessionAuthChallengeDisposition.PerformDefaultHandling, null!);
        }

        private static byte[]? Leaf(global::Security.SecTrust trust)
        {
            var chain = trust.GetCertificateChain();
            return chain.Length > 0 ? chain[0].DerData.ToArray() : null;
        }
    }
#elif ANDROID
    /// <summary>
    /// MAUI's own web view client, plus a certificate check for the trusted
    /// fingerprint, and the page starts and POSTs the sign-in page watches.
    /// </summary>
    private sealed class SignInWebViewClient : Microsoft.Maui.Platform.MauiWebViewClient
    {
        private readonly LibreNmsSignInPage _page;

        public SignInWebViewClient(Microsoft.Maui.Handlers.WebViewHandler handler, LibreNmsSignInPage page)
            : base(handler)
        {
            _page = page;
        }

        public override void OnPageStarted(Android.Webkit.WebView? view, string? url, Android.Graphics.Bitmap? favicon)
        {
            _page.PageStarted(url);
            base.OnPageStarted(view, url, favicon);
        }

        /// <summary>Asked for every request, on a network thread; only noted, never answered.</summary>
        public override Android.Webkit.WebResourceResponse? ShouldInterceptRequest(Android.Webkit.WebView? view, Android.Webkit.IWebResourceRequest? request)
        {
            if (request is { IsForMainFrame: true }
                && string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                _page.PostStarting(request.Url?.ToString());
            }

            return base.ShouldInterceptRequest(view, request);
        }

        public override void OnReceivedSslError(Android.Webkit.WebView? view, Android.Webkit.SslErrorHandler? handler, Android.Net.Http.SslError? error)
        {
            if (error?.Certificate is { } certificate
                && Uri.TryCreate(error.Url, UriKind.Absolute, out var url)
                && _page.IsTrustedCertificate(url.Host, Encoded(certificate)))
            {
                handler?.Proceed();
                return;
            }

            handler?.Cancel();
        }

        private static byte[]? Encoded(Android.Net.Http.SslCertificate certificate)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                return certificate.X509Certificate?.GetEncoded();
            }

            // Before Android 10 the certificate's bytes are only in its saved state.
            return Android.Net.Http.SslCertificate.SaveState(certificate)?.GetByteArray("x509-certificate");
        }
    }
#endif
}
