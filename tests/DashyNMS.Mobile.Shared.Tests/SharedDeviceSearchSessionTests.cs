using System.Net;
using System.Net.Sockets;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// A chat is bound to one connection by session generation (#611's re-review), not by the
/// connection object Core replaces whenever a certificate is trusted or the timeout changes.
/// Trust, timeout and failover keep the chat; sign-in, a switch or sign-out ends it. Failover
/// the chat's transport detects is the session's to make, and a certificate met before a
/// switch is never trusted after it.
/// </summary>
/// <remarks>
/// The session is the app's real <see cref="SessionService"/>. Its client is modelled on
/// Core's: <c>TrustCertificate</c> and <c>SetTimeout</c> give it a new connection object for
/// the same server, as <c>LibreNmsTransport.Install</c> does, which is what broke chats that
/// compared references. Chat transports are real Core transports over loopback HTTP and TLS.
/// </remarks>
public sealed partial class SharedDeviceSearchTests
{
    /// <summary>A client that behaves as Core's does where it matters here, without a server to sign in to.</summary>
    private sealed class ModelledClient
    {
        private LibreNmsConnection? current;

        public ModelledClient()
        {
            Client.Failover.Returns(Failover);
            Client.Connection.Returns(_ => Volatile.Read(ref current));
            Client.IsConnected.Returns(_ => Volatile.Read(ref current) is not null);
            Client.TestAsync(default!, default).ReturnsForAnyArgs(ConnectionTestResult.Success(new SystemInfo()));
            Client.When(c => c.Connect(Arg.Any<LibreNmsConnection>(), Arg.Any<bool>())).Do(call =>
            {
                var connection = call.ArgAt<LibreNmsConnection>(0);
                Failover.Configure(connection.BackupWebRoot?.ToString(), call.ArgAt<bool>(1));
                Volatile.Write(ref current, connection);
            });
            Client.When(c => c.Disconnect()).Do(_ => Volatile.Write(ref current, null));
            // As LibreNmsTransport.Install: the same server, a new connection object.
            Client.When(c => c.TrustCertificate(Arg.Any<string>())).Do(call =>
                Volatile.Write(ref current, Volatile.Read(ref current)?.WithTrustedCertificate(call.ArgAt<string>(0))));
            Client.When(c => c.SetTimeout(Arg.Any<int>())).Do(call =>
                Volatile.Write(ref current, Volatile.Read(ref current)?.WithTimeout(call.ArgAt<int>(0))));
        }

        public ILibreNmsClient Client { get; } = Substitute.For<ILibreNmsClient>();

        /// <summary>The session's failover, as the shared transport's would be.</summary>
        public ServerFailover Failover { get; } = new();
    }

    /// <summary>The app's session and the pieces around it, as the container wires them.</summary>
    private sealed class App : IDisposable
    {
        public App(AppSettings? current = null)
        {
            Current = current ?? new AppSettings();
            Settings = LiveSettings(Current);
            Session = new SessionService(Modelled.Client, Settings, Substitute.For<ITokenProtector>(), NullLogger<SessionService>.Instance);
            Shared = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance, Modelled.Failover);
            Transports = new IntelligenceDeviceTransports(Shared, Settings, Session);
        }

        public AppSettings Current { get; }
        public ISettingsStore Settings { get; }
        public ModelledClient Modelled { get; } = new();
        public SessionService Session { get; }
        public LibreNmsTransport Shared { get; }
        public IntelligenceDeviceTransports Transports { get; }

        public Task SignInAsync(Uri root, string token, Uri? backup = null) =>
            Session.SignInAsync(root.ToString(), token, allowUntrustedCertificate: true, rememberToken: false, backupAddress: backup?.ToString());

        public IntelligenceConversationViewModel Conversation(Func<IReadOnlyList<IntelligenceLookup>, CancellationToken, Task<IntelligenceAnswer>> script)
        {
            var store = Substitute.For<ICheckedSettingsStore>();
            store.Current.Returns(new AppSettings());
            store.TrySave().Returns(true);
            var preferences = new InMemoryPreferences();
            preferences.Set(IntelligencePreferences.NoticeKey, "accepted");
            var lookups = new CompactNetworkLookups(Modelled.Client, Settings, Session, TimeProvider.System, Transports.Open);
            // Inline: what the page would post to the UI thread happens before AskAsync returns.
            return new IntelligenceConversationViewModel(new ScriptedModel(script), new IntelligencePreferences(store, preferences), lookups, Session,
                dispatch: action => action());
        }

        public void Dispose()
        {
            Transports.Dispose();
            Shared.Dispose();
        }
    }

    /// <summary>An on-device model that runs one script per question, calling the chat's lookups as tools.</summary>
    private sealed class ScriptedModel(Func<IReadOnlyList<IntelligenceLookup>, CancellationToken, Task<IntelligenceAnswer>> script) : IIntelligenceModel
    {
        public IntelligenceAvailability Availability => IntelligenceAvailability.Available;
        public int? ContextSize => 4096;
        public Task<int?> CountTokensAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
        public IIntelligenceSession StartSession(string instructions, IReadOnlyList<IntelligenceLookup> lookups) => new Session(lookups, script);

        private sealed class Session(IReadOnlyList<IntelligenceLookup> tools, Func<IReadOnlyList<IntelligenceLookup>, CancellationToken, Task<IntelligenceAnswer>> script)
            : IIntelligenceSession
        {
            public Task<IntelligenceAnswer> AskAsync(string question, Action<string> onText, CancellationToken cancellationToken = default) =>
                script(tools, cancellationToken);

            public void Dispose() { }
        }
    }

    private static Task<string> FindDevice(IReadOnlyList<IntelligenceLookup> tools, string query, CancellationToken token) =>
        tools.Single(t => t.Name == "find_device").Run(query, token);

    /// <summary>An address on loopback nothing listens on: every request is refused, so it counts as unreachable.</summary>
    private static Uri Unreachable()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return new Uri($"http://127.0.0.1:{port}/");
    }

    private static readonly CertificateDetails SomeCertificate = new(
        "nms.example.net", "AA:BB:CC:DD", "CN=nms.example.net", "CN=Example Internal CA",
        DateTime.Today.AddDays(-30), DateTime.Today.AddYears(1),
        System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors, ReplacesTrustedCertificate: false);

    [Fact]
    public async Task Trust_and_timeout_keep_the_scope_and_sign_in_or_sign_out_end_it()
    {
        using var app = new App(new AppSettings { TimeoutSeconds = 20 });
        await app.SignInAsync(new Uri("https://nms.example.net/"), "test-token");
        var scope = SessionScope.Capture(app.Session)!.Value;

        app.Session.TrustCertificate(SomeCertificate);
        Assert.NotSame(scope.Connection, app.Session.Connection);
        Assert.True(scope.IsCurrent(app.Session));

        app.Current.TimeoutSeconds = 45;
        RaiseChanged(app.Settings);
        Assert.Equal(45, app.Session.Connection!.TimeoutSeconds);
        Assert.True(scope.IsCurrent(app.Session));

        // Signing in again, even to the same server, is a new connection.
        await app.SignInAsync(new Uri("https://nms.example.net/"), "test-token");
        Assert.False(scope.IsCurrent(app.Session));
        var again = SessionScope.Capture(app.Session)!.Value;
        Assert.True(again.Generation > scope.Generation);

        app.Session.SignOut(forgetToken: false);
        Assert.False(again.IsCurrent(app.Session));
        Assert.Null(SessionScope.Capture(app.Session));
    }

    /// <summary>
    /// The whole chat, as the person sees it: find_device meets the server's unknown
    /// certificate, the app asks, they accept, and the model's next lookup gets through in
    /// the same answer. Accepting replaces the session's connection object; the chat carries on.
    /// </summary>
    [Fact]
    public async Task Accepting_a_certificate_mid_answer_lets_the_retried_lookup_through_and_keeps_the_chat()
    {
        using var tls = new TlsLoopbackLibreNms();
        using var app = new App();
        await app.SignInAsync(tls.Root, "test-token");
        var signedIn = app.Session.Connection;

        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(answer.Task);
        var prompt = new CertificatePrompt(app.Modelled.Client, app.Session, dialogs, app.Transports);
        var trusted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        prompt.Trusted += (_, _) => trusted.TrySetResult();
        prompt.Start();

        string? first = null, retried = null;
        using var vm = app.Conversation(async (tools, token) =>
        {
            first = await FindDevice(tools, "tls", token);
            answer.TrySetResult(true); // The person taps Trust once the lookup has failed.
            await trusted.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
            retried = await FindDevice(tools, "tls", token);
            return new IntelligenceAnswer("Found it", null);
        });

        Assert.True(await vm.AskAsync("Where is the TLS switch?").WaitAsync(TimeSpan.FromSeconds(30)));

        Assert.True(CompactNetworkLookups.IsFailure(first!));
        Assert.NotEqual(CompactNetworkLookups.Changed, first);
        Assert.Contains("tls.example.net [[device:1]]: Up", retried);
        var turn = Assert.Single(vm.Turns);
        Assert.Equal(IntelligenceTurnState.Completed, turn.State);
        Assert.Equal("Found it", turn.Answer);
        Assert.Equal(IntelligenceLookupState.Completed, turn.Lookups[^1].State);
        Assert.True(vm.IsTranscriptCurrent);
        Assert.Equal(0, vm.ChatRevision);
        Assert.DoesNotContain("connection changed", vm.Notice, StringComparison.OrdinalIgnoreCase);

        // The session's connection object was replaced by the trust, on the same server.
        Assert.NotSame(signedIn, app.Session.Connection);
        Assert.Contains(app.Session.Connection!.TrustedCertificates, f => CertificateTrust.SameFingerprint(f, tls.Fingerprint));

        // And the chat goes on: the next question is asked in the same chat.
        Assert.True(await vm.AskAsync("And again?").WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.Equal(2, vm.Turns.Count);
    }

    [Fact]
    public async Task Changing_the_timeout_mid_answer_keeps_the_chat_and_its_lookups()
    {
        using var nms = new LoopbackLibreNms("timeout.example.net");
        using var app = new App(new AppSettings { TimeoutSeconds = 20 });
        await app.SignInAsync(nms.Root, "test-token");

        string? before = null, after = null;
        using var vm = app.Conversation(async (tools, token) =>
        {
            before = await FindDevice(tools, "timeout", token);
            // Settings' Request timeout, saved while the answer runs.
            app.Current.TimeoutSeconds = 45;
            RaiseChanged(app.Settings);
            after = await FindDevice(tools, "timeout", token);
            return new IntelligenceAnswer("Still here", null);
        });

        Assert.True(await vm.AskAsync("Is it up?").WaitAsync(TimeSpan.FromSeconds(30)));

        Assert.Contains("timeout.example.net [[device:1]]: Up", before);
        Assert.Contains("timeout.example.net [[device:1]]: Up", after);
        Assert.Equal(45, app.Session.Connection!.TimeoutSeconds);
        var turn = Assert.Single(vm.Turns);
        Assert.Equal(IntelligenceTurnState.Completed, turn.State);
        Assert.All(turn.Lookups, l => Assert.Equal(IntelligenceLookupState.Completed, l.State));
        Assert.True(vm.IsTranscriptCurrent);
    }

    [Fact]
    public async Task Signing_in_again_mid_answer_still_ends_the_chat()
    {
        using var nms = new LoopbackLibreNms("resign.example.net");
        using var app = new App();
        await app.SignInAsync(nms.Root, "test-token");

        using var vm = app.Conversation(async (tools, token) =>
        {
            await FindDevice(tools, "resign", token);
            // The same server and token: only the generation says this is a new connection.
            await app.SignInAsync(nms.Root, "test-token");
            await FindDevice(tools, "resign", token);
            return new IntelligenceAnswer("Should not be shown", null);
        });

        await vm.AskAsync("Question").WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Empty(vm.Turns);
        Assert.Equal("The connection changed. The previous chat was cleared.", vm.Notice);
        Assert.Single(nms.Requests);
    }

    /// <summary>
    /// Failover is the session's (#611's re-review): when a chat's own transport can't reach the
    /// server, the session moves to its backup, and every chat transport follows it there.
    /// </summary>
    [Fact]
    public async Task A_chat_transport_that_cannot_reach_the_server_moves_the_session_and_other_chats_follow()
    {
        using var backup = new LoopbackLibreNms("backup.example.net");
        using var app = new App();
        await app.SignInAsync(Unreachable(), "test-token", backup.Root);
        Assert.False(app.Modelled.Failover.IsOnBackup);
        var scope = SessionScope.Capture(app.Session)!.Value;
        using var first = app.Transports.Open(scope);
        using var second = app.Transports.Open(scope);

        // The first unreachable request counts; the second tips it over and is retried on the backup.
        await Assert.ThrowsAnyAsync<Exception>(() => ListThrough(first));
        Assert.False(app.Modelled.Failover.IsOnBackup);
        Assert.Equal("backup.example.net", Assert.Single(await ListThrough(first)).Hostname);

        // The session moved, with its own backup address, and so did the other chat.
        Assert.True(app.Modelled.Failover.IsOnBackup);
        Assert.Equal(app.Session.Connection!.BackupWebRoot!.ToString(), app.Modelled.Failover.BackupAddress);
        Assert.True(scope.IsCurrent(app.Session));
        Assert.Equal("backup.example.net", Assert.Single(await ListThrough(second)).Hostname);
        Assert.Equal(2, backup.Requests.Count);
    }

    [Fact]
    public async Task A_chat_for_an_earlier_connection_cannot_move_the_session_to_its_backup()
    {
        using var backup = new LoopbackLibreNms("backup.example.net");
        using var app = new App();
        var main = Unreachable();
        await app.SignInAsync(main, "test-token", backup.Root);
        var stale = SessionScope.Capture(app.Session)!.Value;
        using var transport = app.Transports.Open(stale);

        // Signed in again before the old chat's requests fail.
        await app.SignInAsync(main, "test-token", backup.Root);
        Assert.False(stale.IsCurrent(app.Session));

        await Assert.ThrowsAnyAsync<Exception>(() => ListThrough(transport));
        Assert.Single(await ListThrough(transport)); // Its own transport fails over, as Core's does.

        // The session, now on a later generation, stays where it was.
        Assert.False(app.Modelled.Failover.IsOnBackup);
        Assert.False(app.Session.SwitchToBackup(stale.Generation));
        Assert.False(app.Modelled.Failover.IsOnBackup);
        Assert.True(app.Session.SwitchToBackup(app.Session.ConnectionGeneration));
        Assert.True(app.Modelled.Failover.IsOnBackup);
    }

    [Fact]
    public async Task A_prompt_still_open_when_the_session_switches_trusts_nothing()
    {
        using var app = new App();
        await app.SignInAsync(new Uri("https://a.example.net/"), "token-a");
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(answer.Task);
        var prompt = new CertificatePrompt(app.Modelled.Client, app.Session, dialogs, app.Transports);
        var reloads = 0;
        prompt.Trusted += (_, _) => reloads++;

        var asking = prompt.AskAsync(SomeCertificate, app.Session.ConnectionGeneration, onBackup: false);
        await app.SignInAsync(new Uri("https://b.example.net/"), "token-b");
        answer.SetResult(true);

        Assert.False(await asking);
        app.Modelled.Client.DidNotReceiveWithAnyArgs().TrustCertificate(default!);
        Assert.Empty(app.Current.TrustedCertificates);
        Assert.Empty(app.Session.Connection!.TrustedCertificates);
        Assert.Equal(0, reloads);

        // Not recorded as declined either: met on B, it is asked about afresh.
        dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        Assert.True(await prompt.AskAsync(SomeCertificate, app.Session.ConnectionGeneration, onBackup: false));
        Assert.Equal(1, reloads);
    }

    [Fact]
    public async Task A_late_refusal_from_an_earlier_connection_is_not_asked_about()
    {
        using var tls = new TlsLoopbackLibreNms();
        using var app = new App();
        await app.SignInAsync(tls.Root, "test-token");
        var dialogs = Substitute.For<IDialogService>();
        var prompt = new CertificatePrompt(app.Modelled.Client, app.Session, dialogs, app.Transports);
        prompt.Start();
        using var stale = app.Transports.Open(SessionScope.Capture(app.Session)!.Value);

        // The chat's request is refused only after the session has signed in elsewhere.
        await app.SignInAsync(new Uri("https://other.example.net/"), "other-token");
        await Assert.ThrowsAsync<LibreNmsApiException>(() => ListThrough(stale));

        await dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
        Assert.Empty(app.Current.TrustedCertificates);
    }

    [Fact]
    public async Task Trust_accepted_for_one_generation_reaches_only_its_own_chat_transports()
    {
        using var tls = new TlsLoopbackLibreNms();
        using var app = new App();
        var connection = new LibreNmsConnection(tls.Root, "test-token", allowUntrustedCertificate: true);
        using var earlier = app.Transports.Open(new SessionScope(1, connection));
        using var current = app.Transports.Open(new SessionScope(2, connection));

        app.Transports.TrustCertificate(tls.Fingerprint, 2);

        Assert.Single(await ListThrough(current));
        await Assert.ThrowsAsync<LibreNmsApiException>(() => ListThrough(earlier));
    }
}
