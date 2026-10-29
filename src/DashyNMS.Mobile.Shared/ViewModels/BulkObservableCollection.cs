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

    /// <summary>
    /// An auto-refresh's result (#214): newer items in at the top and the rest
    /// kept, so a list scrolled down stays where it is. Only the rows whose
    /// text changed (an age moving on) are swapped; when the items aren't the
    /// same list with newer ones before it, this is a <see cref="ReplaceAll"/>.
    /// </summary>
    /// <param name="key">What makes two items the same entry.</param>
    /// <param name="looksSame">Whether the entry would read the same, so its row needn't change.</param>
    public void Merge(IReadOnlyList<T> items, Func<T, object?> key, Func<T, T, bool> looksSame)
    {
        var newer = Items.Count == 0 ? -1 : IndexOf(items, key(Items[0]), key);
        var overlap = newer < 0 ? 0 : Math.Min(Items.Count, items.Count - newer);
        for (var i = 0; i < overlap; i++)
        {
            if (!Equals(key(Items[i]), key(items[newer + i])))
            {
                newer = -1;
                break;
            }
        }

        if (newer < 0)
        {
            ReplaceAll(items);
            return;
        }

        for (var i = 0; i < newer; i++)
        {
            InsertItem(i, items[i]);
        }

        for (var i = newer; i < newer + overlap; i++)
        {
            if (!looksSame(Items[i], items[i]))
            {
                SetItem(i, items[i]);
            }
        }

        while (Count > items.Count)
        {
            RemoveItem(Count - 1);
        }

        for (var i = Count; i < items.Count; i++)
        {
            InsertItem(i, items[i]);
        }
    }

    private static int IndexOf(IReadOnlyList<T> items, object? wanted, Func<T, object?> key)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (Equals(key(items[i]), wanted))
            {
                return i;
            }
        }

        return -1;
    }
}
