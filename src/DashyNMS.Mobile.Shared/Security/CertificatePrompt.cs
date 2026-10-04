using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Security;

/// <summary>
/// Asks about a certificate while signed in (#141), as desktop's
/// <c>App.SetUpCertificatePrompt</c> does (desktop #236). Certificates used to
/// be checked only at sign-in, so the first failover to a backup address with
/// a self-signed or private-CA certificate left every request failing, with
/// nowhere to see the certificate or accept it short of signing out.
/// </summary>
/// <remarks>
/// Core raises <see cref="ILibreNmsClient.CertificateRejected"/> for every
/// request that fails on the certificate - several at once, from whichever
/// thread made each - so it asks once while a prompt is open, and not again
/// this session about one that's been declined. Accepting trusts it on the
/// running connection straight away and saves it to <c>trustedCertificates</c>,
/// which desktop and phone share.
/// </remarks>
public sealed class CertificatePrompt
{
    private readonly ILibreNmsClient _client;
    private readonly ISessionService _session;
    private readonly IDialogService _dialogs;
    private readonly HashSet<string> _declined = new(StringComparer.OrdinalIgnoreCase);
    private int _asking;
    private bool _started;

    public CertificatePrompt(ILibreNmsClient client, ISessionService session, IDialogService dialogs)
    {
        _client = client;
        _session = session;
        _dialogs = dialogs;
    }

    /// <summary>Raised after a certificate is trusted, so the page showing can load again.</summary>
    public event EventHandler? Trusted;

    /// <summary>Starts listening. Once, at start-up; again does nothing.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _client.CertificateRejected += (_, certificate) => _ = AskAsync(certificate);
    }

    /// <summary>
    /// Asks whether to trust <paramref name="certificate"/> - unless signed
    /// out (sign-in asks for itself), already asking, or it's been declined.
    /// True when it was trusted.
    /// </summary>
    internal async Task<bool> AskAsync(CertificateDetails certificate)
    {
        if (!_session.IsConnected)
        {
            return false;
        }

        lock (_declined)
        {
            if (_declined.Contains(certificate.Fingerprint))
            {
                return false;
            }
        }

        if (Interlocked.Exchange(ref _asking, 1) == 1)
        {
            return false;
        }

        try
        {
            var service = _client.Failover.IsOnBackup ? "LibreNMS (backup address)" : "LibreNMS";
            var (title, message) = CertificateTrust.DescribeForPrompt(certificate, service);
            if (!await _dialogs.ConfirmAsync(title, message, "Trust", "Cancel"))
            {
                lock (_declined)
                {
                    _declined.Add(certificate.Fingerprint);
                }

                return false;
            }

            _session.TrustCertificate(certificate);
            Trusted?.Invoke(this, EventArgs.Empty);
            return true;
        }
        finally
        {
            Volatile.Write(ref _asking, 0);
        }
    }
}
