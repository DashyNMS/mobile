namespace DashyNMS.Mobile.Controls;

/// <summary>
/// The top line every page shares (#253), from App.xaml's TopBarPadding and
/// TopBarHeight - the Dashboard's bar, which the rest match.
/// </summary>
internal static class TopBar
{
    /// <summary>From the top of the page's safe area to the line.</summary>
    public static double Offset => Resource<Thickness>("TopBarPadding")?.Top ?? 16;

    /// <summary>The line's height: the Dashboard's 44pt Customise button.</summary>
    public static double Height => Resource<double>("TopBarHeight") ?? 44;

    private static T? Resource<T>(string key) where T : struct =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is T typed ? typed : null;
}
