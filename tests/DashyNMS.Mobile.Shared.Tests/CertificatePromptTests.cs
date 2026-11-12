using System.Net.Security;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// A certificate met while signed in - the backup address's, the first time
/// the app fails over to it - is asked about there and then (#141), as desktop does.
/// </summary>
public sealed class CertificatePromptTests
{
    private static readonly CertificateDetails Backup = new(
        "10.44.100.11",
        "12:34:56:78",
        "CN=nms-backup",
        "CN=Example Internal CA",
        DateTime.Today.AddDays(-30),
        DateTime.Today.AddYears(1),
        SslPolicyErrors.RemoteCertificateChainErrors,
        ReplacesTrustedCertificate: false);

    /// <summary>The session's connection: Backup's host is its backup address.</summary>
    private static readonly LibreNmsConnection Server = new(
        new Uri("https://nms.example.net/"), "test-token", backupWebRoot: new Uri("https://10.44.100.11/"));

    private readonly ILibreNmsClient _client = Substitute.For<ILibreNmsClient>();
    private readonly ISessionService _session = Fakes.Session();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IConnectionCertificateProbe _probe = Substitute.For<IConnectionCertificateProbe>();
    private readonly ServerFailover _failover = new();

    public CertificatePromptTests()
    {
        _client.Failover.Returns(_failover);
        _session.IsConnected.Returns(true);
        _session.Connection.Returns(Server);
        _session.TrustCertificate(default!, default).ReturnsForAnyArgs(true);
        // The connection in use refuses Backup on its backup address, for whichever
        // generation it is probed for.
        Probes(Backup, onBackup: true);
    }

    private CertificatePrompt NewPrompt() => new(_client, _session, _dialogs, probe: _probe);

    /// <summary>What a fresh request to the probed connection is refused on.</summary>
    private void Probes(CertificateDetails? refused, bool onBackup = false) =>
        _probe.ProbeAsync(Arg.Any<SessionScope>(), Arg.Any<CancellationToken>()).Returns(call =>
            Task.FromResult(refused is null ? null : new ScopedCertificateRejection(refused, call.ArgAt<SessionScope>(0).Generation, onBackup)));

    [Fact]
    public async Task Accepting_trusts_it_names_the_backup_and_reloads()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var prompt = NewPrompt();
        var reloads = 0;
        prompt.Trusted += (_, _) => reloads++;

        Assert.True(await prompt.AskAsync(Backup));

        var (title, message) = CertificateTrust.DescribeForPrompt(Backup, "LibreNMS (backup address)");
        await _dialogs.Received(1).ConfirmAsync(title, message, "Trust", "Cancel");
        _session.Received(1).TrustCertificate(Backup, 0);
        Assert.Equal(1, reloads);
    }

    [Fact]
    public async Task A_burst_of_failures_asks_once()
    {
        var answer = new TaskCompletionSource<bool>();
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(answer.Task);
        var prompt = NewPrompt();

        // Every request failing on the certificate at once, while the first prompt is open.
        var first = prompt.AskAsync(Backup);
        var rest = Enumerable.Range(0, 5).Select(_ => prompt.AskAsync(Backup)).ToList();
        Assert.All(await Task.WhenAll(rest), Assert.False);

        answer.SetResult(true);
        Assert.True(await first);
        await _dialogs.ReceivedWithAnyArgs(1).ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task A_declined_certificate_isnt_asked_about_again()
    {
        var prompt = NewPrompt();

        Assert.False(await prompt.AskAsync(Backup)); // declined: the substitute answers false
        Assert.False(await prompt.AskAsync(Backup));

        await _dialogs.ReceivedWithAnyArgs(1).ConfirmAsync(default!, default!, default!, default!);
        _session.DidNotReceiveWithAnyArgs().TrustCertificate(default!, default);
    }

    [Fact]
    public async Task A_decline_answers_for_one_connection_and_the_next_asks_again()
    {
        var prompt = NewPrompt();
        Assert.False(await prompt.AskAsync(Backup)); // declined on generation 0

        // Signed in again: the same certificate, reported or met by the probe, is asked about afresh.
        _session.ConnectionGeneration.Returns(1);
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        Assert.True(await prompt.AskAsync(Backup));

        await _dialogs.ReceivedWithAnyArgs(2).ConfirmAsync(default!, default!, default!, default!);
        _session.Received(1).TrustCertificate(Backup, 1);
    }

    [Fact]
    public async Task Signed_out_it_leaves_it_to_sign_in()
    {
        _session.IsConnected.Returns(false);
        _session.Connection.Returns((LibreNmsConnection?)null);

        Assert.False(await NewPrompt().AskAsync(Backup));
        Assert.False(await NewPrompt().AskAsync(Backup, 0, onBackup: true));

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Listens_to_the_client_once_started()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var prompt = NewPrompt();
        var trusted = new TaskCompletionSource();
        prompt.Trusted += (_, _) => trusted.TrySetResult();
        prompt.Start();

        _client.CertificateRejected += Raise.Event<EventHandler<CertificateDetails>>(_client, Backup);

        await trusted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _session.Received(1).TrustCertificate(Backup, 0);
    }

    [Fact]
    public async Task A_late_refusal_from_the_previous_server_isnt_asked_about()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var prompt = NewPrompt();
        prompt.Start();

        // Signed in elsewhere: the session is on a new server and generation when a request
        // still in flight on the previous one fails on that server's certificate.
        _session.ConnectionGeneration.Returns(2);
        _session.Connection.Returns(new LibreNmsConnection(new Uri("https://new.example.net/"), "new-token"));
        _client.CertificateRejected += Raise.Event<EventHandler<CertificateDetails>>(_client, Backup);

        Assert.False(await prompt.AskAsync(Backup));
        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
        _session.DidNotReceiveWithAnyArgs().TrustCertificate(default!, default);
        // Another server's host isn't worth a request.
        await _probe.DidNotReceiveWithAnyArgs().ProbeAsync(default, default);
    }

    [Fact]
    public async Task A_refusal_is_asked_about_for_the_generation_the_probe_was_made_for()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        _session.ConnectionGeneration.Returns(7);
        var main = Backup with { Host = "nms.example.net" };
        Probes(main, onBackup: false);

        Assert.True(await NewPrompt().AskAsync(main));

        await _probe.Received(1).ProbeAsync(new SessionScope(7, Server), Arg.Any<CancellationToken>());
        var (title, message) = CertificateTrust.DescribeForPrompt(main, "LibreNMS");
        await _dialogs.Received(1).ConfirmAsync(title, message, "Trust", "Cancel");
        _session.Received(1).TrustCertificate(main, 7);
    }

    [Fact]
    public async Task The_backup_is_named_from_the_probed_address_not_the_failover_state()
    {
        // The session's failover is on the main address, but the probe dialled the backup.
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);

        Assert.True(await NewPrompt().AskAsync(Backup));

        var (title, message) = CertificateTrust.DescribeForPrompt(Backup, "LibreNMS (backup address)");
        await _dialogs.Received(1).ConfirmAsync(title, message, "Trust", "Cancel");
    }

    /// <summary>
    /// #611's review: the client's refusal can come from an earlier connection's request, the
    /// same host on another port or a certificate since rotated. Only what the connection in
    /// use presents now is asked about.
    /// </summary>
    [Fact]
    public async Task The_reported_certificate_is_never_asked_about_only_what_the_probe_meets()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var now = Backup with { Fingerprint = "9A:8B:7C:6D", Subject = "CN=nms-backup-renewed" };
        Probes(now, onBackup: true);

        Assert.True(await NewPrompt().AskAsync(Backup));

        var (title, message) = CertificateTrust.DescribeForPrompt(now, "LibreNMS (backup address)");
        await _dialogs.Received(1).ConfirmAsync(title, message, "Trust", "Cancel");
        _session.Received(1).TrustCertificate(now, 0);
        _session.DidNotReceive().TrustCertificate(Arg.Is(Backup), Arg.Any<long>());
    }

    [Fact]
    public async Task Nothing_is_asked_when_the_connection_in_use_gets_through()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        Probes(null);

        Assert.False(await NewPrompt().AskAsync(Backup));

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
        _session.DidNotReceiveWithAnyArgs().TrustCertificate(default!, default);
    }

    [Fact]
    public async Task Without_a_probe_a_refusal_that_names_no_connection_is_dropped()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);

        Assert.False(await new CertificatePrompt(_client, _session, _dialogs).AskAsync(Backup));

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task A_refusal_while_probing_earns_one_more_probe()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var firstProbe = new TaskCompletionSource<ScopedCertificateRejection?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probes = 0;
        _probe.ProbeAsync(Arg.Any<SessionScope>(), Arg.Any<CancellationToken>()).Returns(call =>
            Interlocked.Increment(ref probes) == 1
                ? firstProbe.Task
                : Task.FromResult<ScopedCertificateRejection?>(new(Backup, call.ArgAt<SessionScope>(0).Generation, true)));
        var prompt = NewPrompt();

        // The first probe got through (the switch hadn't happened yet); a refusal arrived meanwhile.
        var first = prompt.AskAsync(Backup);
        Assert.False(await prompt.AskAsync(Backup));
        firstProbe.SetResult(null);

        Assert.True(await first);
        Assert.Equal(2, probes);
        _session.Received(1).TrustCertificate(Backup, 0);
    }

    [Theory]
    [InlineData("nms.example.net", false)]
    [InlineData("NMS.Example.Net", false)]
    [InlineData("10.44.100.11", true)]
    [InlineData("other.example.net", null)]
    [InlineData("", null)]
    public void Only_the_connections_own_addresses_bind_a_refusal(string host, bool? onBackup) =>
        Assert.Equal(onBackup, CertificatePrompt.Dialled(Server, host));
}
