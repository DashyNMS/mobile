using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Security;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.SignIn;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// Signs in from the QR code desktop shows (#171, desktop#263): the Camera
/// app opens its <c>dashynms://sign-in</c> link, Core's <see cref="SignInLink"/>
/// reads it, and after asking - always - the phone signs in as that computer
/// does, keeping the token in the keychain.
/// </summary>
/// <remarks>
/// <para>Any web page can open a <c>dashynms://</c> link, so nothing happens
/// without a yes to Core's own question, which names the server and the
/// backup address. Signed in already, the question says the current session
/// will be replaced; and the current session stays until the new one has
/// worked.</para>
/// <para>Only the certificates the link names are trusted - another
/// certificate the server presents fails the sign-in rather than being
/// offered - and they're forgotten again if the sign-in doesn't go ahead.</para>
/// </remarks>
public sealed class SignInLinks
{
    private readonly ISessionService _session;
    private readonly ISettingsStore _settings;
    private readonly SecretCache _secrets;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly IAlertNotifier _notifier;
    private readonly NotificationRouter _router;
    private readonly ILogger<SignInLinks> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SignInLinks(
        ISessionService session,
        ISettingsStore settings,
        SecretCache secrets,
        IDialogService dialogs,
        INavigationService navigation,
        IAlertNotifier notifier,
        NotificationRouter router,
        ILogger<SignInLinks> logger)
    {
        _session = session;
        _settings = settings;
        _secrets = secrets;
        _dialogs = dialogs;
        _navigation = navigation;
        _notifier = notifier;
        _router = router;
        _logger = logger;
    }

    /// <summary>Whether <paramref name="link"/> is a sign-in link (not a widget's or a notification's), whatever it holds.</summary>
    public static bool IsSignInLink(string? link) =>
        link is not null && link.StartsWith($"{SignInLink.Scheme}://{SignInLink.Action}", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads <paramref name="text"/>, asks, and signs in. One link at a time:
    /// a second scan while the first is being asked about is ignored.
    /// </summary>
    /// <returns>True when it signed in.</returns>
    public async Task<bool> OpenAsync(string? text)
    {
        if (!await _gate.WaitAsync(0).ConfigureAwait(true))
        {
            return false;
        }

        try
        {
            return await OpenOnceAsync(text).ConfigureAwait(true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> OpenOnceAsync(string? text)
    {
        if (!SignInLink.TryParse(text, out var link, out var error))
        {
            // Never the link itself in the log: it holds a token.
            _logger.LogInformation("A sign-in link couldn't be read: {Error}", error);
            await _dialogs.AlertAsync("Can't sign in", error ?? "That isn't a DashyNMS sign-in link.").ConfigureAwait(true);
            return false;
        }

        var replacing = _session.IsConnected && _session.Connection is { } current
            ? $"\n\nThis replaces your sign-in to {current.WebRoot.Host}."
            : string.Empty;
        if (!await _dialogs.ConfirmAsync(link!.ConfirmationTitle, link.ConfirmationDetail + replacing, "Sign in", "Cancel").ConfigureAwait(true))
        {
            _logger.LogInformation("A sign-in link was turned down");
            return false;
        }

        // Plain http sends the token unencrypted - said as signing in by hand says it (#3).
        if (link.Server.Scheme == Uri.UriSchemeHttp || link.Backup?.Scheme == Uri.UriSchemeHttp)
        {
            var proceed = await _dialogs.ConfirmAsync(
                "Not a secure connection",
                "This server isn't using HTTPS. Your API token and everything DashyNMS loads will be sent unencrypted, and anyone on the network can read them. Continue anyway?",
                "Continue",
                "Cancel").ConfigureAwait(true);
            if (!proceed)
            {
                return false;
            }
        }

        await _secrets.EnsureLoadedAsync(ServiceCollectionExtensions.SecretKeys).ConfigureAwait(true);

        var trusted = _settings.Current.TrustedCertificates;
        var added = link.TrustedCertificates
            .Where(f => !trusted.Any(t => DesktopNMS.Core.Security.CertificateTrust.SameFingerprint(t, f)))
            .ToList();
        trusted.AddRange(added);

        var result = await _session.SignInAsync(
            link.Server.ToString(),
            link.Token,
            link.AllowUntrustedCertificate,
            rememberToken: true,
            backupAddress: link.Backup?.ToString()).ConfigureAwait(true);

        if (!result.Succeeded)
        {
            trusted.RemoveAll(added.Contains);
            _settings.Save();
            var reason = result.UntrustedCertificate is not null
                ? "The server's certificate isn't one the link names, so it wasn't trusted. Check you scanned the code from the right computer, or sign in by hand."
                : result.ErrorMessage ?? "LibreNMS didn't accept the sign-in.";
            await _dialogs.AlertAsync("Can't sign in", reason).ConfigureAwait(true);
            return false;
        }

        _logger.LogInformation("Signed in from a sign-in link");
        await _navigation.GoToAsync(Routes.Main).ConfigureAwait(true);
        if (_settings.Current.Notifications.Enabled)
        {
            await _notifier.RequestPermissionAsync().ConfigureAwait(true);
        }

        await _router.MainShownAsync().ConfigureAwait(true);
        return true;
    }
}
