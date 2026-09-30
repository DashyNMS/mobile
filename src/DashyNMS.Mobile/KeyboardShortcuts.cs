using DashyNMS.Mobile.Controls;

namespace DashyNMS.Mobile;

/// <summary>
/// What the keyboard shortcuts do on an iPad or a Mac (#88) - ⌘F to search,
/// ⌘R to refresh, ⌘1 to ⌘9 for the tabs - whichever page is showing. The
/// platform (Platforms/iOS/AppDelegate) registers the keys and calls these.
/// </summary>
/// <remarks>
/// They find what they act on in the page itself - its search box, its
/// pull-to-refresh - so every page with one gets the shortcut, with nothing
/// to add to each.
/// </remarks>
public static class KeyboardShortcuts
{
    /// <summary>The number of tab shortcuts: ⌘1 to ⌘9.</summary>
    public const int TabCount = 9;

    private static Page? Current => Shell.Current?.CurrentPage;

    /// <summary>The page's search box, brought out with the cursor in it.</summary>
    public static void Search()
    {
        if (Current?.GetVisualTreeDescendants().OfType<SearchBar>().FirstOrDefault(box => IsShowing(box.Parent)) is { } search)
        {
            SearchReveal.Reveal(search);
        }
    }

    /// <summary>
    /// Refreshes what's on screen, as pulling it down does - with a list's
    /// detail beside it, both.
    /// </summary>
    public static void Refresh()
    {
        foreach (var refresh in Current?.GetVisualTreeDescendants().OfType<RefreshView>().Where(refresh => IsShowing(refresh)) ?? [])
        {
            if (refresh.Command?.CanExecute(refresh.CommandParameter) == true)
            {
                refresh.Command.Execute(refresh.CommandParameter);
            }
        }
    }

    /// <summary>The <paramref name="index"/>th tab (from 0), as they're arranged.</summary>
    public static void ShowTab(int index)
    {
        if (Shell.Current?.CurrentItem is not TabBar tabs)
        {
            return;
        }

        var visible = tabs.Items.Where(tab => tab.IsVisible).ToList();
        if (index < visible.Count)
        {
            tabs.CurrentItem = visible[index];
        }
    }

    /// <summary>
    /// Visible, as are all its parents - not a hidden pane's. A search box
    /// is asked about from its slot: tucked away, the box itself is hidden.
    /// </summary>
    private static bool IsShowing(Element? element)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (current is VisualElement { IsVisible: false })
            {
                return false;
            }
        }

        return true;
    }
}
