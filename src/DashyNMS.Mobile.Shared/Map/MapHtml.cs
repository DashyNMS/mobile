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
    [property: JsonPropertyName("off")] int Off = 0)
{
    /// <summary>Unreported states occupy their own grey share of the ring.</summary>
    [JsonPropertyName("unknown")]
    public int Unknown => Math.Max(0, Count - Up - Down - Maintenance - Off);

    /// <summary>Ordered ring counts; unknown is explicitly grey rather than a gradient remainder.</summary>
    [JsonPropertyName("ring")]
    public IReadOnlyList<MapRingPart> Ring =>
    [
        new(Down, "#DA3633"), new(Maintenance, "#4C9AFF"), new(Up, "#2EA043"),
        new(Off + Unknown, "#6E6E6E"),
    ];
}

/// <summary>A ring segment passed as JSON, so the rendered colours are testable without MAUI.</summary>
public sealed record MapRingPart(
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("colour")] string Colour);

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
/// <see cref="PinScheme"/>, which the page catches; a tapped http(s) link
/// (the attribution) opens in the browser instead, and the web view refuses
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

    /// <summary>The page OpenStreetMap's credit links to - its copyright and licence page.</summary>
    public const string OpenStreetMapCopyright = "https://www.openstreetmap.org/copyright";

    /// <summary>
    /// The credit OpenStreetMap's tile policy asks for, linked to its
    /// copyright page (#164). Leaflet's attribution is markup; this is the
    /// only markup the page puts there, and it's ours.
    /// </summary>
    internal const string OpenStreetMapCredit = "© <a href=\"" + OpenStreetMapCopyright + "\">OpenStreetMap</a> contributors";

    /// <summary>
    /// The address the page is loaded as, rather than none (#164). With no
    /// base address the tile requests went out without a Referer, which
    /// OpenStreetMap's tile policy asks for and its servers may refuse
    /// without; with the app's website as the base, they say where they're
    /// from. Nothing on the page is fetched from it.
    /// </summary>
    public static Uri BaseUrl => Services.AppLinks.Website;

    /// <summary>
    /// The web view's User-Agent on the map (#164): OpenStreetMap's tile
    /// policy asks apps to identify themselves rather than send a stock
    /// browser's.
    /// </summary>
    public static string UserAgent(string? version) =>
        "DashyNMS-Mobile/" + (string.IsNullOrWhiteSpace(version) ? "1" : version) + " (+" + Services.AppLinks.Website + ")";

    /// <summary>
    /// A tapped link on the map - its attribution: OpenStreetMap's copyright
    /// page, or Leaflet's - to open in the browser, or null for anything
    /// that isn't an http(s) link away from the page itself.
    /// </summary>
    public static Uri? ExternalLink(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || Uri.Compare(uri, BaseUrl, UriComponents.HttpRequestUrl, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0)
        {
            return null;
        }

        return uri;
    }

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
            <meta name="referrer" content="strict-origin-when-cross-origin">
            <style>
            {{leafletCss}}
              html, body, #map { margin: 0; padding: 0; height: 100%; overflow: hidden; background: {{background}}; }
              {{tileFilter}}
              .leaflet-marker-icon { transition: opacity 250ms; }
              .immediate .leaflet-marker-icon { transition: none; }
              .leaflet-touch .leaflet-bar a, .leaflet-bar a { width: 44px; height: 44px; line-height: 44px; }
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
              L.tileLayer(data.tiles, { maxZoom: 19, attribution: data.attribution, referrerPolicy: 'strict-origin-when-cross-origin' }).addTo(map);
              window.setMapClearance = function (top, right, bottom, left) {
                var previous = clearance;
                hasClearance = true;
                clearance = { top: top, right: right, bottom: bottom, left: left };
                placeControls();
                if (!ready) initialise();
                else if (fitWithoutClearance && !userMoved) {
                  // A selected pin already owns its zoom; only its safe offset needs repair.
                  if (dimming) focusSelection();
                  else programmatic(function () { fit(); });
                } else if (dimming) {
                  // Only a dimming selection follows the uncovered-centre delta.
                  programmatic(function () {
                    map.panBy([((right - left) - (previous.right - previous.left)) / 2,
                      ((bottom - top) - (previous.bottom - previous.top)) / 2], { animate: false });
                  });
                }
                fitWithoutClearance = false;
              };
              var clearance = { top: 0, right: 0, bottom: 0, left: 0 }, selected = [], dimming = false, ready = false, pendingFocus = false;
              var target = null, hasClearance = false, fitWithoutClearance = false, userMoved = false, moving = 0;
              function placeControls() {
                var container = map.getContainer();
                var controls = container.querySelector('.leaflet-control-container');
                controls.style.position = 'absolute';
                controls.style.top = clearance.top + 'px'; controls.style.right = clearance.right + 'px';
                controls.style.bottom = clearance.bottom + 'px'; controls.style.left = clearance.left + 'px';
                controls.style.pointerEvents = 'none';
                controls.querySelectorAll('.leaflet-control').forEach(function (c) { c.style.pointerEvents = 'auto'; });
                var zoom = controls.querySelector('.leaflet-control-zoom');
                if (zoom) {
                  // Measure while hidden too. Leaflet's cached size can lag native
                  // resize. Keep both 44px targets clear of header AND bottom card;
                  // when impossible, hide zoom (pinch remains available).
                  zoom.style.visibility = '';
                  var height = zoom.offsetHeight;
                  var room = container.clientHeight - clearance.top - clearance.bottom - 20;
                  zoom.style.visibility = height > 0 && room >= height ? '' : 'hidden';
                }
              }
              function programmatic(action) { moving++; try { action(); } finally { moving--; } }
              map.on('movestart zoomstart', function () { if (!moving && ready) userMoved = true; });
              function focusSelection() {
                if (!ready) { pendingFocus = true; return; }
                pendingFocus = false;
                var pins = data.pins.filter(function (p) { return selected.indexOf(p.id) >= 0; });
                if (!pins.length) return;
                var centre = L.latLngBounds(pins.map(function (p) { return [p.lat, p.lng]; })).getCenter();
                var zoom = target && target.ids === selected.join(',') ? target.zoom : map.getZoom();
                if (target && target.ids === selected.join(',')) target = null;
                programmatic(function () {
                  // One synchronous camera operation owns both merged-pin zoom and safe offset.
                  map.stop();
                  map.setView(centre, zoom, { animate: false });
                  map.panBy([(clearance.right - clearance.left) / 2, (clearance.bottom - clearance.top) / 2], { animate: false });
                });
              }
              window.setSelection = function (ids, immediate, dim, focus) {
                selected = ids;
                dimming = dim !== false && ids.length > 0;
                document.getElementById('map').classList.toggle('immediate', immediate);
                layer.eachLayer(function (marker) {
                  var el = marker.getElement();
                  if (el) el.style.opacity = !dimming || marker.pinIds.some(function (id) { return selected.indexOf(id) >= 0; }) ? '1' : '0.25';
                });
                if (!dimming) { pendingFocus = false; if (!ids.length) target = null; }
                if (focus && dimming) focusSelection();
              };
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
                  var ring = g.pins[0].ring.map(function (part, index) {
                    return [g.pins.reduce(function (sum, p) { return sum + p.ring[index].count; }, 0), part.colour];
                  });
                  var count = g.pins.reduce(function (total, p) { return total + p.count; }, 0);
                  var size = Math.round(2 * Math.min(20, Math.max(10, 9 + Math.log2(Math.max(count, 1)) * 2)));

                  // The ring, in proportion, down first from twelve o'clock.
                  var stops = [], from = 0;
                  ring.forEach(function (s) {
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
                  marker.pinIds = g.pins.map(function (p) { return p.id; });
                  marker.on('click', function () {
                    // Defer the zoom until the native card has supplied its clearance.
                    target = { ids: marker.pinIds.join(','), zoom: g.pins.length > 1 ? Math.min(map.getZoom() + 2, 18) : map.getZoom() };
                    window.location.href = '{{PinScheme}}://pins/' + g.pins.map(function (p) { return p.id; }).join(',');
                  });
                  layer.addLayer(marker);
                  marker.getElement().style.opacity = !dimming || marker.pinIds.some(function (id) { return selected.indexOf(id) >= 0; }) ? '1' : '0.25';
                });
              }

              function fit() {
                // Keep at least a quarter of each axis usable, even when large
                // captions or glass cover most of a short landscape canvas.
                var size = map.getSize();
                var left = clearance.left + 30, right = clearance.right + 30;
                var top = clearance.top + 30, bottom = clearance.bottom + 30;
                var horizontal = Math.min(1, size.x * 0.75 / (left + right));
                var vertical = Math.min(1, size.y * 0.75 / (top + bottom));
                if (bounds.length) { map.fitBounds(bounds, { paddingTopLeft: [left * horizontal, top * vertical], paddingBottomRight: [right * horizontal, bottom * vertical], maxZoom: 12, animate: false }); } else { map.setView([20, 0], 2, { animate: false }); }
                draw();
              }
              map.on('zoomend', draw);
              // Native geometry normally starts the map. A timed fallback also makes
              // it usable when the first bridge call is lost during document load.
              function initialise() {
                var container = map.getContainer();
                if (ready || container.clientHeight <= 0 || container.clientWidth <= 0) return;
                programmatic(function () {
                  // Leaflet caches getSize(), even before layout, and invalidateSize
                  // is a no-op until a view exists. Seed that view before invalidating
                  // so a document loaded at zero size can fit its eventual viewport.
                  map.setView([20, 0], 2, { animate: false });
                  map.invalidateSize({ pan: false });
                  fit();
                });
                fitWithoutClearance = !hasClearance;
                ready = true;
                if (pendingFocus) focusSelection();
              }
              function resized() {
                programmatic(function () { map.invalidateSize(); });
                placeControls();
                initialise();
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
