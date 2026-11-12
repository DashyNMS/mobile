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
    private readonly ServerFailover _failover = new();

    public CertificatePromptTests()
    {
        _client.Failover.Returns(_failover);
        _session.IsConnected.Returns(true);
        _session.Connection.Returns(Server);
        _session.TrustCertificate(default!, default).ReturnsForAnyArgs(true);
    }

    private CertificatePrompt NewPrompt() => new(_client, _session, _dialogs);

    [Fact]
    public async Task Accepting_trusts_it_names_the_backup_and_reloads()
    {
        _failover.Configure("https://10.44.100.11/", startOnBackup: true);
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
    public async Task Signed_out_it_leaves_it_to_sign_in()
    {
        _session.IsConnected.Returns(false);

        Assert.False(await NewPrompt().AskAsync(Backup));

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
    }

    [Fact]
    public async Task A_refusal_is_asked_about_for_the_generation_read_with_its_connection()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        _session.ConnectionGeneration.Returns(7);
        var main = Backup with { Host = "nms.example.net" };

        Assert.True(await NewPrompt().AskAsync(main));

        var (title, message) = CertificateTrust.DescribeForPrompt(main, "LibreNMS");
        await _dialogs.Received(1).ConfirmAsync(title, message, "Trust", "Cancel");
        _session.Received(1).TrustCertificate(main, 7);
    }

    [Fact]
    public async Task The_backup_is_named_from_the_refused_host_not_the_failover_state()
    {
        // The session's failover is on the main address, but the refusal came from the backup.
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);

        Assert.True(await NewPrompt().AskAsync(Backup));

        var (title, message) = CertificateTrust.DescribeForPrompt(Backup, "LibreNMS (backup address)");
        await _dialogs.Received(1).ConfirmAsync(title, message, "Trust", "Cancel");
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
