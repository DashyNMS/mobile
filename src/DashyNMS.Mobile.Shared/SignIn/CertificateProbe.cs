using System.Net.Http;
using DesktopNMS.Core.Security;

namespace DashyNMS.Mobile.SignIn;

/// <summary>What a look at the server's website found before signing in on it.</summary>
public sealed record ProbeResult(bool Reached, CertificateDetails? UntrustedCertificate = null, string? ErrorMessage = null);

/// <summary>
/// Opens the server's website once before "Sign in with LibreNMS" shows it
/// (#162). A web view can't ask about a certificate it doesn't trust, so a
/// self-signed one is found here first and offered through the same
/// "Trust this certificate?" prompt as the API's; the web view then accepts
/// exactly the certificates trusted that way. It also catches an address
/// that doesn't answer, with a clearer message than a blank web view.
/// </summary>
public interface ICertificateProbe
{
    Task<ProbeResult> ProbeAsync(Uri webRoot, bool allowUntrustedCertificate, IReadOnlyCollection<string> trustedCertificates, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ICertificateProbe"/>
public sealed class CertificateProbe : ICertificateProbe
{
    public async Task<ProbeResult> ProbeAsync(Uri webRoot, bool allowUntrustedCertificate, IReadOnlyCollection<string> trustedCertificates, CancellationToken cancellationToken = default)
    {
        CertificateDetails? rejected = null;
        using var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        handler.SslOptions.RemoteCertificateValidationCallback = CertificateTrust.CreateCallback(
            webRoot.Host,
            allowUntrustedCertificate,
            trustedCertificates,
            details => rejected = details);

        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        try
        {
            // Any answer will do - a login redirect, a page, even an error
            // page: the server is there and its certificate has been seen.
            using var response = await http.GetAsync(webRoot, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return new ProbeResult(true);
        }
        catch (HttpRequestException) when (rejected is not null)
        {
            return new ProbeResult(false, rejected);
        }
        catch (HttpRequestException ex) when (!allowUntrustedCertificate && ex.InnerException is System.Security.Authentication.AuthenticationException)
        {
            return new ProbeResult(false, ErrorMessage: "This server's certificate isn't trusted by your phone. Turn on \"Allow untrusted certificate\" to check and trust it, or use an API token instead.");
        }
        catch (HttpRequestException)
        {
            return new ProbeResult(false, ErrorMessage: "Couldn't reach " + webRoot.Host + ". Check the address and that your phone can reach the server.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProbeResult(false, ErrorMessage: webRoot.Host + " didn't answer in time.");
        }
    }
}
