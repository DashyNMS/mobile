using DesktopNMS.Core.Api;
using DesktopNMS.Core.Devices;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Topology;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Topology;

/// <summary>
/// Everything a device is connected to, loaded the way desktop's Device
/// Details loads its Neighbours tab, so Network Search's Connected to and
/// Device View's Neighbours agree with it. Core's
/// <see cref="DeviceNeighbours.Build"/> only joins the links. Desktop also
/// ties neighbours LibreNMS didn't link to a monitored device, and names the
/// switch's port for a connection only the switch reports (an access point
/// or an antenna with no LLDP of its own). Without those steps the switch's
/// end reads "Unknown port". Kept in Mobile until the same steps move into
/// Core for both apps.
/// </summary>
public sealed class DeviceNeighbourLoader
{
    /// <summary>Most unlinked neighbours whose announced MAC is looked up in ARP - desktop's limit.</summary>
    internal const int MaxMacLookups = 32;

    /// <summary>Most neighbours whose port lists are fetched to name their end of a connection only they report - desktop's limit.</summary>
    internal const int MaxNeighbourPortLookups = 4;

    private readonly ILibreNmsClient _client;
    private readonly ILogger? _logger;

    public DeviceNeighbourLoader(ILibreNmsClient client, ILogger? logger = null)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// The device's neighbours from both sides' discovery.
    /// </summary>
    /// <param name="deviceId">The device.</param>
    /// <param name="ports">The device's own ports, for naming its end of each link.</param>
    /// <param name="devices">
    /// Monitored devices, for matching neighbours LibreNMS didn't link. Only
    /// asked for when there are any, so a device whose links are all linked
    /// never waits for the fleet list.
    /// </param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <remarks>
    /// A 404 for either links list means the device has no links. LibreNMS
    /// answers that way for a device with no discovery of its own, and the
    /// switch's report of it must still show. Other failures of the links
    /// lists throw. Matching and port naming are best effort, as on desktop:
    /// a failed lookup leaves that neighbour as LibreNMS reported it.
    /// </remarks>
    public async Task<IReadOnlyList<DeviceNeighbour>> LoadAsync(
        int deviceId,
        Task<IReadOnlyList<Port>> ports,
        Func<CancellationToken, Task<IReadOnlyCollection<Device>>> devices,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(devices);

        var own = LinksOrNoneAsync(() => _client.Links.ListForDeviceAsync(deviceId, cancellationToken));
        var fleet = LinksOrNoneAsync(() => _client.Links.ListAllAsync(cancellationToken));
        await Task.WhenAll(own, fleet, ports).ConfigureAwait(false);

        var matches = await MatchUnlinkedAsync(deviceId, own.Result, devices, cancellationToken).ConfigureAwait(false);
        var built = DeviceNeighbours.Build(
            deviceId,
            own.Result,
            fleet.Result,
            ports.Result.GroupBy(p => p.PortId).ToDictionary(g => g.Key, g => g.First().DisplayName),
            matches);

        return await NameNeighbourPortsAsync(built, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<NetworkLink>> LinksOrNoneAsync(Func<Task<IReadOnlyList<NetworkLink>>> load)
    {
        try
        {
            return await load().ConfigureAwait(false);
        }
        catch (LibreNmsApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return [];
        }
    }

    /// <summary>
    /// For each of the device's own links LibreNMS didn't link to a device
    /// (no remote_device_id), the monitored device it is when that can be told
    /// reliably, keyed by link id. The neighbour matches by its announced
    /// name (<see cref="NeighbourMatcher.MatchByName"/>). Failing that, a
    /// neighbour announcing its MAC as its port, as some antennas do, matches
    /// when that MAC's ARP entry leads to one device's IP. Only a single,
    /// unambiguous device counts. This is desktop's MatchUnlinkedNeighboursAsync.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, (int DeviceId, NeighbourMatchKind Kind)>> MatchUnlinkedAsync(
        int deviceId,
        IReadOnlyList<NetworkLink> own,
        Func<CancellationToken, Task<IReadOnlyCollection<Device>>> devices,
        CancellationToken cancellationToken)
    {
        var matches = new Dictionary<int, (int DeviceId, NeighbourMatchKind Kind)>();
        var unlinked = own
            .Where(l => l.Id > 0 && l.RemoteDeviceId is not > 0)
            .GroupBy(l => l.Id)
            .Select(g => g.First())
            .ToList();
        if (unlinked.Count == 0)
        {
            return matches;
        }

        IReadOnlyList<Device> others;
        try
        {
            others = (await devices(cancellationToken).ConfigureAwait(false)).Where(d => d.DeviceId != deviceId).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not OutOfMemoryException)
        {
            _logger?.LogDebug(ex, "No device list to match unlinked neighbours against");
            return matches;
        }

        if (others.Count == 0)
        {
            return matches;
        }

        var byMac = new List<(int LinkId, string Mac)>();
        foreach (var link in unlinked)
        {
            if (NeighbourMatcher.MatchByName(link.RemoteHostname, others) is { } byName)
            {
                matches[link.Id] = (byName, NeighbourMatchKind.Name);
            }
            else if (NeighbourMatcher.MacFromPortId(link.RemotePort) is { } mac)
            {
                byMac.Add((link.Id, mac));
            }
        }

        using var gate = new SemaphoreSlim(4);
        var lookups = byMac.Take(MaxMacLookups).Select(async item =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var entries = await _client.Arp.FindByMacAsync(item.Mac, cancellationToken).ConfigureAwait(false);
                var ids = entries
                    .Select(e => DeviceAddresses.Find(others, e.Ipv4Address))
                    .Where(d => d is not null)
                    .Select(d => d!.DeviceId)
                    .Distinct()
                    .Take(2)
                    .ToList();
                return (item.LinkId, DeviceId: ids.Count == 1 ? ids[0] : (int?)null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not OutOfMemoryException)
            {
                _logger?.LogDebug(ex, "ARP lookup for a neighbour's announced MAC failed");
                return (item.LinkId, DeviceId: (int?)null);
            }
            finally
            {
                gate.Release();
            }
        });

        foreach (var (linkId, matched) in await Task.WhenAll(lookups).ConfigureAwait(false))
        {
            if (matched is { } id)
            {
                matches[linkId] = (id, NeighbourMatchKind.Mac);
            }
        }

        return matches;
    }

    /// <summary>
    /// A connection only the neighbour reports names this device's port but
    /// not the neighbour's own. That name comes from the neighbour's port
    /// list. This is desktop's NameNeighbourPortsAsync, and it's why an
    /// access point shows the switch port it's plugged into. Best effort: a
    /// failure leaves "Unknown port".
    /// </summary>
    private async Task<IReadOnlyList<DeviceNeighbour>> NameNeighbourPortsAsync(IReadOnlyList<DeviceNeighbour> built, CancellationToken cancellationToken)
    {
        var wanted = built
            .Where(n => n.Source == NeighbourSource.Neighbour && n.RemoteDeviceId is > 0 && n.RemotePortId is > 0 && string.IsNullOrWhiteSpace(n.RemotePortName))
            .Select(n => n.RemoteDeviceId!.Value)
            .Distinct()
            .Take(MaxNeighbourPortLookups)
            .ToList();
        if (wanted.Count == 0)
        {
            return built;
        }

        var lookups = wanted.Select(async id =>
        {
            try
            {
                var ports = await _client.Ports.ListForDeviceAsync(id, cancellationToken).ConfigureAwait(false);
                return (Id: id, Names: (IReadOnlyDictionary<int, string>)ports.GroupBy(p => p.PortId).ToDictionary(g => g.Key, g => g.First().DisplayName));
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not OutOfMemoryException)
            {
                _logger?.LogDebug(ex, "Could not name the ports of a neighbour");
                return (Id: id, Names: (IReadOnlyDictionary<int, string>)new Dictionary<int, string>());
            }
        });
        var namesByDevice = (await Task.WhenAll(lookups).ConfigureAwait(false)).ToDictionary(x => x.Id, x => x.Names);

        return built
            .Select(n => n.Source == NeighbourSource.Neighbour
                         && n.RemoteDeviceId is { } device
                         && n.RemotePortId is { } port
                         && string.IsNullOrWhiteSpace(n.RemotePortName)
                         && namesByDevice.TryGetValue(device, out var names)
                         && names.TryGetValue(port, out var name)
                         && !string.IsNullOrWhiteSpace(name)
                ? n with { RemotePortName = name }
                : n)
            .ToList();
    }
}
