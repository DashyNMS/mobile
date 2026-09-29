using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Adapters;

/// <summary>
/// Shell navigation. A tab route whose page isn't pinned to the tab bar is
/// pushed onto More instead (#68) - see <see cref="AppPages.Resolve"/>.
/// </summary>
public sealed class ShellNavigationService(TabPins pins) : INavigationService
{
    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        var resolved = AppPages.Resolve(route, pins);
        return MainThread.InvokeOnMainThreadAsync(() => parameters is null
            ? Shell.Current.GoToAsync(resolved)
            : Shell.Current.GoToAsync(resolved, parameters));
    }
}
