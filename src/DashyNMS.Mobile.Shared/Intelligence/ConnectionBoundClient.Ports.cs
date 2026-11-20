using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Intelligence;

internal sealed partial class ConnectionBoundClient
{
    /// <summary>Only trace's IPv4 ARP and MAC FDB collection reads escape Core's resource API.</summary>
    public ILibreNmsTransport TraceTransport => portProfile ? new TraceRoutesOnly(this) : throw new RefusedException(nameof(TraceTransport));

    private sealed class TraceRoutesOnly(ConnectionBoundClient owner) : ILibreNmsTransport
    {
        public LibreNmsConnection? Connection => throw new RefusedException(nameof(Connection));
        public Task<IReadOnlyList<T>> GetCollectionAsync<T>(string relativeUrl, string collectionProperty, CancellationToken cancellationToken = default)
        {
            const string arp = "resources/ip/arp/";
            var ipv4 = relativeUrl.StartsWith(arp, StringComparison.Ordinal) ? relativeUrl[arp.Length..] : "";
            var allowedArp = typeof(T) == typeof(ArpEntry) && collectionProperty == "arp"
                && IPAddress.TryParse(ipv4, out var address) && address.AddressFamily == AddressFamily.InterNetwork && address.ToString() == ipv4;
            var allowedFdb = typeof(T) == typeof(FdbEntry) && collectionProperty == "ports_fdb" && TraceFdbRoute().IsMatch(relativeUrl);
            if (!allowedArp && !allowedFdb) throw new RefusedException(nameof(GetCollectionAsync));
            return owner.Open().Transport.GetCollectionAsync<T>(relativeUrl, collectionProperty, cancellationToken);
        }
        public Task<JsonDocument> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(SendAsync));
        public Task<string> SendRawAsync(string relativeUrl, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(SendRawAsync));
    }
    [GeneratedRegex(@"\Aresources/fdb/[0-9a-f]{12}\z", RegexOptions.CultureInvariant)]
    private static partial Regex TraceFdbRoute();

    private static void PositiveTopologyId(int id)
    {
        if (id <= 0) throw new RefusedException("A positive topology device ID is required");
    }
    private sealed class TopologyPortsOnly(ConnectionBoundClient owner) : IPortsApi
    {
        public Task<IReadOnlyList<Port>> ListForDeviceAsync(int deviceId, CancellationToken cancellationToken = default)
        { PositiveTopologyId(deviceId); return owner.Apis().Ports.ListForDeviceAsync(deviceId, cancellationToken); }
        public Task<IReadOnlyList<Port>> ListAllStatusAsync(CancellationToken cancellationToken = default) => throw new RefusedException(nameof(ListAllStatusAsync));
        public Task<IReadOnlyList<Port>> ListAllNamesAsync(CancellationToken cancellationToken = default) => throw new RefusedException(nameof(ListAllNamesAsync));
        public Task<IReadOnlyList<DeviceIpAddress>> ListIpAddressesAsync(int deviceId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(ListIpAddressesAsync));
        public Task<Port?> GetAsync(int portId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetAsync));
        public Task<IReadOnlyList<Port>> FindByLearnedMacAsync(string mac, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(FindByLearnedMacAsync));
    }
    private sealed class TopologyLinksOnly(ConnectionBoundClient owner) : ILinksApi
    {
        public Task<IReadOnlyList<NetworkLink>> ListForDeviceAsync(int deviceId, CancellationToken cancellationToken = default)
        { PositiveTopologyId(deviceId); return owner.Apis().Links.ListForDeviceAsync(deviceId, cancellationToken); }
        public Task<IReadOnlyList<NetworkLink>> ListAllAsync(CancellationToken cancellationToken = default) => owner.Apis().Links.ListAllAsync(cancellationToken);
    }
    private sealed class TopologyFdbOnly(ConnectionBoundClient owner) : IFdbApi
    {
        public Task<IReadOnlyList<FdbEntry>> ListForDeviceAsync(int deviceId, CancellationToken cancellationToken = default)
        { PositiveTopologyId(deviceId); return owner.Apis().Fdb.ListForDeviceAsync(deviceId, cancellationToken); }
        public Task<IReadOnlyList<FdbEntry>> FindByMacAsync(string mac, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(FindByMacAsync));
        public Task<string?> FindVendorAsync(string mac, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(FindVendorAsync));
    }
    private sealed class TopologyVlansOnly(ConnectionBoundClient owner) : IVlansApi
    {
        public Task<IReadOnlyList<Vlan>> ListAsync(CancellationToken cancellationToken = default) => owner.Apis().Vlans.ListAsync(cancellationToken);
    }
}
