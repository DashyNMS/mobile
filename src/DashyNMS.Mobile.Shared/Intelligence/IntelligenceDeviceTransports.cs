using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Security;
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
/// session when it fails over to the backup or is switched back by hand. A failover the
/// chat's transport detects itself stays its own, so a chat can't move the whole app;</item>
/// <item>raises <see cref="CertificateRejected"/> for a certificate it turns down, so
/// <see cref="Security.CertificatePrompt"/> asks about it as it does for the session's
/// requests, and <see cref="TrustCertificate"/> accepts it on every live transport at once.</item>
/// </list>
/// <para>
/// None of this can point a transport at another server or token: timeout, trust and
/// address changes go through Core's own <see cref="LibreNmsTransport"/> calls, which keep
/// the connection's web root, backup and token. Each transport has its own
/// <see cref="ServerFailover"/>, so configuring one never resets the session's.
/// </para>
/// </remarks>
public sealed class IntelligenceDeviceTransports : IDisposable
{
    private readonly LibreNmsTransport shared;
    private readonly ISettingsStore settings;
    private readonly HashSet<Lease> live = [];
    private readonly Lock gate = new();
    private bool disposed;

    /// <param name="shared">
    /// The session's transport: read for its failover state and request timing hook, never
    /// used to send. Its <see cref="ServerFailover"/> is followed, not shared.
    /// </param>
    /// <param name="settings">The source of the current timeout and trusted certificates.</param>
    public IntelligenceDeviceTransports(LibreNmsTransport shared, ISettingsStore settings)
    {
        this.shared = shared;
        this.settings = settings;
        settings.Changed += OnSettingsChanged;
        shared.Failover.Changed += OnSessionFailoverChanged;
    }

    /// <summary>
    /// A certificate a chat's request was refused on, raised on the thread that made the
    /// request. The app asks about it as it does for the session's own requests.
    /// </summary>
    public event EventHandler<CertificateDetails>? CertificateRejected;

    /// <summary>Transports open now; for tests and diagnostics.</summary>
    internal int LiveCount
    {
        get { lock (gate) return live.Count; }
    }

    /// <summary>
    /// Opens a transport for <paramref name="connection"/> alone. The caller owns it and
    /// must dispose it; disposing closes its connections and stops it following the session.
    /// </summary>
    internal IOwnedTransport Open(LibreNmsConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var failover = new ServerFailover();
        var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance, failover) { RequestObserver = shared.RequestObserver };
        var lease = new Lease(this, connection, transport, failover);
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
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Accepts <paramref name="fingerprint"/> on every live chat transport straight away,
    /// as <see cref="DesktopNMS.Services.ISessionService.TrustCertificate"/> does for the session's.
    /// </summary>
    public void TrustCertificate(string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        foreach (var lease in Snapshot())
            lease.Transport.TrustCertificate(fingerprint);
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
    private sealed class Lease(IntelligenceDeviceTransports owner, LibreNmsConnection captured, LibreNmsTransport transport, ServerFailover failover)
        : IOwnedTransport
    {
        private int closed;

        public LibreNmsTransport Transport => transport;

        public LibreNmsConnection? Connection => transport.Connection;

        public void OnCertificateRejected(object? sender, CertificateDetails certificate) =>
            owner.CertificateRejected?.Invoke(owner, certificate);

        /// <summary>Moves to the backup or back with the session; a no-op when already there.</summary>
        public void Follow(bool sessionOnBackup)
        {
            if (Volatile.Read(ref closed) != 0 || captured.BackupWebRoot is null || failover.IsOnBackup == sessionOnBackup)
                return;
            if (sessionOnBackup)
                failover.Configure(captured.BackupWebRoot.ToString(), startOnBackup: true);
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
            transport.CertificateRejected -= OnCertificateRejected;
            transport.Dispose();
        }
    }
}
