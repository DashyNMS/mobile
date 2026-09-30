using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DashyNMS.Mobile.Map;

/// <summary>One location's pin, as the map page draws it, with how many of its devices are in each state for the pin's ring.</summary>
public sealed record MapPinData(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lat")] double Latitude,
    [property: JsonPropertyName("lng")] double Longitude,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("up")] int Up = 0,
    [property: JsonPropertyName("down")] int Down = 0,
    [property: JsonPropertyName("maintenance")] int Maintenance = 0,
    [property: JsonPropertyName("off")] int Off = 0);

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
/// <para>Pins that land within <see cref="ClusterRadius"/> of each other on
/// screen merge into one, as desktop's map does (Core's PinClustering, the
/// same greedy pass in the same order), and regroup on every zoom and pan.
/// Geocoding often puts several sites at the same point; drawn separately
/// they stacked exactly, and only the top one could be seen or tapped
/// (#84). A merged pin shows the total and a ring of device states, and a
/// tap on it lists every device there and zooms in until they separate.</para>
/// </remarks>
public static class MapHtml
{
    /// <summary>What a tapped pin navigates to: dashynms-map://pins/{id},{id},...</summary>
    public const string PinScheme = "dashynms-map";

    /// <summary>Desktop's merge distance, in screen points.</summary>
    internal const int ClusterRadius = 26;

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
        var surface = dark ? "#171B23" : "#FFFFFF";
        var text = dark ? "#E8EEF6" : "#11141A";

        return $$"""
            <!DOCTYPE html>
            <html><head>
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'nonce-{{nonce}}'; style-src 'unsafe-inline'; img-src {{tileScheme}} data:">
            <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1">
            <style>
            {{leafletCss}}
              html, body, #map { margin: 0; padding: 0; height: 100%; background: {{background}}; }
              {{tileFilter}}
              /* As desktop's pins: a ring split by device state, the count in the hole, bigger for bigger sites. */
              .pin { border-radius: 50%; display: flex; align-items: center; justify-content: center;
                     box-shadow: 0 1px 4px rgba(0,0,0,0.45); }
              .pin span { width: 62%; height: 62%; border-radius: 50%; display: flex; align-items: center; justify-content: center;
                          background: {{surface}}; color: {{text}}; font: 600 11px -apple-system, Roboto, sans-serif; }
            </style>
            <script nonce="{{nonce}}">{{leafletJs}}</script>
            </head><body><div id="map"></div>
            <script nonce="{{nonce}}">
            (function () {
              var data = {{data}};
              var map = L.map('map', { worldCopyJump: true });
              L.tileLayer(data.tiles, { maxZoom: 19, attribution: data.attribution }).addTo(map);
              var layer = L.layerGroup().addTo(map);
              var bounds = data.pins.map(function (p) { return [p.lat, p.lng]; });
              var radius = {{ClusterRadius}};

              // Core's PinClustering, as desktop: greedy, in order; a pin joins the
              // first group within the radius on screen, which keeps its centre
              // on its members.
              function groups() {
                var found = [];
                data.pins.forEach(function (p) {
                  var at = map.latLngToLayerPoint([p.lat, p.lng]);
                  for (var i = 0; i < found.length; i++) {
                    var g = found[i], dx = g.x - at.x, dy = g.y - at.y;
                    if (dx * dx + dy * dy <= radius * radius) {
                      g.pins.push(p);
                      g.x += (at.x - g.x) / g.pins.length;
                      g.y += (at.y - g.y) / g.pins.length;
                      return;
                    }
                  }
                  found.push({ pins: [p], x: at.x, y: at.y });
                });
                return found;
              }

              function draw() {
                layer.clearLayers();
                groups().forEach(function (g) {
                  var up = 0, down = 0, maintenance = 0, off = 0;
                  g.pins.forEach(function (p) { up += p.up; down += p.down; maintenance += p.maintenance; off += p.off; });
                  var count = up + down + maintenance + off;
                  var size = Math.round(2 * Math.min(20, Math.max(10, 9 + Math.log2(Math.max(count, 1)) * 2)));

                  // The ring, in proportion, down first from twelve o'clock.
                  var stops = [], from = 0;
                  [[down, '#DA3633'], [maintenance, '#4C9AFF'], [up, '#2EA043'], [off, '#6E6E6E']].forEach(function (s) {
                    if (!s[0]) { return; }
                    var to = from + s[0] / count * 360;
                    stops.push(s[1] + ' ' + from + 'deg ' + to + 'deg');
                    from = to;
                  });

                  var el = document.createElement('div');
                  el.className = 'pin';
                  el.style.width = el.style.height = size + 'px';
                  el.style.background = stops.length ? 'conic-gradient(' + stops.join(', ') + ')' : '#6E6E6E';
                  var hole = document.createElement('span');
                  hole.textContent = String(count);
                  el.appendChild(hole);

                  var where = map.layerPointToLatLng([g.x, g.y]);
                  var title = g.pins.length === 1 ? g.pins[0].name : g.pins.length + ' locations';
                  var marker = L.marker(where, { icon: L.divIcon({ html: el, className: '', iconSize: [size, size] }), title: title });
                  marker.on('click', function () {
                    // A merged pin also zooms in until its locations separate.
                    if (g.pins.length > 1) { map.setView(where, Math.min(map.getZoom() + 2, 18)); }
                    window.location.href = '{{PinScheme}}://pins/' + g.pins.map(function (p) { return p.id; }).join(',');
                  });
                  layer.addLayer(marker);
                });
              }

              function fit() {
                if (bounds.length) { map.fitBounds(bounds, { padding: [30, 30], maxZoom: 12 }); } else { map.setView([20, 0], 2); }
                draw();
              }
              map.on('zoomend', draw);
              fit();
              // The page can load before the web view has its final size, and the
              // size changes again when the location panel opens: Leaflet then
              // draws only part of the map and fits the pins to the wrong area
              // (#84). Re-measure on every resize, and fit again the first time
              // there's a real size.
              var fitted = false;
              function resized() {
                map.invalidateSize();
                if (!fitted && map.getSize().y > 0) { fitted = true; fit(); }
              }
              window.addEventListener('resize', resized);
              setTimeout(resized, 250);
            })();
            </script>
            </body></html>
            """;
    }

    /// <summary>
    /// The pins a dashynms-map://pins/{id},{id},... navigation is for - one
    /// location, or every location in a merged pin - or null for anything else.
    /// </summary>
    public static IReadOnlyList<int>? PinsFrom(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, PinScheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, "pins", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var ids = new List<int>();
        foreach (var part in uri.AbsolutePath.Trim('/').Split(','))
        {
            if (!int.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id))
            {
                return null;
            }

            ids.Add(id);
        }

        return ids.Count > 0 ? ids : null;
    }
}
