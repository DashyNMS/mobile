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
/// #611's independent review. The session client's certificate refusal names only a host and a
/// certificate, so a request from the previous connection - the same host on another port, or
/// the same address before its certificate was rotated - could be taken for the new session's
/// and its certificate trusted there. And a chat transport, published before it was configured,
/// could have a concurrent settings or failover change overwritten by the older state it read.
/// </summary>
public sealed partial class SharedDeviceSearchTests
{
    /// <summary>A prompt that accepts whatever it is shown, not started: tests hand it refusals themselves.</summary>
    private static (CertificatePrompt Prompt, IDialogService Dialogs) AcceptingPrompt(App app)
    {
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        return (new CertificatePrompt(app.Modelled.Client, app.Session, dialogs, app.Transports), dialogs);
    }

    /// <summary>The certificate a request made on <paramref name="scope"/>'s connection is refused on.</summary>
    private static async Task<CertificateDetails> RefusedOn(App app, SessionScope scope)
    {
        using var transport = app.Transports.Open(scope);
        var refused = await Assert.ThrowsAsync<LibreNmsApiException>(() => ListThrough(transport));
        return refused.UntrustedCertificate!;
    }

    private static void AssertTrustedOnly(App app, string expected, string stale)
    {
        Assert.Contains(app.Current.TrustedCertificates, f => CertificateTrust.SameFingerprint(f, expected));
        Assert.DoesNotContain(app.Current.TrustedCertificates, f => CertificateTrust.SameFingerprint(f, stale));
        app.Modelled.Client.Received(1).TrustCertificate(Arg.Is<string>(f => CertificateTrust.SameFingerprint(f, expected)));
        app.Modelled.Client.DidNotReceive().TrustCertificate(Arg.Is<string>(f => CertificateTrust.SameFingerprint(f, stale)));
    }

    [Fact]
    public async Task A_late_refusal_from_the_same_host_on_another_port_trusts_only_the_new_servers_certificate()
    {
        using var before = new TlsLoopbackLibreNms();
        using var after = new TlsLoopbackLibreNms();
        Assert.Equal(before.Root.Host, after.Root.Host);
        Assert.NotEqual(before.Root.Port, after.Root.Port);
        using var app = new App();
        var (prompt, dialogs) = AcceptingPrompt(app);
        await app.SignInAsync(before.Root, "token-a");
        var stale = await RefusedOn(app, SessionScope.Capture(app.Session)!.Value);
        Assert.True(CertificateTrust.SameFingerprint(before.Fingerprint, stale.Fingerprint));

        // Signed in on the other port before the earlier request's refusal is delivered. Its
        // host is one the new connection dials, so a host check took it for the new session's.
        await app.SignInAsync(after.Root, "token-b");
        Assert.True(await prompt.AskAsync(stale));

        await dialogs.ReceivedWithAnyArgs(1).ConfirmAsync(default!, default!, default!, default!);
        AssertTrustedOnly(app, after.Fingerprint, before.Fingerprint);
        using var chat = app.Transports.Open(SessionScope.Capture(app.Session)!.Value);
        Assert.Single(await ListThrough(chat));
    }

    [Fact]
    public async Task A_late_refusal_from_before_a_certificate_rotation_trusts_only_the_renewed_certificate()
    {
        using var server = new TlsLoopbackLibreNms();
        using var app = new App();
        var (prompt, dialogs) = AcceptingPrompt(app);
        await app.SignInAsync(server.Root, "test-token");
        var stale = await RefusedOn(app, SessionScope.Capture(app.Session)!.Value);
        var old = server.Fingerprint;

        // Renewed, and signed in again at the very same address and port, before the earlier
        // request's refusal is delivered.
        server.Rotate();
        Assert.False(CertificateTrust.SameFingerprint(old, server.Fingerprint));
        await app.SignInAsync(server.Root, "test-token");
        Assert.True(await prompt.AskAsync(stale));

        await dialogs.ReceivedWithAnyArgs(1).ConfirmAsync(default!, default!, default!, default!);
        AssertTrustedOnly(app, server.Fingerprint, old);
    }

    [Fact]
    public async Task A_late_refusal_is_dropped_when_the_connection_in_use_already_gets_through()
    {
        using var before = new TlsLoopbackLibreNms();
        using var after = new TlsLoopbackLibreNms();
        using var app = new App();
        var (prompt, dialogs) = AcceptingPrompt(app);
        await app.SignInAsync(before.Root, "token-a");
        var stale = await RefusedOn(app, SessionScope.Capture(app.Session)!.Value);

        app.Current.TrustedCertificates.Add(after.Fingerprint);
        await app.SignInAsync(after.Root, "token-b");
        Assert.False(await prompt.AskAsync(stale));

        await dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
        Assert.DoesNotContain(app.Current.TrustedCertificates, f => CertificateTrust.SameFingerprint(f, before.Fingerprint));
        app.Modelled.Client.DidNotReceiveWithAnyArgs().TrustCertificate(default!);
    }

    [Fact]
    public async Task Settings_saved_while_a_transport_is_being_configured_are_the_ones_it_keeps()
    {
        var current = new AppSettings { TimeoutSeconds = 20 };
        var settings = LiveSettings(current);
        using var shared = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var transports = new IntelligenceDeviceTransports(shared, settings, Fakes.Session());
        var fingerprint = string.Join(':', Enumerable.Repeat("CD", 32));
        Task? saved = null;
        transports.BeforeConfigure = () =>
        {
            transports.BeforeConfigure = null;
            // Saved on another thread after Open read the settings and before it applied them.
            current.TimeoutSeconds = 45;
            current.TrustedCertificates.Add(fingerprint);
            saved = Task.Run(() => RaiseChanged(settings));
            // Every chance to land first: unserialised it did, and Configure then put back what it read.
            saved.Wait(TimeSpan.FromMilliseconds(500));
        };

        using var transport = transports.Open(new SessionScope(0, new LibreNmsConnection(new Uri("https://nms.example.net/"), "test-token")));
        await saved!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(45, transport.Connection!.TimeoutSeconds);
        Assert.Contains(transport.Connection!.TrustedCertificates, f => CertificateTrust.SameFingerprint(f, fingerprint));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_session_failover_while_a_transport_is_being_configured_leaves_it_where_the_session_is(bool sessionStartsOnBackup)
    {
        const string BackupAddress = "https://nms-backup.example.net/";
        var sessionFailover = new ServerFailover();
        sessionFailover.Configure(BackupAddress, sessionStartsOnBackup);
        using var shared = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance, sessionFailover);
        using var transports = new IntelligenceDeviceTransports(shared, LiveSettings(new AppSettings()), Fakes.Session());
        var connection = new LibreNmsConnection(new Uri("https://nms.example.net/"), "test-token", backupWebRoot: new Uri(BackupAddress));
        Task? moved = null;
        transports.BeforeConfigure = () =>
        {
            transports.BeforeConfigure = null;
            // The session moves after Open read where it was and before Open applied it.
            moved = Task.Run(() =>
            {
                if (sessionStartsOnBackup) sessionFailover.FailBack();
                else sessionFailover.Configure(BackupAddress, startOnBackup: true);
            });
            moved.Wait(TimeSpan.FromMilliseconds(500));
        };

        using var transport = transports.Open(new SessionScope(0, connection));
        await moved!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(!sessionStartsOnBackup, sessionFailover.IsOnBackup);
        Assert.Equal(sessionFailover.IsOnBackup, transport.OnBackup);
        Assert.True(SessionScope.SameServer(connection, transport.Connection!));
    }
}
