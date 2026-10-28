namespace DashyNMS.Mobile.Pages;

/// <summary>
/// For a list the dashboard's shortcuts open pre-filtered (Alerts, Devices):
/// tells a fresh visit - the tab tapped, or reached from anywhere but its own
/// pages - from coming back from an alert or device it opened. A fresh visit
/// without a shortcut goes back to the user's own filter; coming back keeps
/// whatever was showing (#81).
/// </summary>
internal sealed class ShortcutReturn
{
    private bool _arrivedWithShortcut;
    private bool _leftForOwnPage;

    /// <summary>A shortcut came with this visit (from ApplyQueryAttributes).</summary>
    public void Arrived() => _arrivedWithShortcut = true;

    /// <summary>
    /// Leaving: for a page pushed on top of this one (it's on this page's
    /// stack), or somewhere else - another tab, or back out.
    /// </summary>
    public void Left(Page page, NavigatedFromEventArgs args)
    {
        _leftForOwnPage = args.DestinationPage is { } destination && page.Navigation.NavigationStack.Contains(destination);

        // A shortcut applied after the last OpenedAfresh would otherwise carry
        // over and skip the next visit's reset.
        _arrivedWithShortcut = false;
    }

    /// <summary>
    /// Whether this visit is a fresh one with no shortcut. The shortcut's
    /// query may be applied before or after this is asked; either way the
    /// shortcut wins, as it's applied last or skips the reset.
    /// </summary>
    public bool OpenedAfresh()
    {
        var afresh = !_arrivedWithShortcut && !_leftForOwnPage;
        _arrivedWithShortcut = false;
        _leftForOwnPage = false;
        return afresh;
    }
}
