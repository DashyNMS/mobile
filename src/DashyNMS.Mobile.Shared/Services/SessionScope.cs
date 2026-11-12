using DesktopNMS.Core.Api;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// One signed-in connection, as something that must not outlive it captured it: an
/// Intelligence chat, its lookups, the device search's private transport (#596).
/// </summary>
/// <remarks>
/// <para>
/// The session's <see cref="ISessionService.Connection"/> can't be compared by reference.
/// Core gives the client a new connection object whenever a certificate is trusted or the
/// request timeout changes (<c>LibreNmsTransport.Install</c>), on the same server with the
/// same token, so a chat that compared references was cleared by accepting the very
/// certificate prompt its own lookup raised, or by changing the timeout in Settings.
/// </para>
/// <para>
/// <see cref="ISessionService.ConnectionGeneration"/> moves only on a genuine change:
/// sign-in, reconnect, restore and sign-out, the same server again included. The pair is
/// read from <see cref="ISessionService.CurrentScope"/>, in one step with the switch: read
/// one property after the other, a switch in between could pair the new generation with
/// the old connection, and a reconnect to the same server with a different certificate
/// policy would then pass as current with the old policy. The server, backup and token are
/// compared as well, so a scope can never pass for a different server even if a session
/// implementation didn't move the generation.
/// </para>
/// </remarks>
public readonly record struct SessionScope(long Generation, LibreNmsConnection Connection)
{
    /// <summary>The connection in use now with its generation; null when signed out.</summary>
    public static SessionScope? Capture(ISessionService session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.CurrentScope;
    }

    /// <summary>
    /// Whether the session is still on this connection: the same generation and the same
    /// server, backup and token. A trusted certificate or a timeout change keeps it current.
    /// </summary>
    public bool IsCurrent(ISessionService session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.CurrentScope is { } now && now.Generation == Generation && SameServer(Connection, now.Connection);
    }

    /// <summary>Whether two connections reach the same server, by the same backup, with the same token.</summary>
    public static bool SameServer(LibreNmsConnection a, LibreNmsConnection b) =>
        a.WebRoot == b.WebRoot && a.BackupWebRoot == b.BackupWebRoot && string.Equals(a.ApiToken, b.ApiToken, StringComparison.Ordinal);
}
