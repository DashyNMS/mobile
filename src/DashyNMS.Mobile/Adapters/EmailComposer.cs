using DashyNMS.Mobile.Services;
using Microsoft.Maui.ApplicationModel.Communication;

namespace DashyNMS.Mobile.Adapters;

/// <summary>
/// <see cref="IEmailComposer"/> over MAUI's email API: Mail's compose sheet on
/// iOS, or the person's chosen email app through a mailto intent on Android.
/// Both leave sending to the person. Answer reports (#574) need this, and
/// LSApplicationQueriesSchemes and the Android queries element declare mailto
/// so <see cref="IsAvailable"/> can find a mail app.
/// </summary>
public sealed class EmailComposer : IEmailComposer
{
    public bool IsAvailable => Email.Default.IsComposeSupported;

    public Task ComposeAsync(EmailDraft draft) => MainThread.InvokeOnMainThreadAsync(async () =>
    {
#if IOS
        // MAUI discards the bool returned by its mailto fallback. Check it here
        // so a refused launch reaches the preview's truthful copy fallback.
        if (!MessageUI.MFMailComposeViewController.CanSendMail)
        {
            if (!await Launcher.Default.OpenAsync(draft.ToMailtoUri()))
                throw new InvalidOperationException("The email app refused the draft.");
            return;
        }
#endif
        await Email.Default.ComposeAsync(new EmailMessage(draft.Subject, draft.Body, draft.To) { BodyFormat = EmailBodyFormat.PlainText });
    });
}
