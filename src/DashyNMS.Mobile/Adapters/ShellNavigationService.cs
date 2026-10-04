using DashyNMS.Mobile.Pages;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Adapters;

/// <summary>
/// Shell navigation. A tab route whose page isn't pinned to the tab bar is
/// pushed onto More instead (#68) - see <see cref="AppPages.Resolve"/>. On a
/// larger screen, a list showing its detail beside itself takes that route
/// into its pane rather than opening a page (#88) - see <see cref="IDetailHost"/>.
/// Each is noted in the diagnostics with what it opened and from where (#126).
/// </summary>
public sealed class ShellNavigationService(TabPins pins, DiagnosticsLog? diagnostics = null) : INavigationService
{
    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (route != Routes.Back)
            {
                var from = Shell.Current.CurrentPage?.GetType().Name.Replace("Page", string.Empty, StringComparison.Ordinal) ?? "start";
                diagnostics?.Note("Open", $"{NavigationDescription.Describe(route, parameters)} from {from}");
            }

            if (Shell.Current.CurrentPage is IDetailHost host && host.TryShowDetail(route, parameters))
            {
                return;
            }

            var resolved = AppPages.Resolve(route, pins);
            if (parameters is not null && await TryBackToCurrentTabAsync(resolved, parameters))
            {
                return;
            }

            await (parameters is null
                ? Shell.Current.GoToAsync(resolved)
                : Shell.Current.GoToAsync(resolved, parameters));
        });

    /// <summary>
    /// The tab that's already showing, with a query - a device's Location
    /// opened from the Devices tab's own list, say. Shell pops back to the
    /// tab's page but never hands it the query, and with the device beside
    /// the list (#88) doesn't move at all, so the list wasn't filtered (#101).
    /// Here the pop is done and the query handed over directly.
    /// </summary>
    private static async Task<bool> TryBackToCurrentTabAsync(string resolved, IDictionary<string, object> parameters)
    {
        if (Shell.Current.CurrentItem?.CurrentItem is not { } tab
            || resolved != $"{Routes.Main}/{tab.Route}"
            || tab.CurrentItem is not IShellContentController content
            || content.Page is not IQueryAttributable page)
        {
            return false;
        }

        if (tab.Navigation.NavigationStack.Count > 1)
        {
            await tab.Navigation.PopToRootAsync();
        }

        // A copy: the page clears the query once it's applied.
        page.ApplyQueryAttributes(new Dictionary<string, object>(parameters));
        return true;
    }
}
