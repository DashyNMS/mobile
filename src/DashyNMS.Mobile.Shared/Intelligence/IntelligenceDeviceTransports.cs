using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// Owns the private transports Intelligence's device search dials (#596, #605): one per
/// lookup set, each pinned to the connection its chat captured, but kept in step with the
/// session the way the session's own transport is.
/// </summary>
/// <remarks>
/// <para>
/// A transport that only ever dials the chat's own server must not also drop what the
/// session does for every other request. Opened alone it would keep the timeout and
/// trusted certificates the connection had at sign-in, start on the main address while
/// the app is on the backup, and fail on a backup certificate with nobody asked about it.
/// So the host opens each one and, while it is live:
/// </para>
/// <list type="bullet">
/// <item>applies the current request timeout and every saved trusted certificate when it
/// opens, and again whenever settings change (Settings' timeout, a certificate trusted on
/// desktop or the phone);</item>
/// <item>starts on whichever address the session's transport is using, and follows the
/// session when it fails over to the backup or is switched back by hand;</item>
/// <item>hands failover it detects itself to the session (<see cref="ISessionService.SwitchToBackup"/>),
/// so the whole app moves to the backup together, as it does when the session's own
/// requests can't reach the server. Only while the session is still on the connection the
/// chat captured: a chat for an earlier connection can never move a later one;</item>
/// <item>raises <see cref="CertificateRejected"/> for a certificate it turns down, with the
/// session generation it was opened for, so <see cref="Security.CertificatePrompt"/> asks
/// about it as it does for the session's requests and ignores one from an earlier
/// connection. <see cref="TrustCertificate"/> accepts it on that generation's transports.</item>
/// </list>
/// <para>
/// None of this can point a transport at another server or token: timeout, trust and
/// address changes go through Core's own <see cref="LibreNmsTransport"/> calls, which keep
/// the connection's web root, backup and token. Each transport has its own
/// <see cref="ServerFailover"/>, so configuring one never resets the session's. That is
/// also why failover goes through the session rather than a shared object: Core's
/// <see cref="LibreNmsTransport.Configure"/> resets the failover it is given, and the
/// session moves its own failover with its own backup address, never the chat's.
/// </para>
/// </remarks>
public sealed class IntelligenceDeviceTransports : IDisposable
{
    private readonly LibreNmsTransport shared;
    private readonly ISettingsStore settings;
    private readonly ISessionService session;
    private readonly HashSet<Lease> live = [];
    private readonly Lock gate = new();
    private bool disposed;

    /// <param name="shared">
    /// The session's transport: read for its failover state and request timing hook, never
    /// used to send. Its <see cref="ServerFailover"/> is followed, not shared.
    /// </param>
    /// <param name="settings">The source of the current timeout and trusted certificates.</param>
    /// <param name="session">Owns failover: a chat transport that can't reach the server asks it to move.</param>
    public IntelligenceDeviceTransports(LibreNmsTransport shared, ISettingsStore settings, ISessionService session)
    {
        this.shared = shared;
        this.settings = settings;
        this.session = session;
        settings.Changed += OnSettingsChanged;
        shared.Failover.Changed += OnSessionFailoverChanged;
    }

    /// <summary>
    /// A certificate a chat's request was refused on, raised on the thread that made the
    /// request, with the session generation the chat's transport was opened for. The app
    /// asks about it as it does for the session's own requests, unless the session has
    /// moved on since.
    /// </summary>
    public event EventHandler<IntelligenceCertificateRejection>? CertificateRejected;

    /// <summary>Transports open now; for tests and diagnostics.</summary>
    internal int LiveCount
    {
        get { lock (gate) return live.Count; }
    }

    /// <summary>
    /// Opens a transport for <paramref name="scope"/>'s connection alone. The caller owns it
    /// and must dispose it; disposing closes its connections and stops it following the session.
    /// </summary>
    internal IOwnedTransport Open(SessionScope scope)
    {
        var connection = scope.Connection;
        ArgumentNullException.ThrowIfNull(connection);
        var failover = new ServerFailover();
        var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance, failover) { RequestObserver = shared.RequestObserver };
        var lease = new Lease(this, scope, transport, failover);
        bool closed;
        lock (gate)
        {
            closed = disposed;
            if (!closed) live.Add(lease);
        }
        if (closed)
        {
            transport.Dispose();
            throw new ObjectDisposedException(nameof(IntelligenceDeviceTransports));
        }

        try
        {
            transport.CertificateRejected += lease.OnCertificateRejected;
            transport.Configure(Current(connection), startOnBackup: shared.Failover.IsOnBackup && connection.BackupWebRoot is not null);
            // After Configure: starting on the backup is following the session, not news for it.
            failover.Changed += lease.OnOwnFailoverChanged;
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Accepts <paramref name="fingerprint"/> straight away on the live chat transports opened
    /// for <paramref name="generation"/>, as <see cref="ISessionService.TrustCertificate(CertificateDetails, long)"/>
    /// does for the session's. A transport for an earlier connection is left as it was.
    /// </summary>
    public void TrustCertificate(string fingerprint, long generation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        foreach (var lease in Snapshot())
        {
            if (lease.Scope.Generation == generation)
                lease.Transport.TrustCertificate(fingerprint);
        }
    }

    /// <summary>The captured connection with the settings in force now: their timeout and every trusted certificate.</summary>
    private LibreNmsConnection Current(LibreNmsConnection connection)
    {
        var current = settings.Current;
        var updated = connection.WithTimeout(current.TimeoutSeconds);
        foreach (var fingerprint in current.TrustedCertificates ?? [])
        {
            if (!updated.TrustedCertificates.Any(f => CertificateTrust.SameFingerprint(f, fingerprint)))
                updated = updated.WithTrustedCertificate(fingerprint);
        }
        return updated;
    }

    private void OnSettingsChanged(object? sender, AppSettings current)
    {
        foreach (var lease in Snapshot())
        {
            // Each call is a no-op when nothing changed, and keeps the address in use.
            lease.Transport.SetTimeout(current.TimeoutSeconds);
            foreach (var fingerprint in current.TrustedCertificates ?? [])
                lease.Transport.TrustCertificate(fingerprint);
        }
    }

    private void OnSessionFailoverChanged(object? sender, EventArgs e)
    {
        var onBackup = shared.Failover.IsOnBackup;
        foreach (var lease in Snapshot())
            lease.Follow(onBackup);
    }

    private Lease[] Snapshot()
    {
        lock (gate) return [.. live];
    }

    private void Release(Lease lease)
    {
        lock (gate) live.Remove(lease);
    }

    /// <summary>Closes every live transport and stops following the session. For app shutdown and tests.</summary>
    public void Dispose()
    {
        Lease[] closing;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            closing = [.. live];
        }
        settings.Changed -= OnSettingsChanged;
        shared.Failover.Changed -= OnSessionFailoverChanged;
        foreach (var lease in closing) lease.Dispose();
    }

    /// <summary>A transport its holder must dispose.</summary>
    internal interface IOwnedTransport : ILibreNmsTransport, IDisposable { }

    /// <summary>One chat's transport: sends through Core's, and closes it on disposal.</summary>
    private sealed class Lease(IntelligenceDeviceTransports owner, SessionScope scope, LibreNmsTransport transport, ServerFailover failover)
        : IOwnedTransport
    {
        private int closed;

        public SessionScope Scope => scope;

        public LibreNmsTransport Transport => transport;

        public LibreNmsConnection? Connection => transport.Connection;

        public void OnCertificateRejected(object? sender, CertificateDetails certificate)
        {
            if (Volatile.Read(ref closed) == 0)
                owner.CertificateRejected?.Invoke(owner, new(certificate, scope.Generation, failover.IsOnBackup));
        }

        /// <summary>
        /// This transport moved to the backup on its own: two requests in a row couldn't reach
        /// the server. The session decides, for its generation only; it then moves the whole
        /// app and every chat follows (<see cref="Follow"/>). Following the session lands here
        /// too, already matching it, so that is not reported back.
        /// </summary>
        public void OnOwnFailoverChanged(object? sender, EventArgs e)
        {
            if (Volatile.Read(ref closed) != 0 || !failover.IsOnBackup || owner.shared.Failover.IsOnBackup)
                return;
            owner.session.SwitchToBackup(scope.Generation);
        }

        /// <summary>Moves to the backup or back with the session; a no-op when already there.</summary>
        public void Follow(bool sessionOnBackup)
        {
            if (Volatile.Read(ref closed) != 0 || scope.Connection.BackupWebRoot is null || failover.IsOnBackup == sessionOnBackup)
                return;
            if (sessionOnBackup)
                failover.Configure(scope.Connection.BackupWebRoot.ToString(), startOnBackup: true);
            else
                failover.FailBack();
        }

        public Task<System.Text.Json.JsonDocument> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken cancellationToken = default) =>
            transport.SendAsync(method, relativeUrl, body, cancellationToken);

        public Task<IReadOnlyList<T>> GetCollectionAsync<T>(string relativeUrl, string collectionProperty, CancellationToken cancellationToken = default) =>
            transport.GetCollectionAsync<T>(relativeUrl, collectionProperty, cancellationToken);

        public Task<string> SendRawAsync(string relativeUrl, CancellationToken cancellationToken = default) =>
            transport.SendRawAsync(relativeUrl, cancellationToken);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref closed, 1) != 0) return;
            owner.Release(this);
            failover.Changed -= OnOwnFailoverChanged;
            transport.CertificateRejected -= OnCertificateRejected;
            transport.Dispose();
        }
    }
}

/// <summary>A certificate a chat's device search was refused on, and the connection it was refused for.</summary>
/// <param name="Certificate">The certificate the server presented.</param>
/// <param name="Generation">The <see cref="ISessionService.ConnectionGeneration"/> the chat's transport was opened for.</param>
/// <param name="OnBackup">Whether that transport was dialling the backup address, for the prompt's wording.</param>
public sealed record IntelligenceCertificateRejection(CertificateDetails Certificate, long Generation, bool OnBackup);
