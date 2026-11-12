using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// A captured scope is always one connection's generation with that connection (#611's
/// re-review). Read one after the other, a switch in between paired the new generation with
/// the old connection, and a reconnect to the same server with a different certificate
/// policy then passed as current while carrying the old policy.
/// </summary>
public sealed class SessionScopeTests
{
    private readonly ILibreNmsClient _client = Substitute.For<ILibreNmsClient>();
    private LibreNmsConnection? _current;

    /// <summary>Runs inside the switch, before the client takes the new connection.</summary>
    private Action? _duringSwitch;

    public SessionScopeTests()
    {
        _client.When(c => c.Connect(Arg.Any<LibreNmsConnection>(), Arg.Any<bool>())).Do(call =>
        {
            _duringSwitch?.Invoke();
            Volatile.Write(ref _current, call.ArgAt<LibreNmsConnection>(0));
        });
        _client.When(c => c.Disconnect()).Do(_ => Volatile.Write(ref _current, null));
        _client.Connection.Returns(_ => Volatile.Read(ref _current));
        _client.IsConnected.Returns(_ => Volatile.Read(ref _current) is not null);
        _client.TestAsync(default!, default).ReturnsForAnyArgs(ConnectionTestResult.Success(new SystemInfo()));
    }

    private static readonly string Fingerprint = string.Join(':', Enumerable.Repeat("AB", 32));

    private SessionService NewSession() =>
        new(_client, Fakes.Settings(), Substitute.For<ITokenProtector>(), NullLogger<SessionService>.Instance);

    [Fact]
    public async Task A_capture_during_a_same_server_reconnect_never_pairs_the_new_generation_with_the_old_policy()
    {
        var session = NewSession();
        await session.SignInAsync("https://nms.example.net/", "test-token", allowUntrustedCertificate: false, rememberToken: false);
        var before = session.CurrentScope!.Value;
        Assert.False(before.Connection.AllowUntrustedCertificate);

        // The reconnect stops inside the switch, with the generation moved and the client still
        // holding the old connection, while another thread captures a scope.
        Task<SessionScope?>? capture = null;
        var capturedMidSwitch = false;
        _duringSwitch = () =>
        {
            capture = Task.Run(() => SessionScope.Capture(session));
            // Held by the switch, it can't finish until the switch has. Reading unlocked it
            // finished at once, with the new generation and the old connection.
            capturedMidSwitch = capture.Wait(TimeSpan.FromMilliseconds(250));
        };

        await session.ReconnectAsync("https://nms.example.net/", null, allowUntrustedCertificate: true, backupAddress: null);

        Assert.False(capturedMidSwitch);
        var scope = (await capture!.WaitAsync(TimeSpan.FromSeconds(5)))!.Value;
        Assert.Equal(session.ConnectionGeneration, scope.Generation);
        Assert.NotEqual(before.Generation, scope.Generation);
        Assert.Same(_client.Connection, scope.Connection);
        Assert.True(scope.Connection.AllowUntrustedCertificate);
        Assert.False(before.IsCurrent(session));
        Assert.True(scope.IsCurrent(session));
    }

    [Fact]
    public async Task A_trusted_certificate_keeps_the_scope_current()
    {
        var session = NewSession();
        // As LibreNmsTransport.Install: the same server, a new connection object.
        _client.When(c => c.TrustCertificate(Arg.Any<string>())).Do(call =>
            Volatile.Write(ref _current, Volatile.Read(ref _current)?.WithTrustedCertificate(call.ArgAt<string>(0))));
        await session.SignInAsync("https://nms.example.net/", "test-token", allowUntrustedCertificate: false, rememberToken: false);
        var scope = SessionScope.Capture(session)!.Value;

        _client.TrustCertificate(Fingerprint);

        Assert.NotSame(scope.Connection, session.Connection);
        Assert.True(scope.IsCurrent(session));
        session.SignOut(forgetToken: false);
        Assert.Null(SessionScope.Capture(session));
        Assert.False(scope.IsCurrent(session));
    }
}
