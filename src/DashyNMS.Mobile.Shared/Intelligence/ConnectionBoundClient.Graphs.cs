using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Intelligence;

internal sealed partial class ConnectionBoundClient
{
    /// <summary>Shared graph rendering uses a JSON image envelope and the existing sensor fallback routes.</summary>
    internal ILibreNmsTransport GraphTransport => graphProfile ? new GraphRoutesOnly(this) : throw new RefusedException(nameof(GraphTransport));

    private sealed class GraphRoutesOnly(ConnectionBoundClient owner) : ILibreNmsTransport
    {
        public LibreNmsConnection? Connection => throw new RefusedException(nameof(Connection));
        public Task<IReadOnlyList<T>> GetCollectionAsync<T>(string relativeUrl, string collectionProperty, CancellationToken cancellationToken = default)
            => collectionProperty == "graphs" ? owner.GetSensorRouteAsync<T>(relativeUrl, collectionProperty, cancellationToken) : throw new RefusedException(nameof(GetCollectionAsync));
        public Task<JsonDocument> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken cancellationToken = default)
        {
            if (method != HttpMethod.Get || body is not null || !IsGraphImageRoute(relativeUrl)) throw new RefusedException(nameof(SendAsync));
            return owner.Open().Transport.SendAsync(method, relativeUrl, null, cancellationToken);
        }
        public Task<string> SendRawAsync(string relativeUrl, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(SendRawAsync));
    }

    /// <summary>Allow only the three image paths and the exact bounded query Shared emits, including its SVG permission probe.</summary>
    internal static bool IsGraphImageRoute(string relativeUrl)
    {
        var parts = relativeUrl.Split('?');
        if (parts.Length != 2 || !GraphImageQuery().IsMatch(parts[1])) return false;
        var match = GraphImagePath().Match(parts[0]);
        if (!match.Success || !int.TryParse(match.Groups["device"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var device) || device <= 0) return false;
        if (match.Groups["sensor"].Success && (!int.TryParse(match.Groups["sensor"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var sensor) || sensor <= 0)) return false;
        if (!match.Groups["port"].Success) return true;
        var encoded = match.Groups["port"].Value;
        var decoded = Uri.UnescapeDataString(encoded);
        return Uri.EscapeDataString(decoded) == encoded && !decoded.Any(c => char.IsControl(c) || c is '\\' or '?' or '#')
            && !decoded.Split('/').Any(segment => segment is "." or "..");
    }
    [GeneratedRegex(@"\Adevices/(?<device>[1-9][0-9]{0,9})/(?:device_[a-z0-9_]{1,80}|graphs/health/device_[a-z0-9_]{1,80}/(?<sensor>[1-9][0-9]{0,9})|ports/(?<port>(?:[A-Za-z0-9_.~-]|%[A-F0-9]{2}){1,768})/port_(?:bits|upkts|nupkts|errors))\z", RegexOptions.CultureInvariant)]
    private static partial Regex GraphImagePath();
    [GeneratedRegex(@"\Afrom=(?:-1(?:hour|day|week|month|year)|-?[0-9]{1,12})&width=800&height=250(?:&to=-?[0-9]{1,12})?(?:&legend=no)?&graph_type=(?:png|svg)&output=base64&bg=FFFFFF&bbg=FFFFFF\z", RegexOptions.CultureInvariant)]
    private static partial Regex GraphImageQuery();

    private sealed class GraphListingsOnly(ConnectionBoundClient owner) : IGraphsApi
    {
        public Task<IReadOnlyList<GraphType>> ListAsync(int deviceId, CancellationToken cancellationToken = default)
        { PositiveTopologyId(deviceId); return owner.Apis().Graphs.ListAsync(deviceId, cancellationToken); }
        public Task<IReadOnlyList<GraphType>> ListHealthAsync(int deviceId, CancellationToken cancellationToken = default)
        { PositiveTopologyId(deviceId); return owner.Apis().Graphs.ListHealthAsync(deviceId, cancellationToken); }
        public Task<IReadOnlyList<GraphType>> ListWirelessAsync(int deviceId, CancellationToken cancellationToken = default)
        { PositiveTopologyId(deviceId); return owner.Apis().Graphs.ListWirelessAsync(deviceId, cancellationToken); }
        public Task<string> GetSvgAsync(int deviceId, string graphName, GraphTimeRange range, int width, int height, CancellationToken cancellationToken = default, bool legend = true) => throw new RefusedException(nameof(GetSvgAsync));
        public Task<string> GetPortSvgAsync(int deviceId, string ifName, string graphType, GraphTimeRange range, int width, int height, CancellationToken cancellationToken = default, bool legend = true) => throw new RefusedException(nameof(GetPortSvgAsync));
        public Task<string> GetSensorSvgAsync(int deviceId, string graphName, int sensorId, GraphTimeRange range, int width, int height, CancellationToken cancellationToken = default, bool legend = true) => throw new RefusedException(nameof(GetSensorSvgAsync));
    }
}
