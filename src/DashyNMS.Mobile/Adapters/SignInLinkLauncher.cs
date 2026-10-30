using DashyNMS.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Adapters;

/// <summary>
/// Hands a <c>dashynms://sign-in</c> link from the Camera app (#171) to
/// <see cref="SignInLinks"/> once the app can ask about it - on a cold
/// launch the link arrives before there's a window to show the question in.
/// </summary>
public static class SignInLinkLauncher
{
    /// <returns>True if it was a sign-in link, so the caller shouldn't treat it as anything else.</returns>
    public static bool TryOpen(string? link)
    {
        if (!SignInLinks.IsSignInLink(link))
        {
            return false;
        }

        _ = OpenWhenReadyAsync(link!);
        return true;
    }

    private static async Task OpenWhenReadyAsync(string link)
    {
        // Up to ten seconds for the first page to show; a launch takes far less.
        for (var i = 0; i < 50 && Shell.Current?.CurrentPage is null; i++)
        {
            await Task.Delay(200);
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (IPlatformApplication.Current?.Services.GetService<SignInLinks>() is { } links)
            {
                await links.OpenAsync(link);
            }
        });
    }
}
