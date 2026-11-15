using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Intelligence;

internal sealed partial class ConnectionBoundClient
{
    private Task<T> ReadDetailsAsync<T>(int deviceId, Func<LibreNmsResourceApis, Task<T>> read)
    {
        if (!detailProfile || deviceId <= 0) throw new RefusedException("Device detail read outside its reviewed profile");
        return read(Apis());
    }
    private sealed class DetailPortsOnly(ConnectionBoundClient owner) : IPortsApi
    {
        public Task<IReadOnlyList<Port>> ListForDeviceAsync(int deviceId, CancellationToken cancellationToken = default)
            => owner.ReadDetailsAsync(deviceId, apis => apis.Ports.ListForDeviceAsync(deviceId, cancellationToken));
        public Task<IReadOnlyList<DeviceIpAddress>> ListIpAddressesAsync(int deviceId, CancellationToken cancellationToken = default)
            => owner.ReadDetailsAsync(deviceId, apis => apis.Ports.ListIpAddressesAsync(deviceId, cancellationToken));
        public Task<IReadOnlyList<Port>> ListAllStatusAsync(CancellationToken cancellationToken = default) => throw new RefusedException(nameof(ListAllStatusAsync));
        public Task<IReadOnlyList<Port>> ListAllNamesAsync(CancellationToken cancellationToken = default) => throw new RefusedException(nameof(ListAllNamesAsync));
    }
    private sealed class DetailArpOnly(ConnectionBoundClient owner) : IArpApi
    {
        public Task<IReadOnlyList<ArpEntry>> ListForDeviceAsync(int deviceId, CancellationToken cancellationToken = default)
            => owner.ReadDetailsAsync(deviceId, apis => apis.Arp.ListForDeviceAsync(deviceId, cancellationToken));
        public Task<IReadOnlyList<ArpEntry>> FindByMacAsync(string mac, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(FindByMacAsync));
        public Task<IReadOnlyList<ArpEntry>> ListAllAsync(CancellationToken cancellationToken = default) => throw new RefusedException(nameof(ListAllAsync));
    }
    private sealed class DetailRoutingOnly(ConnectionBoundClient owner) : IRoutingApi
    {
        public Task<IReadOnlyList<BgpSession>> ListBgpSessionsAsync(int deviceId, CancellationToken cancellationToken = default)
            => owner.ReadDetailsAsync(deviceId, apis => apis.Routing.ListBgpSessionsAsync(deviceId, cancellationToken));
        public Task<IReadOnlyList<OspfNeighbour>> ListOspfNeighboursAsync(int deviceId, CancellationToken cancellationToken = default)
            => owner.ReadDetailsAsync(deviceId, apis => apis.Routing.ListOspfNeighboursAsync(deviceId, cancellationToken));
        public Task<IReadOnlyList<Ospfv3Neighbour>> ListOspfv3NeighboursAsync(int deviceId, CancellationToken cancellationToken = default)
            => owner.ReadDetailsAsync(deviceId, apis => apis.Routing.ListOspfv3NeighboursAsync(deviceId, cancellationToken));
        public Task<IReadOnlyList<Vrf>> ListVrfsAsync(int deviceId, CancellationToken cancellationToken = default)
            => owner.ReadDetailsAsync(deviceId, apis => apis.Routing.ListVrfsAsync(deviceId, cancellationToken));
    }
}
