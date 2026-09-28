using System.Collections.Specialized;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Tests;

public sealed class BulkObservableCollectionTests
{
    [Fact]
    public void Replacing_everything_is_one_reset_not_an_event_per_item()
    {
        var collection = new BulkObservableCollection<int> { 1, 2 };
        var events = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => events.Add(e.Action);

        collection.ReplaceAll(Enumerable.Range(1, 500));

        Assert.Equal([NotifyCollectionChangedAction.Reset], events);
        Assert.Equal(500, collection.Count);
    }

    [Fact]
    public void The_same_items_again_change_nothing()
    {
        var collection = new BulkObservableCollection<string> { "a", "b" };
        var raised = 0;
        collection.CollectionChanged += (_, _) => raised++;

        collection.ReplaceAll(["a", "b"]);
        collection.ReplaceAll(["b", "a"]);

        Assert.Equal(1, raised); // only the reorder
        Assert.Equal(["b", "a"], collection);
    }
}
