using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DashyNMS.Mobile.Map;

/// <summary>One location's pin, as the map page draws it.</summary>
public sealed record MapPinData(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lat")] double Latitude,
    [property: JsonPropertyName("lng")] double Longitude,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("state")] string State);

/// <summary>
/// The Map page's HTML: Leaflet (bundled with the app, inlined - never from
/// a CDN), the tile server, and one pin per location.
/// </summary>
/// <remarks>
/// Unlike a graph page this runs script - Leaflet's own and a few lines that
/// place the pins - so it's locked down another way. Only scripts carrying
/// this page's one-off nonce run; images come only from the tile server's
/// scheme (and data: for Leaflet's own); nothing else loads. Names from
/// LibreNMS reach the page only as JSON (whose encoder escapes &lt;, &gt;
/// and &amp;, so a name can't close the script) and are drawn with
/// textContent, never as markup. A pin tap navigates to
/// <see cref="PinScheme"/>, which the page catches; the web view refuses
/// every other navigation.
/// </remarks>
public static class MapHtml
{
    /// <summary>What a tapped pin navigates to: dashynms-map://pin/{id}.</summary>
    public const string PinScheme = "dashynms-map";

    /// <summary>The credit OpenStreetMap's tile policy asks for.</summary>
    internal const string OpenStreetMapCredit = "© OpenStreetMap contributors";

    public static string Build(
        IReadOnlyList<MapPinData> pins,
        string tileTemplate,
        bool openStreetMapTiles,
        bool dark,
        string leafletJs,
        string leafletCss)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var tileScheme = tileTemplate.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ? "http:" : "https:";
        var data = JsonSerializer.Serialize(new
        {
            pins,
            tiles = tileTemplate,
            attribution = openStreetMapTiles ? OpenStreetMapCredit : string.Empty,
        });

        var tileFilter = dark ? ".leaflet-tile-pane { filter: invert(1) hue-rotate(180deg) brightness(0.95) contrast(0.9); }" : string.Empty;
        var background = dark ? "#11141A" : "#FFFFFF";

        return $$"""
            <!DOCTYPE html>
            <html><head>
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'nonce-{{nonce}}'; style-src 'unsafe-inline'; img-src {{tileScheme}} data:">
            <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1">
            <style>
            {{leafletCss}}
              html, body, #map { margin: 0; padding: 0; height: 100%; background: {{background}}; }
              {{tileFilter}}
              .pin { width: 28px; height: 28px; border-radius: 14px; border: 2px solid #FFFFFF; color: #FFFFFF;
                     font: 600 12px -apple-system, Roboto, sans-serif; display: flex; align-items: center; justify-content: center;
                     box-shadow: 0 1px 4px rgba(0,0,0,0.4); }
              .pin.up { background: #2EA043; } .pin.down { background: #DA3633; } .pin.off { background: #6E6E6E; }
            </style>
            <script nonce="{{nonce}}">{{leafletJs}}</script>
            </head><body><div id="map"></div>
            <script nonce="{{nonce}}">
            (function () {
              var data = {{data}};
              var map = L.map('map', { worldCopyJump: true });
              L.tileLayer(data.tiles, { maxZoom: 19, attribution: data.attribution }).addTo(map);
              var bounds = [];
              data.pins.forEach(function (p) {
                var el = document.createElement('div');
                el.className = 'pin ' + p.state;
                el.textContent = String(p.count);
                var marker = L.marker([p.lat, p.lng], { icon: L.divIcon({ html: el, className: '', iconSize: [28, 28] }), title: p.name });
                marker.on('click', function () { window.location.href = '{{PinScheme}}://pin/' + p.id; });
                marker.addTo(map);
                bounds.push([p.lat, p.lng]);
              });
              if (bounds.length) { map.fitBounds(bounds, { padding: [30, 30], maxZoom: 12 }); } else { map.setView([20, 0], 2); }
            })();
            </script>
            </body></html>
            """;
    }

    /// <summary>The pin a dashynms-map://pin/{id} navigation is for, or null for anything else.</summary>
    public static int? PinFrom(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, PinScheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(uri.Host, "pin", StringComparison.OrdinalIgnoreCase)
        && int.TryParse(uri.AbsolutePath.Trim('/'), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
}
