using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Adapters;

public sealed class LauncherService : ILauncherService
{
    public Task OpenAsync(Uri uri) => Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);

    /// <summary>
    /// Needs the scheme declared to the OS to see whether anything handles it:
    /// LSApplicationQueriesSchemes on iOS, a queries element on Android.
    /// </summary>
    public Task<bool> TryOpenAppAsync(Uri uri) => MainThread.InvokeOnMainThreadAsync(() => Launcher.Default.TryOpenAsync(uri));
}
