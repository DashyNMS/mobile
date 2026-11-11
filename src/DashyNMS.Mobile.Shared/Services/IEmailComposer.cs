namespace DashyNMS.Mobile.Services;

/// <summary>A plain-text email for the person to review and send from their own email app.</summary>
public sealed record EmailDraft(string To, string Subject, string Body)
{
    /// <summary>Encodes each component separately so report text cannot add recipients or URI parameters.</summary>
    // System.Uri parses mailto addresses and requires the recipient separator literally.
    public Uri ToMailtoUri() => new("mailto:" + Uri.EscapeDataString(To).Replace("%40", "@", StringComparison.Ordinal)
        + "?subject=" + Uri.EscapeDataString(Subject) + "&body=" + Uri.EscapeDataString(Body));
}

/// <summary>
/// Opens the platform's email composer, pre-filled. It never sends anything
/// itself: the person sees the draft in their email app and chooses whether to
/// send it, so a view model can't post data without them seeing it (#574).
/// </summary>
public interface IEmailComposer
{
    /// <summary>False when the device has no email app or account that can take a draft.</summary>
    bool IsAvailable { get; }

    /// <summary>Hands the draft to the email app. Throws when the platform refuses it. Completion does not distinguish sending from cancellation.</summary>
    Task ComposeAsync(EmailDraft draft);
}
