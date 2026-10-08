using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Security;
using DesktopNMS.Core.SignIn;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// Server address and API token, the same fields as desktop's connection
/// window - and first, "Sign in with LibreNMS" (#162): sign in on the
/// server's own website and let the app create its token, with typing a
/// token in kept as the way back when that can't work.
/// </summary>
public sealed partial class SignInViewModel : ViewModelBase
{
    private readonly ISessionService _session;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly SecretCache _secrets;
    private readonly IAlertNotifier _notifier;
    private readonly NotificationRouter _router;
    private readonly IDialogService? _dialogs;
    private readonly IWebSignIn? _webSignIn;
    private readonly ICertificateProbe _probe;
    private bool _restoreAttempted;

    private readonly ILauncherService? _launcher;
    private readonly IClipboardText? _clipboard;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpenTokensPage))]
    [NotifyCanExecuteChangedFor(nameof(OpenTokensPageCommand))]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    private string _apiToken = string.Empty;

    /// <summary>
    /// Where to get a token, in Core's words so desktop says the same (#161):
    /// the settings menu, then API, then API Tokens - and that a LibreNMS admin
    /// can give an account API access.
    /// </summary>
    public string TokenHint => SignInHelp.TokenHint;

    /// <summary>"Open the API Tokens page" - once the address can be read (#161).</summary>
    public bool CanOpenTokensPage => _launcher is not null && LibreNmsConnection.ApiTokensPageUrl(ServerUrl) is not null;

    /// <summary>
    /// "Use the token you copied" - shown while the clipboard holds text, in
    /// token mode (#161). Whether it's a token is only known once read, which
    /// waits for the tap.
    /// </summary>
    [ObservableProperty]
    private bool _canPasteToken;

    [ObservableProperty]
    private string _backupAddress = string.Empty;

    [ObservableProperty]
    private bool _allowUntrustedCertificate;

    [ObservableProperty]
    private bool _rememberToken = true;

    /// <summary>
    /// True while signing back in with the saved token - the page shows the
    /// logo and a spinner instead of the form, so the form doesn't flash up
    /// only to vanish a moment later (issue #28).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowForm))]
    private bool _isRestoring;

    public bool ShowForm => !IsRestoring;

    /// <summary>
    /// True once the user has chosen to type an API token in, or "Sign in
    /// with LibreNMS" couldn't work for this server - the form then asks for
    /// the token as it always did.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsWebSignIn), nameof(ShowsTokenEntry), nameof(ShowsLibreNmsLink))]
    private bool _usesToken;

    /// <summary>"Sign in with LibreNMS" is the way in, unless the platform has no web view for it.</summary>
    public bool ShowsWebSignIn => _webSignIn is not null && !UsesToken;

    public bool ShowsTokenEntry => !ShowsWebSignIn;

    /// <summary>"Sign in with LibreNMS instead", under the token form - only where it exists.</summary>
    public bool ShowsLibreNmsLink => _webSignIn is not null && UsesToken;

    public SignInViewModel(
        ISessionService session,
        ISettingsStore settings,
        INavigationService navigation,
        SecretCache secrets,
        IAlertNotifier notifier,
        NotificationRouter router,
        IDialogService? dialogs = null,
        IWebSignIn? webSignIn = null,
        ICertificateProbe? probe = null,
        ILauncherService? launcher = null,
        IClipboardText? clipboard = null)
    {
        _launcher = launcher;
        _clipboard = clipboard;
        _dialogs = dialogs;
        _webSignIn = webSignIn;
        _probe = probe ?? new CertificateProbe();
        _secrets = secrets;
        _notifier = notifier;
        _router = router;
        _session = session;
        _settings = settings;
        _navigation = navigation;

        var current = settings.Current;
        _serverUrl = current.ServerUrl ?? string.Empty;
        _backupAddress = current.BackupServerAddress ?? string.Empty;
        _allowUntrustedCertificate = current.AllowUntrustedCertificate;
        _rememberToken = current.RememberToken || string.IsNullOrEmpty(current.ServerUrl);

        // Whether there's a token isn't known until the keychain has been
        // read, but a remembered server is a good guess that there is.
        _isRestoring = current.RememberToken && !string.IsNullOrEmpty(current.ServerUrl);
    }

    /// <summary>The server's API Tokens page, in the browser (#161), to create a token and copy it.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenTokensPage))]
    private Task OpenTokensPageAsync() =>
        LibreNmsConnection.ApiTokensPageUrl(ServerUrl) is { } page && _launcher is not null
            ? _launcher.OpenAsync(page)
            : Task.CompletedTask;

    /// <summary>
    /// Looks whether there's copied text to offer - on showing, coming back
    /// to the app (from copying a token in the browser) and focusing the
    /// token box. Only asks whether there's text; nothing is read yet.
    /// </summary>
    public void CheckClipboard() => CanPasteToken = _clipboard?.HasText == true;

    /// <summary>
    /// "Use the token you copied" (#161): reads the clipboard now the user has
    /// asked, and fills in the token if it is one - trimmed, a leading
    /// "Bearer " dropped, as Core's ApiTokenText reads it.
    /// </summary>
    [RelayCommand]
    private async Task PasteTokenAsync()
    {
        if (_clipboard is null)
        {
            return;
        }

        string? text;
        try
        {
            text = await _clipboard.GetTextAsync();
        }
        catch (Exception)
        {
            // The user said no to pasting, or the platform wouldn't.
            return;
        }

        if (ApiTokenText.TryExtract(text, out var token))
        {
            ApiToken = token;
            ErrorMessage = null;
        }
        else
        {
            ErrorMessage = "What you copied isn't an API token. In LibreNMS, copy the token shown once after you create it.";
        }
    }

    /// <summary>
    /// On first show: signs straight back in with the saved address and token,
    /// if there are any and they still work.
    /// </summary>
    [RelayCommand]
    private async Task AppearingAsync()
    {
        if (_restoreAttempted)
        {
            return;
        }

        _restoreAttempted = true;

        var restored = false;
        await RunAsync(async () =>
        {
            // The saved token is only readable once secrets are loaded.
            await _secrets.EnsureLoadedAsync(ServiceCollectionExtensions.SecretKeys);
            var result = await _session.TryRestoreAsync();

            // A certificate not yet trusted, or changed since it was (#189):
            // show it, and only on your say-so trust it and try again.
            if (result?.UntrustedCertificate is { } certificate && await TrustAsync(certificate))
            {
                result = await _session.TryRestoreAsync();
            }

            restored = result?.Succeeded == true;
            if (result is { Succeeded: false })
            {
                ErrorMessage = result.ErrorMessage;
            }
        });

        if (restored)
        {
            await ShowMainAsync();
        }

        // Signed in, or it didn't work: either way the form is what's
        // wanted if this page is shown again (after signing out).
        IsRestoring = false;
    }

    /// <summary>
    /// Whether <paramref name="address"/> would be reached over plain http -
    /// typed with http:// (a bare name gets https://, as LibreNmsConnection does).
    /// </summary>
    internal static bool IsCleartext(string? address) =>
        !string.IsNullOrWhiteSpace(address)
        && address.Trim().StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private async Task SignInAsync()
    {
        // Nothing to sign in with: say where a token comes from, in Core's words (#161).
        if (string.IsNullOrWhiteSpace(ApiToken))
        {
            ErrorMessage = SignInHelp.MissingToken;
            return;
        }

        // Core's HTTP client goes round Android's cleartext policy and iOS's
        // App Transport Security, so neither would stop the token going out
        // unencrypted; say so first (#3). Some LAN-only installs have no TLS,
        // so it's a warning rather than a refusal.
        if (_dialogs is not null && (IsCleartext(ServerUrl) || IsCleartext(BackupAddress)))
        {
            var proceed = await _dialogs.ConfirmAsync(
                "Not a secure connection",
                "This server isn't using HTTPS. Your API token and everything DashyNMS loads will be sent unencrypted, and anyone on the network can read them. Continue anyway?",
                "Continue",
                "Cancel");
            if (!proceed)
            {
                return;
            }
        }

        await CompleteSignInAsync();
    }

    [RelayCommand]
    private void UseToken()
    {
        ErrorMessage = null;
        UsesToken = true;
    }

    [RelayCommand]
    private void UseLibreNms()
    {
        ErrorMessage = null;
        UsesToken = false;
    }

    /// <summary>
    /// "Sign in with LibreNMS" (#162): checks the server answers and its
    /// certificate is trusted, then shows its website for the user to sign in
    /// on, and signs in with the token the app creates there - named after the
    /// phone and the day. When it can't work - plain http, an account without
    /// API access, LibreNMS before 26.4 - it says why and goes back to asking
    /// for a token.
    /// </summary>
    [RelayCommand]
    private async Task SignInWithLibreNmsAsync()
    {
        if (_webSignIn is null)
        {
            return;
        }

        if (!LibreNmsConnection.TryParseWebRoot(ServerUrl, out var webRoot, out var error))
        {
            ErrorMessage = error;
            return;
        }

        // The user types their password into this page, so never over plain
        // http - and iOS and Android's web views wouldn't load it anyway.
        if (!string.Equals(webRoot!.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            FallBack(WebSignInMessages.NeedsHttps);
            return;
        }

        var reached = false;
        await RunAsync(async () =>
        {
            var probe = await _probe.ProbeAsync(webRoot, AllowUntrustedCertificate, TrustedCertificates());
            if (probe.UntrustedCertificate is { } certificate)
            {
                probe = await TrustAsync(certificate)
                    ? await _probe.ProbeAsync(webRoot, AllowUntrustedCertificate, TrustedCertificates())
                    : new ProbeResult(false, ErrorMessage: WebSignInMessages.CertificateDeclined);
            }

            reached = probe.Reached;
            ErrorMessage = probe.Reached ? null : probe.ErrorMessage;
        });

        if (!reached)
        {
            return;
        }

        var flow = new WebTokenSignIn(webRoot, WebTokenSignIn.NameFor(_webSignIn.DeviceName, DateTime.Now));
        var result = await _webSignIn.SignInAsync(new WebSignInRequest(flow, AllowUntrustedCertificate, TrustedCertificates()));

        switch (result.Outcome)
        {
            case WebSignInOutcome.Token:
                ApiToken = result.Token!;
                if (!await CompleteSignInAsync())
                {
                    // The token's made: keep it in the box (hidden) to try again.
                    UsesToken = true;
                }

                break;

            case WebSignInOutcome.NotAllowed:
                FallBack(WebSignInMessages.NotAllowed);
                break;

            case WebSignInOutcome.Failed:
                FallBack(WebSignInMessages.Failed);
                break;
        }
    }

    private void FallBack(string message)
    {
        UsesToken = true;
        ErrorMessage = message;
    }

    private IReadOnlyCollection<string> TrustedCertificates() =>
        _settings.Current.TrustedCertificates.ToArray();

    /// <summary>Signs in with <see cref="ApiToken"/> - typed in, or created by "Sign in with LibreNMS".</summary>
    private async Task<bool> CompleteSignInAsync()
    {
        var succeeded = false;
        await RunAsync(async () =>
        {
            // Saving the token goes through the same cache - see SecretCache.
            await _secrets.EnsureLoadedAsync(ServiceCollectionExtensions.SecretKeys);
            var result = await _session.SignInAsync(
                ServerUrl,
                ApiToken,
                AllowUntrustedCertificate,
                RememberToken,
                backupAddress: BackupAddress);

            if (result.UntrustedCertificate is { } certificate && await TrustAsync(certificate))
            {
                result = await _session.SignInAsync(ServerUrl, ApiToken, AllowUntrustedCertificate, RememberToken, backupAddress: BackupAddress);
            }

            succeeded = result.Succeeded;
            ErrorMessage = result.Succeeded ? null : result.ErrorMessage;
        });

        if (succeeded)
        {
            // Never leave the token sitting in a text box behind the app.
            ApiToken = string.Empty;
            await ShowMainAsync();
        }

        return succeeded;
    }

    /// <summary>
    /// Shows a certificate LibreNMS presented that this phone can't verify -
    /// what's wrong with it, who it names and its fingerprint, in Core's
    /// words so desktop asks the same way - and trusts that exact
    /// certificate only if you say so (#189).
    /// </summary>
    private async Task<bool> TrustAsync(CertificateDetails certificate)
    {
        if (_dialogs is null)
        {
            return false;
        }

        var (title, message) = CertificateTrust.DescribeForPrompt(certificate, "LibreNMS");
        if (!await _dialogs.ConfirmAsync(title, message, "Trust", "Cancel"))
        {
            return false;
        }

        _session.TrustCertificate(certificate);
        return true;
    }

    private async Task ShowMainAsync()
    {
        await _navigation.GoToAsync(Routes.Main);

        // Asked here, once there's something to be notified about; the
        // platforms only actually prompt the first time.
        if (_settings.Current.Notifications.Enabled)
        {
            await _notifier.RequestPermissionAsync();
        }

        // A tapped notification may be what opened the app.
        await _router.MainShownAsync();
    }
}
