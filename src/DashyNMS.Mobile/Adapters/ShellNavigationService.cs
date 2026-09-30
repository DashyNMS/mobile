using DashyNMS.Mobile.Pages;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Adapters;

/// <summary>
/// Shell navigation. A tab route whose page isn't pinned to the tab bar is
/// pushed onto More instead (#68) - see <see cref="AppPages.Resolve"/>. On a
/// larger screen, a list showing its detail beside itself takes that route
/// into its pane rather than opening a page (#88) - see <see cref="IDetailHost"/>.
/// </summary>
public sealed class ShellNavigationService(TabPins pins) : INavigationService
{
    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null) =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Shell.Current.CurrentPage is IDetailHost host && host.TryShowDetail(route, parameters))
            {
                return Task.CompletedTask;
            }

            var resolved = AppPages.Resolve(route, pins);
            return parameters is null
                ? Shell.Current.GoToAsync(resolved)
                : Shell.Current.GoToAsync(resolved, parameters);
        });
}
