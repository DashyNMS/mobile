using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// find_device's private transports (#596) keep what the session's transport does for every
/// other request: the certificate prompt and live trust, the request timeout as it changes,
/// the backup address, and closing when the attempt that opened them ends. Real Core
/// transports over loopback HTTP and TLS, so these are Core's behaviours, not a fake's.
/// </summary>
public sealed partial class SharedDeviceSearchTests
{
    private static ISettingsStore LiveSettings(AppSettings current) => Fakes.Settings(current);

    private static void RaiseChanged(ISettingsStore store) =>
        store.Changed += Raise.Event<EventHandler<AppSettings>>(store, store.Current);

    private static Task<IReadOnlyList<DesktopNMS.Core.Models.Device>> ListThrough(ILibreNmsTransport transport) =>
        new LibreNmsResourceApis(transport).Devices.ListAsync();

    [Fact]
    public async Task A_refused_certificate_reaches_the_prompt_and_trusting_it_lets_the_chat_through()
    {
        using var tls = new TlsLoopbackLibreNms();
        using var shared = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        var settings = LiveSettings(new AppSettings());
        using var transports = new IntelligenceDeviceTransports(shared, settings, Substitute.For<ISessionService>());

        var client = Substitute.For<ILibreNmsClient>();
        client.Failover.Returns(new ServerFailover());
        var session = Substitute.For<ISessionService>();
        session.IsConnected.Returns(true);
        session.TrustCertificate(default!, default).ReturnsForAnyArgs(true);
        // The answer waits until the first request has failed: Core retries a refused
        // handshake, and the prompt ignores repeats while it is open, as on a phone.
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(answer.Task);
        var prompt = new CertificatePrompt(client, session, dialogs, transports);
        var trusted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        prompt.Trusted += (_, _) => trusted.TrySetResult();
        prompt.Start();

        using var transport = transports.Open(new SessionScope(0, new LibreNmsConnection(tls.Root, "test-token", allowUntrustedCertificate: true)));
        var refused = await Assert.ThrowsAsync<LibreNmsApiException>(() => ListThrough(transport));
        Assert.Equal(tls.Fingerprint, refused.UntrustedCertificate?.Fingerprint, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(0, tls.Served);
        answer.SetResult(true);

        // The same prompt the session's own requests use asks, and accepting trusts the
        // certificate on the session and on this live transport, with no reconnect.
        await trusted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await dialogs.ReceivedWithAnyArgs(1).ConfirmAsync(default!, default!, default!, default!);
        session.Received(1).TrustCertificate(Arg.Is<CertificateDetails>(c => CertificateTrust.SameFingerprint(c.Fingerprint, tls.Fingerprint)), 0);
        var devices = await ListThrough(transport);
        Assert.Equal("tls.example.net", Assert.Single(devices).Hostname);
        Assert.Equal(tls.Root, transport.Connection!.WebRoot);
        Assert.Equal("test-token", transport.Connection!.ApiToken);
    }

    [Fact]
    public async Task Certificates_trusted_in_settings_apply_when_a_transport_opens_and_while_it_is_live()
    {
        using var tls = new TlsLoopbackLibreNms();
        using var shared = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        var current = new AppSettings();
        var settings = LiveSettings(current);
        using var transports = new IntelligenceDeviceTransports(shared, settings, Substitute.For<ISessionService>());
        var connection = new LibreNmsConnection(tls.Root, "test-token", allowUntrustedCertificate: true);

        using var live = transports.Open(new SessionScope(0, connection));
        await Assert.ThrowsAsync<LibreNmsApiException>(() => ListThrough(live));

        // Trusted elsewhere (desktop's shared settings, or the session's prompt): saved, then live.
        current.TrustedCertificates.Add(tls.Fingerprint);
        RaiseChanged(settings);
        Assert.Single(await ListThrough(live));

        // A transport opened afterwards starts with it, though the captured connection never had it.
        Assert.Empty(connection.TrustedCertificates);
        using var later = transports.Open(new SessionScope(0, connection));
        Assert.Single(await ListThrough(later));
    }

    [Fact]
    public void The_request_timeout_follows_settings_at_open_and_while_live_without_changing_server_or_token()
    {
        using var shared = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        var current = new AppSettings { TimeoutSeconds = 12 };
        var settings = LiveSettings(current);
        using var transports = new IntelligenceDeviceTransports(shared, settings, Substitute.For<ISessionService>());
        var connection = new LibreNmsConnection(new Uri("https://nms.example.net/"), "test-token", timeoutSeconds: 30,
            backupWebRoot: new Uri("https://nms-backup.example.net/"));

        using var transport = transports.Open(new SessionScope(0, connection));
        Assert.Equal(12, transport.Connection!.TimeoutSeconds);

        current.TimeoutSeconds = 45;
        RaiseChanged(settings);
        Assert.Equal(45, transport.Connection!.TimeoutSeconds);
        Assert.True(SessionScope.SameServer(connection, transport.Connection!));

        // The captured connection itself is never altered.
        Assert.Equal(30, connection.TimeoutSeconds);
    }

    [Fact]
    public async Task A_transport_starts_on_the_session_address_and_follows_failover_and_switching_back()
    {
        using var main = new LoopbackLibreNms("main.example.net");
        using var backup = new LoopbackLibreNms("backup.example.net");
        var connection = new LibreNmsConnection(main.Root, "test-token", backupWebRoot: backup.Root);
        var failover = new ServerFailover();
        using var shared = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance, failover);
        shared.Configure(connection, startOnBackup: true);
        using var transports = new IntelligenceDeviceTransports(shared, LiveSettings(new AppSettings()), Substitute.For<ISessionService>());

        using var transport = transports.Open(new SessionScope(0, connection));
        Assert.Equal("backup.example.net", Assert.Single(await ListThrough(transport)).Hostname);
        // Opening a chat's transport never resets the session's own failover state.
        Assert.True(failover.IsOnBackup);

        // Switched back by hand (Settings' "Switch back to the server address").
        Assert.True(failover.FailBack());
        Assert.Equal("main.example.net", Assert.Single(await ListThrough(transport)).Hostname);

        // The session fails over again on its own.
        for (var i = 0; i < ServerFailover.FailuresBeforeSwitch; i++) failover.RecordUnreachable();
        Assert.True(failover.IsOnBackup);
        Assert.Equal("backup.example.net", Assert.Single(await ListThrough(transport)).Hostname);

        Assert.Equal(2, backup.Requests.Count);
        Assert.Single(main.Requests);
        Assert.All(main.Requests.Concat(backup.Requests), r => Assert.Equal("test-token", r.Token));
    }

    [Fact]
    public async Task Transports_are_isolated_and_each_closes_on_its_own()
    {
        using var a = new LoopbackLibreNms("a.example.net");
        using var b = new LoopbackLibreNms("b.example.net");
        using var shared = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        var transports = new IntelligenceDeviceTransports(shared, LiveSettings(new AppSettings()), Substitute.For<ISessionService>());

        var first = transports.Open(new SessionScope(0, new LibreNmsConnection(a.Root, "token-a")));
        var second = transports.Open(new SessionScope(0, new LibreNmsConnection(b.Root, "token-b")));
        Assert.Equal(2, transports.LiveCount);
        Assert.Equal("a.example.net", Assert.Single(await ListThrough(first)).Hostname);
        Assert.Equal("b.example.net", Assert.Single(await ListThrough(second)).Hostname);

        first.Dispose();
        first.Dispose();
        Assert.Equal(1, transports.LiveCount);
        await Assert.ThrowsAnyAsync<Exception>(() => ListThrough(first));
        Assert.Equal("b.example.net", Assert.Single(await ListThrough(second)).Hostname);

        // Shutting the host down closes whatever is still open.
        transports.Dispose();
        Assert.Equal(0, transports.LiveCount);
        await Assert.ThrowsAnyAsync<Exception>(() => ListThrough(second));
        Assert.Throws<ObjectDisposedException>(() => transports.Open(new SessionScope(0, new LibreNmsConnection(a.Root, "token-a"))));

        Assert.Equal(new[] { ("/api/v0/devices", "token-a") }, a.Requests);
        Assert.Equal(new[] { ("/api/v0/devices", "token-b"), ("/api/v0/devices", "token-b") }, b.Requests);
    }

    [Fact]
    public async Task Disposing_an_owned_set_closes_its_transport_without_a_connection_switch()
    {
        var closed = 0;
        var nms = new FixtureLibreNms();
        var set = new CompactNetworkLookups(Fakes.Client(), Fakes.Settings(), Session(), deviceTransport: c =>
                new ClosingTransport(nms.Open(c), () => Interlocked.Increment(ref closed)))
            .Open(_ => { });
        var lookup = set.Lookups.Single(l => l.Name == "find_device");

        Assert.StartsWith("Matching devices: 10.", await lookup.Run("sw", default));
        Assert.Equal(0, closed);
        set.Dispose();
        set.Dispose();
        Assert.Equal(1, closed);

        // A late call on the disposed set fails without sending anything.
        Assert.Equal(CompactNetworkLookups.Failed, await lookup.Run("sw", default));
        Assert.Single(nms.Requests);
    }

    [Fact]
    public void A_set_that_never_searched_opens_and_closes_nothing()
    {
        var opened = 0;
        var nms = new FixtureLibreNms();
        using (new CompactNetworkLookups(Fakes.Client(), Fakes.Settings(), Session(), deviceTransport: c =>
               {
                   opened++;
                   return nms.Open(c);
               }).Open(_ => { }))
        {
        }
        Assert.Equal(0, opened);
    }

    /// <summary>
    /// A LibreNMS device list over TLS on a loopback port, with a self-signed certificate no
    /// trust store accepts, so only a fingerprint the person trusted lets a request through.
    /// </summary>
    private sealed class TlsLoopbackLibreNms : IDisposable
    {
        private static readonly byte[] Body = Encoding.UTF8.GetBytes(
            """{"status":"ok","count":1,"devices":[{"device_id":1,"hostname":"tls.example.net","ip":"192.0.2.10","status":1,"disabled":0,"ignore":0}]}""");

        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly X509Certificate2 certificate;
        private readonly CancellationTokenSource stop = new();
        private int served;

        public TlsLoopbackLibreNms()
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=tls.example.net", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            // Re-imported so the private key is usable by SslStream on every platform.
            certificate = X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pkcs12), null);
            listener.Start();
            Root = new Uri($"https://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
            _ = Task.Run(AcceptAsync);
        }

        public Uri Root { get; }

        public string Fingerprint => CertificateTrust.Fingerprint(certificate);

        /// <summary>Requests that got past the handshake and were answered.</summary>
        public int Served => Volatile.Read(ref served);

        private async Task AcceptAsync()
        {
            while (!stop.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(stop.Token); }
                catch (Exception) { return; }
                _ = Task.Run(() => ServeAsync(client));
            }
        }

        private async Task ServeAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    await using var tls = new SslStream(client.GetStream());
                    await tls.AuthenticateAsServerAsync(certificate);
                    var buffer = new byte[4096];
                    var head = new StringBuilder();
                    while (!head.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                    {
                        var read = await tls.ReadAsync(buffer);
                        if (read == 0) return;
                        head.Append(Encoding.ASCII.GetString(buffer, 0, read));
                    }
                    Interlocked.Increment(ref served);
                    var response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Body.Length}\r\nConnection: close\r\n\r\n";
                    await tls.WriteAsync(Encoding.ASCII.GetBytes(response));
                    await tls.WriteAsync(Body);
                    await tls.FlushAsync();
                }
                catch (Exception)
                {
                    // A refused handshake ends here: the client turned the certificate down.
                }
            }
        }

        public void Dispose()
        {
            stop.Cancel();
            listener.Stop();
            certificate.Dispose();
            stop.Dispose();
        }
    }
}
