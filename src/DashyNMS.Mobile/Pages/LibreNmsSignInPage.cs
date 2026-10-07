using DesktopNMS.Core.Security;
using DesktopNMS.Core.SignIn;

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
/// is one the user already trusted for the API (#189) - see the platform
/// hooks at the bottom.
/// </remarks>
public sealed class LibreNmsSignInPage : ContentPage
{
    private readonly WebSignInRequest _request;
    private readonly TaskCompletionSource<WebSignInResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly WebView _web;
    private readonly Controls.BusyCard _busy;
    private bool _handling;

    public LibreNmsSignInPage(WebSignInRequest request)
    {
        _request = request;
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

        _web = new WebView
        {
            Source = new UrlWebViewSource { Url = request.Flow.TokensPage.AbsoluteUri },
            Opacity = 0,
        };
        _web.Navigated += OnNavigated;
        _web.HandlerChanged += (_, _) => TrustServerCertificate();

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
        if (_result.Task.IsCompleted || _handling)
        {
            return;
        }

        if (e.Result is WebNavigationResult.Failure or WebNavigationResult.Timeout)
        {
            Finish(new WebSignInResult(WebSignInOutcome.Failed));
            return;
        }

        if (e.Result != WebNavigationResult.Success)
        {
            // Cancelled - a redirect replaced it; the next page reports in.
            return;
        }

        _handling = true;
        try
        {
            Uri.TryCreate(e.Url, UriKind.Absolute, out var location);

            // Scripts run on the server's own pages only, never a sign-on provider's.
            string? probe = null;
            if (_request.Flow.IsOnServer(location))
            {
                probe = await RunScriptAsync(WebTokenSignIn.ProbeScript);
            }

            var step = _request.Flow.Next(location, probe);
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
                    Working("Creating a token for DashyNMS…");
                    await RunScriptAsync(_request.Flow.SubmitScript);
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
        finally
        {
            _handling = false;
        }
    }

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
            Working(result.Outcome == WebSignInOutcome.Token ? "Signing in…" : "Closing…");
        }
    }

    /// <summary>
    /// Lets the web view accept the server's certificate when the user has
    /// trusted that exact one (#189). The web view can't ask on its own, so
    /// the sign-in page asks first (<see cref="ICertificateProbe"/>), and
    /// nothing else - another host, another certificate - gets through.
    /// </summary>
    private void TrustServerCertificate()
    {
        if (!_request.AllowUntrustedCertificate || _request.TrustedCertificates.Count == 0)
        {
            return;
        }

#if IOS
        if (_web.Handler is Microsoft.Maui.Handlers.IWebViewHandler handler && handler.PlatformView is WebKit.WKWebView platform)
        {
            platform.NavigationDelegate = new TrustingNavigationDelegate(handler, _request);
        }
#elif ANDROID
        if (_web.Handler is Microsoft.Maui.Handlers.WebViewHandler handler && handler.PlatformView is Android.Webkit.WebView platform)
        {
            platform.SetWebViewClient(new TrustingWebViewClient(handler, _request));
        }
#endif
    }

#if IOS
    /// <summary>MAUI's own navigation delegate, plus the certificate check it leaves to the system.</summary>
    private sealed class TrustingNavigationDelegate : Microsoft.Maui.Platform.MauiWebViewNavigationDelegate
    {
        private readonly WebSignInRequest _request;

        public TrustingNavigationDelegate(Microsoft.Maui.Handlers.IWebViewHandler handler, WebSignInRequest request)
            : base(handler)
        {
            _request = request;
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
                && _request.Flow.IsServerHost(space.Host)
                && !trust.Evaluate(out _)
                && CertificateTrust.IsTrusted(_request.TrustedCertificates, Leaf(trust)))
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
    /// <summary>MAUI's own web view client, plus a certificate check for the trusted fingerprint.</summary>
    private sealed class TrustingWebViewClient : Microsoft.Maui.Platform.MauiWebViewClient
    {
        private readonly WebSignInRequest _request;

        public TrustingWebViewClient(Microsoft.Maui.Handlers.WebViewHandler handler, WebSignInRequest request)
            : base(handler)
        {
            _request = request;
        }

        public override void OnReceivedSslError(Android.Webkit.WebView? view, Android.Webkit.SslErrorHandler? handler, Android.Net.Http.SslError? error)
        {
            if (error?.Certificate is { } certificate
                && Uri.TryCreate(error.Url, UriKind.Absolute, out var url)
                && _request.Flow.IsServerHost(url.Host)
                && CertificateTrust.IsTrusted(_request.TrustedCertificates, Encoded(certificate)))
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
