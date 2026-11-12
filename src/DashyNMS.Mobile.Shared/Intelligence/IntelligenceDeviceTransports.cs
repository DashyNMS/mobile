using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;
using DashyNMS.Mobile.Security;
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
public sealed class IntelligenceDeviceTransports : ICertificateProbe, IDisposable
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
    public event EventHandler<ScopedCertificateRejection>? CertificateRejected;

    /// <summary>Transports open now; for tests and diagnostics.</summary>
    internal int LiveCount
    {
        get { lock (gate) return live.Count; }
    }

    /// <summary>
    /// Opens a transport for <paramref name="scope"/>'s connection alone. The caller owns it
    /// and must dispose it; disposing closes its connections and stops it following the session.
    /// </summary>
    internal IOwnedTransport Open(SessionScope scope) => OpenLease(scope, quiet: false);

    /// <param name="scope">The connection the transport dials, with its generation.</param>
    /// <param name="quiet">
    /// For <see cref="ProbeAsync"/>: its refusal is returned to the caller rather than raised,
    /// and it never moves the session to the backup on its own.
    /// </param>
    private Lease OpenLease(SessionScope scope, bool quiet)
    {
        var connection = scope.Connection;
        ArgumentNullException.ThrowIfNull(connection);
        var failover = new ServerFailover();
        var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance, failover) { RequestObserver = shared.RequestObserver };
        var lease = new Lease(this, scope, transport, failover, quiet);
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
            if (!quiet) transport.CertificateRejected += lease.OnCertificateRejected;
            // Published before it is configured, so a settings or failover change from here on
            // reaches it; Reconcile serialises with those, and each applies the latest state.
            lease.Reconcile();
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Test seam: runs inside a transport's first configuration, after it has read the
    /// settings and failover state it will apply and before it applies them, the window in
    /// which a concurrent change used to be overwritten.
    /// </summary>
    internal Action? BeforeConfigure { get; set; }

    /// <summary>
    /// Sends one request to <paramref name="scope"/>'s connection, on a transport opened for it
    /// alone, and returns the certificate it was refused on; null when it got through or failed
    /// for another reason.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The session's client reports a refusal with only the certificate and its host. Core's
    /// transport is one long-lived object across every switch and keeps the last refused
    /// certificate in one field, so that report can't be tied to a request, a connection or
    /// even a port: a request still in flight from the previous connection, to the same host on
    /// another port, or before the server's certificate was rotated, reports the old
    /// certificate after the switch. Trusting it would carry stale trust into the new session.
    /// </para>
    /// <para>
    /// So <see cref="Security.CertificatePrompt"/> takes the client's report only as a sign that
    /// something was refused, and asks about what this probe meets instead: a request made now,
    /// on a transport that can only dial the connection <paramref name="scope"/> names, at the
    /// address the session is using, and alone on it, so the refusal is that request's.
    /// </para>
    /// </remarks>
    public async Task<ScopedCertificateRejection?> ProbeAsync(SessionScope scope, CancellationToken cancellationToken = default)
    {
        using var lease = OpenLease(scope, quiet: true);
        var onBackup = lease.OnBackup;
        try
        {
            using var response = await lease.SendAsync(HttpMethod.Get, "system", cancellationToken: cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (LibreNmsApiException ex) when (ex.UntrustedCertificate is { } certificate)
        {
            // Moved address mid-request: which one refused it is no longer known. The next
            // refusal probes again.
            return lease.OnBackup == onBackup ? new(certificate, scope.Generation, onBackup) : null;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Unreachable, offline, an HTTP error: nothing to ask about.
            return null;
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
    private static LibreNmsConnection Current(LibreNmsConnection connection, int timeoutSeconds, IReadOnlyList<string> trusted)
    {
        var updated = connection.WithTimeout(timeoutSeconds);
        foreach (var fingerprint in trusted)
        {
            if (!updated.TrustedCertificates.Any(f => CertificateTrust.SameFingerprint(f, fingerprint)))
                updated = updated.WithTrustedCertificate(fingerprint);
        }
        return updated;
    }

    // Neither handler applies what its event carried: two changes raised close together can
    // run in either order, so each brings every transport up to the latest state instead.
    private void OnSettingsChanged(object? sender, AppSettings current)
    {
        foreach (var lease in Snapshot())
            lease.Reconcile();
    }

    private void OnSessionFailoverChanged(object? sender, EventArgs e)
    {
        foreach (var lease in Snapshot())
            lease.Reconcile();
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
    internal interface IOwnedTransport : ILibreNmsTransport, IDisposable
    {
        /// <summary>Whether it is dialling the connection's backup address; for tests and diagnostics.</summary>
        bool OnBackup { get; }
    }

    /// <summary>One chat's transport: sends through Core's, and closes it on disposal.</summary>
    private sealed class Lease(IntelligenceDeviceTransports owner, SessionScope scope, LibreNmsTransport transport, ServerFailover failover, bool quiet)
        : IOwnedTransport
    {
        /// <summary>
        /// Held while the transport is configured or brought up to date, and while it closes.
        /// Open, Settings and the session's failover each reconcile from whichever thread raised
        /// them; serialised, and each reading the latest state inside it, the last to run always
        /// leaves the newest timeout, trust and address in place, never an older one it read first.
        /// </summary>
        private readonly Lock sync = new();
        private bool configured;
        private int closed;

        public SessionScope Scope => scope;

        public LibreNmsTransport Transport => transport;

        public LibreNmsConnection? Connection => transport.Connection;

        public bool OnBackup => failover.IsOnBackup;

        /// <summary>
        /// Brings the transport to the settings and session address in force now: configures it
        /// the first time, then applies the timeout, every trusted certificate and the address.
        /// Each step is a no-op when nothing changed.
        /// </summary>
        public void Reconcile()
        {
            lock (sync)
            {
                if (Volatile.Read(ref closed) != 0) return;
                var current = owner.settings.Current;
                var timeout = current.TimeoutSeconds;
                IReadOnlyList<string> trusted = current.TrustedCertificates?.ToArray() ?? [];
                var sessionOnBackup = owner.shared.Failover.IsOnBackup;

                if (!configured)
                {
                    owner.BeforeConfigure?.Invoke();
                    transport.Configure(Current(scope.Connection, timeout, trusted),
                        startOnBackup: sessionOnBackup && scope.Connection.BackupWebRoot is not null);
                    configured = true;
                    // After Configure: starting on the backup is following the session, not news for it.
                    if (!quiet) failover.Changed += OnOwnFailoverChanged;
                    return;
                }

                transport.SetTimeout(timeout);
                foreach (var fingerprint in trusted)
                    transport.TrustCertificate(fingerprint);
                Follow(sessionOnBackup);
            }
        }

        public void OnCertificateRejected(object? sender, CertificateDetails certificate)
        {
            if (Volatile.Read(ref closed) == 0)
                owner.CertificateRejected?.Invoke(owner, new(certificate, scope.Generation, failover.IsOnBackup));
        }

        /// <summary>
        /// This transport moved to the backup on its own: two requests in a row couldn't reach
        /// the server. The session decides, for its generation only; it then moves the whole
        /// app and every chat follows (<see cref="Follow"/>). Following the session lands here
        /// too, inside <see cref="Reconcile"/>, and is not reported back: the session moves
        /// under its own lock and then waits on this one, so asking it from here could deadlock.
        /// </summary>
        public void OnOwnFailoverChanged(object? sender, EventArgs e)
        {
            if (sync.IsHeldByCurrentThread || Volatile.Read(ref closed) != 0 || !failover.IsOnBackup || owner.shared.Failover.IsOnBackup)
                return;
            owner.session.SwitchToBackup(scope.Generation);
        }

        /// <summary>Moves to the backup or back with the session; a no-op when already there. Under <see cref="sync"/>.</summary>
        private void Follow(bool sessionOnBackup)
        {
            if (scope.Connection.BackupWebRoot is null || failover.IsOnBackup == sessionOnBackup)
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
            lock (sync)
            {
                failover.Changed -= OnOwnFailoverChanged;
                transport.CertificateRejected -= OnCertificateRejected;
                transport.Dispose();
            }
        }
    }
}

