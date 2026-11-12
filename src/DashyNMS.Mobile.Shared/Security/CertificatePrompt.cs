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
/// Each refusal is asked about only for the session generation it came from. A chat
/// transport's carries the generation it was opened for. The client's can't be bound at all:
/// it names only the certificate and its host, and the client is one long-lived object across
/// every switch that keeps the last refused certificate in one field. A request still in
/// flight from the previous connection - the same host on another port, or the same address
/// before its certificate was rotated - reports the old certificate after the switch, and no
/// comparison of hosts can tell. So the client's refusal is only a sign that something was
/// refused: the prompt sends a fresh request to the connection in use now, on a transport
/// bound to its generation (<see cref="ICertificateProbe"/>), and asks about the certificate
/// that request meets, or nothing when it gets through. Without a probe it asks nothing; the
/// next sign-in still asks as it always has.
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
    private readonly ICertificateProbe? _probe;
    private readonly HashSet<string> _declined = new(StringComparer.OrdinalIgnoreCase);
    private int _asking;
    private int _probing;
    private int _probeAgain;
    private bool _started;

    /// <param name="probe">
    /// Checks the connection in use when the session's client reports a refusal; the chat
    /// transports when not given, which is what the app uses.
    /// </param>
    public CertificatePrompt(ILibreNmsClient client, ISessionService session, IDialogService dialogs,
        DashyNMS.Mobile.Intelligence.IntelligenceDeviceTransports? chatTransports = null, ICertificateProbe? probe = null)
    {
        _chatTransports = chatTransports;
        _probe = probe ?? chatTransports;
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
    /// A refusal from the session's own client, which names no connection: a sign to probe the
    /// one in use now and ask about what that meets, never about this certificate as reported
    /// (see remarks). One probe at a time; refusals while one runs make it check once more.
    /// </summary>
    internal async Task<bool> AskAsync(CertificateDetails reported)
    {
        ArgumentNullException.ThrowIfNull(reported);
        // Cheap filters before any request: another server's host, a prompt already open, or a
        // certificate already declined (asking again would be declined too).
        if (_probe is null || _session.CurrentScope is not { } first || Dialled(first.Connection, reported.Host) is null
            || Volatile.Read(ref _asking) == 1 || IsDeclined(reported.Fingerprint))
        {
            return false;
        }

        if (Interlocked.Exchange(ref _probing, 1) == 1)
        {
            Volatile.Write(ref _probeAgain, 1);
            return false;
        }

        try
        {
            // Bounded: a refusal that lands while probing (the session switched, say) earns one
            // more look, not an endless loop.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                Volatile.Write(ref _probeAgain, 0);
                if (_session.CurrentScope is not { } scope)
                {
                    return false;
                }

                if (await _probe.ProbeAsync(scope).ConfigureAwait(false) is { } refused
                    && await AskAsync(refused.Certificate, refused.Generation, refused.OnBackup).ConfigureAwait(false))
                {
                    return true;
                }

                if (Volatile.Read(ref _probeAgain) == 0)
                {
                    return false;
                }
            }

            return false;
        }
        finally
        {
            Volatile.Write(ref _probing, 0);
        }
    }

    private bool IsDeclined(string fingerprint)
    {
        lock (_declined)
        {
            return _declined.Contains(fingerprint);
        }
    }

    /// <summary>
    /// Whether <paramref name="host"/> is <paramref name="connection"/>'s backup address (true)
    /// or its server address (false); null when it is neither, so the refusal came from
    /// another connection. Only a filter before probing: a match can't bind a refusal, since a
    /// host doesn't name the port, the connection or the certificate the server has now.
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

        if (IsDeclined(certificate.Fingerprint))
        {
            return false;
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
