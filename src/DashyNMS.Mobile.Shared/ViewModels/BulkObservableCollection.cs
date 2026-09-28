using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that can swap its whole contents
/// with one change notification.
/// </summary>
/// <remarks>
/// Clear-then-Add raises one event per item, and on iOS each is an animated
/// insert into the CollectionView on the main thread: a few hundred devices
/// kept the main thread busy long enough for iOS's watchdog to kill the app
/// (0x8BADF00D) when it went to the background mid-refresh. One Reset is a
/// single reload instead.
/// </remarks>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>
    /// Replaces the contents with <paramref name="items"/>, raising a single
    /// Reset - or nothing at all when they're the same items in the same
    /// order, so a refresh that changes nothing doesn't reload the list.
    /// </summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        var replacement = items as IList<T> ?? items.ToList();
        if (Items.Count == replacement.Count && Items.SequenceEqual(replacement))
        {
            return;
        }

        CheckReentrancy();
        Items.Clear();
        foreach (var item in replacement)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
