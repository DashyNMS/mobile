using DashyNMS.Mobile.Topology;
using DesktopNMS.Core.Topology;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

/// <summary>"Sign out and forget everything" drops every map's layout, in memory as well as on disk (#139).</summary>
public sealed class ResettableMapLayoutStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"map-layouts-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void Forgetting_all_leaves_nothing_to_be_written_back()
    {
        var store = new ResettableMapLayoutStore(_path, NullLogger<MapLayoutStore>.Instance);
        store.Save("nms|location:|group:", new Dictionary<int, MapPoint> { [1] = new(10, 20) });
        store.Save("nms|location:2|group:", new Dictionary<int, MapPoint> { [2] = new(30, 40) });
        Assert.True(File.Exists(_path));

        Assert.True(store.ForgetAll());

        Assert.False(File.Exists(_path));
        Assert.Empty(store.Get("nms|location:|group:"));

        // The next drag writes only itself - Core's store would have written the old layouts back.
        store.Save("nms|location:|group:", new Dictionary<int, MapPoint> { [3] = new(1, 1) });
        Assert.Empty(new MapLayoutStore(_path, NullLogger<MapLayoutStore>.Instance).Get("nms|location:2|group:"));
    }
}
