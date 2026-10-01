using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>Server address and API token, the same fields as desktop's connection window.</summary>
public sealed partial class SignInViewModel : ViewModelBase
{
    private readonly ISessionService _session;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly SecretCache _secrets;
    private readonly IAlertNotifier _notifier;
    private readonly NotificationRouter _router;
    private readonly IDialogService? _dialogs;
    private bool _restoreAttempted;

    [ObservableProperty]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    private string _apiToken = string.Empty;

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

    public SignInViewModel(
        ISessionService session,
        ISettingsStore settings,
        INavigationService navigation,
        SecretCache secrets,
        IAlertNotifier notifier,
        NotificationRouter router,
        IDialogService? dialogs = null)
    {
        _dialogs = dialogs;
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
