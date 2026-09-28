using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Adapters;

public sealed class LauncherService : ILauncherService
{
    public Task OpenAsync(Uri uri) => Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
}
