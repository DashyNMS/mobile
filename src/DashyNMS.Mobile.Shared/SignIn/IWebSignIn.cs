namespace DashyNMS.Mobile.SignIn;

/// <summary>
/// What the sign-in web view needs: where to start, what to call the token,
/// and which certificates it may accept - the same ones the API calls accept,
/// so a self-signed server works only once its certificate has been trusted.
/// </summary>
public sealed record WebSignInRequest(
    WebTokenSignIn Flow,
    bool AllowUntrustedCertificate,
    IReadOnlyCollection<string> TrustedCertificates);

/// <summary>How the web sign-in ended.</summary>
public enum WebSignInOutcome
{
    /// <summary>A token was created: <see cref="WebSignInResult.Token"/>.</summary>
    Token,

    /// <summary>The user closed the web view.</summary>
    Cancelled,

    /// <summary>The account can't create tokens here (no API access, or LibreNMS before 26.4).</summary>
    NotAllowed,

    /// <summary>The page didn't load, or LibreNMS didn't create a token.</summary>
    Failed,
}

public sealed record WebSignInResult(WebSignInOutcome Outcome, string? Token = null)
{
    public static WebSignInResult Cancelled { get; } = new(WebSignInOutcome.Cancelled);
}

/// <summary>
/// Shows LibreNMS's own website in a web view for "Sign in with LibreNMS"
/// (#162), drives it with <see cref="WebTokenSignIn"/>, and clears the web
/// session again afterwards so LibreNMS isn't left logged in inside the app.
/// </summary>
public interface IWebSignIn
{
    /// <summary>The phone's name, for the token's description.</summary>
    string DeviceName { get; }

    Task<WebSignInResult> SignInAsync(WebSignInRequest request);
}
