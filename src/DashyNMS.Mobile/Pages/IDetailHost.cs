namespace DashyNMS.Mobile.Pages;

/// <summary>
/// A list page that can show what's tapped beside itself on a larger screen
/// (#88) - Alerts, Devices. Navigation asks the page on screen first, so the
/// view models go on asking for a route as on a phone.
/// </summary>
public interface IDetailHost
{
    /// <summary>
    /// Shows <paramref name="route"/> in the pane if it's this list's own
    /// detail and there's room.
    /// </summary>
    /// <returns>False to open it as a page, as usual.</returns>
    bool TryShowDetail(string route, IDictionary<string, object>? parameters);
}
