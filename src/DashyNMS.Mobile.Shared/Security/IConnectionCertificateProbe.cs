using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Security;

namespace DashyNMS.Mobile.Security;

/// <summary>
/// Finds out which certificate one connection's server presents now, on a request bound to
/// that connection (#611). <see cref="CertificatePrompt"/> asks about what this finds rather
/// than what the session's client reported, which can't be tied to a connection.
/// </summary>
public interface IConnectionCertificateProbe
{
    /// <summary>
    /// The certificate a request to <paramref name="scope"/>'s connection is refused on now,
    /// with that scope's generation; null when it gets through or fails for another reason.
    /// </summary>
    Task<ScopedCertificateRejection?> ProbeAsync(SessionScope scope, CancellationToken cancellationToken = default);
}

/// <summary>A certificate a request was refused on, and the connection that request belonged to.</summary>
/// <param name="Certificate">The certificate the server presented.</param>
/// <param name="Generation">The <see cref="DesktopNMS.Services.ISessionService.ConnectionGeneration"/> the request's transport was opened for.</param>
/// <param name="OnBackup">Whether that transport was dialling the backup address, for the prompt's wording.</param>
public sealed record ScopedCertificateRejection(CertificateDetails Certificate, long Generation, bool OnBackup);
