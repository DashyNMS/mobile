using DashyNMS.Mobile.Pages;
using DashyNMS.Mobile.SignIn;

namespace DashyNMS.Mobile.Adapters;

/// <summary>
/// Shows <see cref="LibreNmsSignInPage"/> over the sign-in page and waits for
/// it (#162), then clears every web view's cookies and storage so LibreNMS
/// isn't left logged in inside the app - whether a token was made or not.
/// </summary>
public sealed class WebSignIn : IWebSignIn
{
    /// <summary>
    /// The phone's name for the token's description. iOS 16 and later give
    /// apps the model ("iPhone") rather than the name the owner chose, unless
    /// Apple grants the user-assigned-device-name entitlement.
    /// </summary>
    public string DeviceName => DeviceInfo.Current.Name;

    public async Task<WebSignInResult> SignInAsync(WebSignInRequest request)
    {
        var navigation = Shell.Current?.Navigation ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
        if (navigation is null)
        {
            return new WebSignInResult(WebSignInOutcome.Failed);
        }

        var page = new LibreNmsSignInPage(request);
        await MainThread.InvokeOnMainThreadAsync(() => navigation.PushModalAsync(page));
        try
        {
            return await page.Result;
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (navigation.ModalStack.Contains(page))
                {
                    await navigation.PopModalAsync();
                }

                await ClearWebDataAsync();
            });
        }
    }

    private static Task ClearWebDataAsync()
    {
#if IOS
        var cleared = new TaskCompletionSource();
        WebKit.WKWebsiteDataStore.DefaultDataStore.RemoveDataOfTypes(
            WebKit.WKWebsiteDataStore.AllWebsiteDataTypes,
            Foundation.NSDate.DistantPast,
            () => cleared.TrySetResult());
        return cleared.Task;
#elif ANDROID
        Android.Webkit.CookieManager.Instance?.RemoveAllCookies(null);
        Android.Webkit.CookieManager.Instance?.Flush();
        Android.Webkit.WebStorage.Instance?.DeleteAllData();
        return Task.CompletedTask;
#else
        return Task.CompletedTask;
#endif
    }
}
