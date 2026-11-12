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
/// <para>
/// Intelligence's device search sends through transports of its own, pinned to the
/// chat's connection (#596). Their refusals are asked about here too, and an accepted
/// certificate is trusted on them as well as on the session's.
/// </para>
/// <para>
/// Each refusal is bound to the session generation it came from. A chat transport's
/// carries the generation it was opened for. The client's carries only the certificate,
/// and it is one long-lived object across every switch, so a request still in flight from
/// the previous connection can fail after the switch. Its refusal is taken for the session
/// now only when the certificate's host is an address that connection dials, read with its
/// generation in one step (<see cref="ISessionService.CurrentScope"/>); anything else is a
/// stale refusal from another server and is dropped. That host also says whether it was
/// the backup address, rather than the session's failover state when the event arrives.
/// A late refusal that does match came from the very endpoint the session dials now, and
/// trusted certificates are one shared list of fingerprints, so asking about it is what a
/// fresh request there would do.
/// </para>
/// <para>
/// One from an earlier connection (a chat's late request after a switch) isn't asked
/// about, and a prompt still open when the session signs in elsewhere or out trusts
/// nothing when accepted: the check and the trust are one step with the switch
/// (<see cref="ISessionService.TrustCertificate(CertificateDetails, long)"/>). So a
/// certificate met on one server is never trusted for another, nor saved to the shared
/// trusted list on its behalf.
/// </para>
/// </remarks>
public sealed class CertificatePrompt
{
    private readonly ILibreNmsClient _client;
    private readonly ISessionService _session;
    private readonly IDialogService _dialogs;
    private readonly DashyNMS.Mobile.Intelligence.IntelligenceDeviceTransports? _chatTransports;
    private readonly HashSet<string> _declined = new(StringComparer.OrdinalIgnoreCase);
    private int _asking;
    private bool _started;

    public CertificatePrompt(ILibreNmsClient client, ISessionService session, IDialogService dialogs,
        DashyNMS.Mobile.Intelligence.IntelligenceDeviceTransports? chatTransports = null)
    {
        _chatTransports = chatTransports;
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
        if (_chatTransports is not null)
        {
            _chatTransports.CertificateRejected += (_, rejection) =>
                _ = AskAsync(rejection.Certificate, rejection.Generation, rejection.OnBackup);
        }
    }

    /// <summary>
    /// A refusal from the session's own client, which names no connection: bound to the one in
    /// use now only when the certificate's host is an address it dials (see remarks).
    /// </summary>
    internal Task<bool> AskAsync(CertificateDetails certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (_session.CurrentScope is not { } scope || Dialled(scope.Connection, certificate.Host) is not { } onBackup)
        {
            return Task.FromResult(false);
        }

        return AskAsync(certificate, scope.Generation, onBackup);
    }

    /// <summary>
    /// Whether <paramref name="host"/> is <paramref name="connection"/>'s backup address (true)
    /// or its server address (false); null when it is neither, so the refusal came from
    /// another connection.
    /// </summary>
    internal static bool? Dialled(LibreNmsConnection connection, string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        if (SameHost(connection.WebRoot, host))
        {
            return false;
        }

        return connection.BackupWebRoot is { } backup && SameHost(backup, host) ? true : null;
    }

    private static bool SameHost(Uri address, string host)
    {
        var bare = host.Trim().Trim('[', ']');
        return string.Equals(address.Host.Trim('[', ']'), bare, StringComparison.OrdinalIgnoreCase)
            || string.Equals(address.IdnHost.Trim('[', ']'), bare, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Asks whether to trust <paramref name="certificate"/> - unless signed
    /// out (sign-in asks for itself), the session has moved on from
    /// <paramref name="generation"/>, already asking, or it's been declined.
    /// True when it was trusted.
    /// </summary>
    /// <param name="generation">The session generation the refusal came from.</param>
    /// <param name="onBackup">Whether the refused request was dialling the backup address.</param>
    internal async Task<bool> AskAsync(CertificateDetails certificate, long generation, bool onBackup)
    {
        if (!_session.IsConnected || generation != _session.ConnectionGeneration)
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
            var service = onBackup ? "LibreNMS (backup address)" : "LibreNMS";
            var (title, message) = CertificateTrust.DescribeForPrompt(certificate, service);
            if (!await _dialogs.ConfirmAsync(title, message, "Trust", "Cancel"))
            {
                // A decline answers for this connection's certificate only; after a switch
                // the same certificate on the next connection is still worth asking about.
                if (generation == _session.ConnectionGeneration)
                {
                    lock (_declined)
                    {
                        _declined.Add(certificate.Fingerprint);
                    }
                }

                return false;
            }

            // Refused when the session signed in elsewhere or out while the prompt was open.
            if (!_session.TrustCertificate(certificate, generation))
            {
                return false;
            }

            _chatTransports?.TrustCertificate(certificate.Fingerprint, generation);
            Trusted?.Invoke(this, EventArgs.Empty);
            return true;
        }
        finally
        {
            Volatile.Write(ref _asking, 0);
        }
    }
}
