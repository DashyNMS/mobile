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
/// sign-in, reconnect, restore and sign-out, the same server again included. It moves
/// before the client switches, so a reader that sees the new connection always sees the
/// new generation. The server, backup and token are compared as well, so a scope can never
/// pass for a different server even if a session implementation didn't move it.
/// </para>
/// </remarks>
public readonly record struct SessionScope(long Generation, LibreNmsConnection Connection)
{
    /// <summary>The connection in use now with its generation; null when signed out.</summary>
    public static SessionScope? Capture(ISessionService session)
    {
        ArgumentNullException.ThrowIfNull(session);
        // The generation first: it moves before the connection does, so a switch between
        // the two reads leaves an old generation with the new connection, never current.
        var generation = session.ConnectionGeneration;
        return session.Connection is { } connection ? new SessionScope(generation, connection) : null;
    }

    /// <summary>
    /// Whether the session is still on this connection: the same generation and the same
    /// server, backup and token. A trusted certificate or a timeout change keeps it current.
    /// </summary>
    public bool IsCurrent(ISessionService session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Connection is { } now && session.ConnectionGeneration == Generation && SameServer(Connection, now);
    }

    /// <summary>Whether two connections reach the same server, by the same backup, with the same token.</summary>
    public static bool SameServer(LibreNmsConnection a, LibreNmsConnection b) =>
        a.WebRoot == b.WebRoot && a.BackupWebRoot == b.BackupWebRoot && string.Equals(a.ApiToken, b.ApiToken, StringComparison.Ordinal);
}
