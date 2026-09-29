using System.Collections;
using System.Collections.Specialized;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Keeps a list's search box - its header - out of sight above the first
/// row until the list is pulled down, as iOS Mail does: the list gets the
/// room, and the box is a flick away.
/// </summary>
public static class SearchReveal
{
    /// <summary>
    /// Scrolls the header away once the list first has rows, unless
    /// something's being searched. Once per page: after that the box stays
    /// wherever the user leaves it.
    /// </summary>
    public static void Attach(CollectionView list, SearchBar search)
    {
        var hidden = false;

        void TryHide()
        {
            if (hidden || !string.IsNullOrEmpty(search.Text)
                || list.ItemsSource is not IEnumerable items || !items.GetEnumerator().MoveNext())
            {
                return;
            }

            hidden = true;
            list.Dispatcher.Dispatch(() => list.ScrollTo(0, position: ScrollToPosition.Start, animate: false));
        }

        if (list.ItemsSource is INotifyCollectionChanged changes)
        {
            changes.CollectionChanged += (_, _) => TryHide();
        }

        TryHide();
    }
}
