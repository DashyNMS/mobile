using DashyNMS.Mobile.Map;

namespace DashyNMS.Mobile.Adapters;

/// <summary>Leaflet from the app package (Resources/Raw/leaflet), read once.</summary>
public sealed class MapAssets : IMapAssets
{
    private (string Js, string Css)? _leaflet;

    public async Task<(string Js, string Css)> LeafletAsync() => _leaflet ??= (await Read("leaflet/leaflet.js"), await Read("leaflet/leaflet.css"));

    private static async Task<string> Read(string path)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(path);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
