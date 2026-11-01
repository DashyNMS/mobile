using System.Globalization;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Graphs;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Topology;

namespace DashyNMS.Mobile.DeviceSections;

/// <summary>
/// Fetches one Device View section and turns it into titled groups of
/// <see cref="SectionRow"/>s. Reuses desktop's Core helpers - sensor
/// thresholds, neighbour matching, the inventory tree, wireless and routing
/// formatting - so a section reads the same as desktop's tab.
/// </summary>
public sealed class DeviceSectionLoader
{
    /// <summary>Event log entries fetched - desktop's default page size.</summary>
    internal const int EventLogLimit = 100;

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly CorePolling? _polling;

    public DeviceSectionLoader(ILibreNmsClient client, ISettingsStore settings, CorePolling? polling = null)
    {
        _client = client;
        _settings = settings;
        _polling = polling;
    }

    /// <summary>
    /// How old the sensor readings shown are, when the last fleet sensor list
    /// failed and Core kept the ones before it (DashyNMS/desktop#373).
    /// </summary>
    public string? SensorStatus => _polling?.LastSensors is { } last ? SensorRefreshStatus.Describe(last) : null;

    private bool ServerUtc => _settings.Current.ServerTimestampsAreUtc;

    /// <summary>
    /// The section's rows. An endpoint LibreNMS doesn't have for this device
    /// (404 - no VLANs, no wireless, an older LibreNMS without it) is a
    /// section with nothing in it, not a failure: the card drops out as an
    /// empty one does, rather than sitting there saying it couldn't load
    /// (#91). Anything else - a timeout, a 500, an expired token - still throws.
    /// </summary>
    public async Task<IReadOnlyList<SectionGroup>> LoadAsync(DeviceSection section, int deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Load(section, deviceId, cancellationToken);
        }
        catch (LibreNmsApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return [];
        }
    }

    private Task<IReadOnlyList<SectionGroup>> Load(DeviceSection section, int deviceId, CancellationToken cancellationToken) => section switch
    {
        DeviceSection.Availability => AvailabilityAsync(deviceId, cancellationToken),
        DeviceSection.Sensors => SensorsAsync(deviceId, cancellationToken),
        DeviceSection.Resources => ResourcesAsync(deviceId, cancellationToken),
        DeviceSection.Ports => PortsAsync(deviceId, cancellationToken),
        DeviceSection.Neighbours => NeighboursAsync(deviceId, cancellationToken),
        DeviceSection.Vlans => VlansAsync(deviceId, cancellationToken),
        DeviceSection.Fdb => FdbAsync(deviceId, cancellationToken),
        DeviceSection.Arp => ArpAsync(deviceId, cancellationToken),
        DeviceSection.Routing => RoutingAsync(deviceId, cancellationToken),
        DeviceSection.Wireless => WirelessAsync(deviceId, cancellationToken),
        DeviceSection.Inventory => InventoryAsync(deviceId, cancellationToken),
        DeviceSection.EventLog => EventLogAsync(deviceId, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Graphs have their own page."),
    };

    // ------------------------------------------------------------ availability

    private async Task<IReadOnlyList<SectionGroup>> AvailabilityAsync(int deviceId, CancellationToken ct)
    {
        var windowsTask = _client.Devices.GetAvailabilityAsync(deviceId, ct);
        var outagesTask = _client.Devices.GetOutagesAsync(deviceId, ct);
        await Task.WhenAll(windowsTask, outagesTask);

        var windows = windowsTask.Result
            .OrderBy(w => w.DurationSeconds)
            .Select(w => new SectionRow(Units.Window(w.DurationSeconds))
            {
                SortValue = w.DurationSeconds,
                Value = Units.Percent(w.Percent),
                Status = w.Percent >= 99.9 ? RowStatus.Ok : w.Percent >= 99 ? RowStatus.Warning : RowStatus.Critical,
            });

        var now = DateTime.Now;
        var outages = outagesTask.Result
            .Where(o => o.GoingDown is not null)
            .OrderByDescending(o => o.GoingDown)
            .Select(o =>
            {
                var down = ServerTime.ToLocal(o.GoingDown!.Value, ServerUtc);
                var up = ServerTime.ToLocal(o.UpAgain, ServerUtc);
                return new SectionRow("Down " + down.ToString("d MMM HH:mm", CultureInfo.CurrentCulture))
                {
                    Subtitle = up is { } back ? "Back " + back.ToString("d MMM HH:mm", CultureInfo.CurrentCulture) : "Still down",
                    Value = Units.Duration((up ?? now) - down),
                    Status = up is null ? RowStatus.Critical : RowStatus.None,
                };
            });

        return Groups(("Availability", windows), ("Outages", outages));
    }

    // ------------------------------------------------------------ sensors

    private async Task<IReadOnlyList<SectionGroup>> SensorsAsync(int deviceId, CancellationToken ct)
    {
        // No per-device endpoint: LibreNMS only lists every sensor at once,
        // so Device View shares the fleet list Health and the dashboard use.
        var sensors = (await FleetSensorsAsync(ct)).Where(s => s.DeviceId == deviceId);
        var settings = _settings.Current;

        return sensors
            .GroupBy(s => SensorClassName(s.SensorClass))
            .OrderBy(g => SensorClassRank(g.First().SensorClass))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SectionGroup(g.Key, g
                .OrderBy(s => SensorGrouping.ExtractGroupKey(s.Description), StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Description, StringComparer.OrdinalIgnoreCase)
                .Select(s => SensorRow(s, settings))))
            .ToList();
    }

    private async Task<IReadOnlyList<Sensor>> FleetSensorsAsync(CancellationToken ct)
    {
        if (_polling is null)
        {
            return await _client.Sensors.ListAsync(ct);
        }

        var result = await _polling.SensorsAsync(ct);
        return SensorRefreshStatus.HasNothingToShow(result)
            ? throw new InvalidOperationException(SensorRefreshStatus.Describe(result))
            : result.Sensors;
    }

    internal static SectionRow SensorRow(Sensor sensor, AppSettings settings)
    {
        var reading = ReadSensor(sensor, settings);
        return new SectionRow(reading.Name)
        {
            Value = reading.Value,
            Status = reading.Status,
            Subtitle = reading.Limits,
        };
    }

    /// <summary>
    /// A sensor's reading, coloured as desktop does: its category's thresholds
    /// from Settings (dBm, signal, temperature, fan speed - which also honour
    /// the sensor's own LibreNMS limits), or for anything else the sensor's
    /// own limits. Shared by Device View's Sensors section and the Health page,
    /// so the two can't disagree.
    /// </summary>
    internal static SensorReading ReadSensor(Sensor sensor, AppSettings settings)
    {
        if (!sensor.HasReading)
        {
            return new SensorReading(
                string.IsNullOrWhiteSpace(sensor.Description) ? $"Sensor {sensor.SensorId}" : sensor.Description!,
                GraphLegend.FormatSensor(sensor.Current, sensor.SensorClass, sensor.Unit), RowStatus.None, string.Empty);
        }

        var current = sensor.Current!.Value;
        var registered = SensorCategoryRegistry.Resolve(sensor.SensorClass);
        var severity = registered is not null
            ? registered.Thresholds(settings, sensor).Evaluate(current)
            : AgainstOwnLimits(current, sensor.LimitLow, sensor.LimitLowWarn, sensor.LimitHighWarn, sensor.LimitHigh);

        var unit = registered?.UnitSuffix ?? (string.IsNullOrWhiteSpace(sensor.Unit) ? UnitFor(sensor.SensorClass) : " " + sensor.Unit.Trim());
        var name = string.IsNullOrWhiteSpace(sensor.Description) ? $"Sensor {sensor.SensorId}" : sensor.Description!;
        var raw = GraphLegend.FormatSensor(sensor.Current, sensor.SensorClass, sensor.Unit);

        // A state sensor the definitions know (#210), as desktop's
        // (DashyNMS/desktop#468): its name, with the number beneath, and the
        // definition's severity - Unknown unless its source is authoritative,
        // so an unresolved or label-only state never shows OK.
        if (sensor.StateDefinition is { } state)
        {
            return state.Label is { Length: > 0 } label
                ? new SensorReading(name, label, ToStatus(state.Severity), "Raw value: " + raw)
                : new SensorReading(name, raw, ToStatus(state.Severity), null);
        }

        return new SensorReading(name, raw, ToStatus(severity), Limits(sensor.LimitLow, sensor.LimitHigh, unit));
    }

    private static string SensorClassName(string? sensorClass) =>
        SensorCategoryRegistry.Resolve(sensorClass)?.DisplayName
        ?? (string.IsNullOrWhiteSpace(sensorClass) ? "Other" : char.ToUpperInvariant(sensorClass[0]) + sensorClass[1..]);

    /// <summary>Desktop's own classes first, in its order; the rest alphabetically after.</summary>
    private static int SensorClassRank(string? sensorClass)
    {
        var known = SensorCategoryRegistry.KnownClasses;
        for (var i = 0; i < known.Count; i++)
        {
            if (string.Equals(known[i], sensorClass, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    /// <summary>Units for the sensor classes desktop's registry doesn't cover.</summary>
    private static string UnitFor(string? sensorClass) => sensorClass?.ToLowerInvariant() switch
    {
        "voltage" => " V",
        "current" => " A",
        "power" => " W",
        "power_consumed" => " kWh",
        "frequency" => " Hz",
        "humidity" or "load" or "percent" or "charge" => "%",
        "runtime" => " min",
        "snr" or "attenuation" or "loss" => " dB",
        "pressure" => " kPa",
        "airflow" => " cfm",
        "waterflow" => " l/m",
        "bitrate" => " bps",
        _ => string.Empty,
    };

    // ------------------------------------------------------------ resources

    private async Task<IReadOnlyList<SectionGroup>> ResourcesAsync(int deviceId, CancellationToken ct)
    {
        var cpuTask = _client.Health.ListProcessorsAsync(deviceId, ct);
        var memoryTask = _client.Health.ListMempoolsAsync(deviceId, ct);
        var storageTask = _client.Health.ListStorageAsync(deviceId, ct);
        await Task.WhenAll(cpuTask, memoryTask, storageTask);

        var cpus = cpuTask.Result.Select(p => UsageRow(p.Description ?? "Processor", p.UsagePercent, p.WarningPercent, null, null));
        var memory = memoryTask.Result.Select(m => UsageRow(m.Description ?? "Memory", m.UsagePercent, m.WarningPercent, m.UsedBytes, m.TotalBytes));
        var storage = storageTask.Result
            .OrderBy(s => s.Description, StringComparer.OrdinalIgnoreCase)
            .Select(s => UsageRow(s.Description ?? "Storage", s.UsagePercent, s.WarningPercent, s.UsedBytes, s.TotalBytes));

        return Groups(("Processors", cpus), ("Memory", memory), ("Storage", storage));
    }

    internal static SectionRow UsageRow(string name, double? percent, double? warning, long? used, long? total) => new(name)
    {
        Value = Units.Percent(percent),
        Bar = percent is { } p ? Math.Clamp(p / 100, 0, 1) : null,
        Status = percent is not { } value ? RowStatus.None
            : value >= Math.Max(warning ?? 90, 95) ? RowStatus.Critical
            : value >= (warning ?? 80) ? RowStatus.Warning
            : RowStatus.Ok,
        Subtitle = used is not null && total is > 0 ? $"{Units.Bytes(used)} of {Units.Bytes(total)}" : null,
    };

    // ------------------------------------------------------------ ports

    private async Task<IReadOnlyList<SectionGroup>> PortsAsync(int deviceId, CancellationToken ct)
    {
        var ports = (await _client.Ports.ListForDeviceAsync(deviceId, ct)).Where(p => !p.Deleted).ToList();

        // Down but meant to be up first - the ones that need looking at.
        var down = ports.Where(p => !p.IsUp && !IsAdminDown(p));
        var up = ports.Where(p => p.IsUp);
        var shut = ports.Where(p => !p.IsUp && IsAdminDown(p));

        return Groups(
            ("Down", down.OrderBy(p => p.IfIndex ?? int.MaxValue).Select(PortRow)),
            ("Up", up.OrderBy(p => p.IfIndex ?? int.MaxValue).Select(PortRow)),
            ("Shut down", shut.OrderBy(p => p.IfIndex ?? int.MaxValue).Select(PortRow)));
    }

    private static bool IsAdminDown(Port port) =>
        string.Equals(port.IfAdminStatus, "down", StringComparison.OrdinalIgnoreCase) || port.Disabled;

    internal static SectionRow PortRow(Port port)
    {
        var errors = (port.IfInErrorsRate ?? 0) + (port.IfOutErrorsRate ?? 0);
        var (rank, state) = port.IsUp ? (1, "Up") : IsAdminDown(port) ? (2, "Shut down") : (0, "Down");
        return new SectionRow(port.DisplayName)
        {
            Port = PortFacts.From(port, rank, state),
            // Tap for its graphs - as desktop's port graphs panel.
            LinkPortIfName = string.IsNullOrWhiteSpace(port.IfName) ? null : port.IfName,
            SortValue = port.IsUp ? (port.IfInOctetsRate ?? 0) + (port.IfOutOctetsRate ?? 0) : 0,
            Subtitle = port.IfAlias is { Length: > 0 } alias && alias != port.DisplayName ? alias : null,
            Value = port.IsUp ? $"↓{Units.Bits(port.IfInOctetsRate * 8)}  ↑{Units.Bits(port.IfOutOctetsRate * 8)}" : IsAdminDown(port) ? "Shut down" : "Down",
            Status = port.IsUp ? (errors > 0 ? RowStatus.Warning : RowStatus.Ok) : IsAdminDown(port) ? RowStatus.Inactive : RowStatus.Critical,
            Detail = string.Join(" · ", new[]
            {
                port.IfSpeed is > 0 ? Units.Speed(port.IfSpeed) : null,
                port.IfVlan is { } vlan and > 0 ? $"VLAN {vlan}" : null,
                errors > 0 ? $"{errors.ToString("0.##", CultureInfo.CurrentCulture)} errors/s" : null,
            }.Where(s => s is not null)),
        };
    }

    // ------------------------------------------------------------ neighbours

    private async Task<IReadOnlyList<SectionGroup>> NeighboursAsync(int deviceId, CancellationToken ct)
    {
        var ownTask = _client.Links.ListForDeviceAsync(deviceId, ct);
        var fleetTask = _client.Links.ListAllAsync(ct);
        var portsTask = NeighbourPortsAsync(deviceId, ct);
        await Task.WhenAll(ownTask, fleetTask, portsTask);

        var neighbours = DeviceNeighbours.Build(
            deviceId,
            ownTask.Result,
            fleetTask.Result,
            portsTask.Result.ToDictionary(p => p.PortId, p => p.DisplayName),
            new Dictionary<int, (int, NeighbourMatchKind)>());
        var names = await SeenByNamesAsync(neighbours, ct);

        var rows = neighbours
            .Select(n => (Neighbour: n, Name: n.RemoteDeviceId is { } id && names.TryGetValue(id, out var name) ? name : n.RemoteName))
            .OrderBy(x => x.Neighbour.LocalPortName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => NeighbourRow(x.Neighbour, x.Name, portsTask.Result.FirstOrDefault(p => p.PortId == x.Neighbour.LocalPortId)));

        return Groups(("Neighbours", rows));
    }

    /// <summary>
    /// Readable whether or not LibreNMS monitors it (DashyNMS/desktop#448, #471):
    /// one it doesn't says so, and has nothing to open.
    /// </summary>
    internal static SectionRow NeighbourRow(DeviceNeighbour neighbour, string name, Port? port = null)
    {
        var known = neighbour.RemoteDeviceId is > 0;
        return new SectionRow(name)
        {
            Subtitle = $"{neighbour.LocalPortName ?? "Unknown port"} → {neighbour.RemotePortName ?? "Unknown port"}",
            Value = string.Join("/", neighbour.Protocols.Select(p => p.ToUpperInvariant())),
            Detail = known ? neighbour.Platform : string.Join(" · ", new[] { neighbour.Platform, NotMonitored }.Where(s => !string.IsNullOrWhiteSpace(s))),
            Fields = NeighbourFields(neighbour, name, port),
            Status = !neighbour.Active ? RowStatus.Inactive : string.IsNullOrWhiteSpace(port?.IfOperStatus) ? RowStatus.None : port!.IsUp ? RowStatus.Ok : RowStatus.Critical,
            LinkDeviceId = known ? neighbour.RemoteDeviceId : null,
        };
    }

    /// <summary>Keep discovery direction and unknown port readings explicit, including reverse-only neighbours.</summary>
    internal static IReadOnlyList<SectionField> NeighbourFields(DeviceNeighbour neighbour, string name, Port? port) =>
    [
        new("Link", !neighbour.Active ? "Not active" : string.IsNullOrWhiteSpace(port?.IfOperStatus) ? "State unknown" : port!.IsUp ? "Up" : "Down"),
        new("Speed", port?.IfSpeed is > 0 ? Units.Speed(port.IfSpeed) : "Unknown"),
        new("Traffic in", port?.IfInOctetsRate is { } incoming ? Units.Bits(incoming * 8) : "Unknown"),
        new("Traffic out", port?.IfOutOctetsRate is { } outgoing ? Units.Bits(outgoing * 8) : "Unknown"),
        new("Seen by", (neighbour.Source == NeighbourSource.ThisDevice ? "This device" : name)
            + " via " + string.Join("/", neighbour.Protocols.Select(p => p.ToUpperInvariant()))),
    ];

    /// <summary>Port readings are optional: ping-only devices still have reverse-discovered neighbours.</summary>
    private async Task<IReadOnlyList<Port>> NeighbourPortsAsync(int deviceId, CancellationToken ct)
    {
        try
        {
            return await _client.Ports.ListForDeviceAsync(deviceId, ct);
        }
        catch (LibreNmsApiException ex) when (!ex.IsAuthenticationFailure)
        {
            return [];
        }
    }

    internal const string NotMonitored = "Not monitored in LibreNMS";

    /// <summary>
    /// Names for the devices that see this one but that it doesn't report
    /// itself - Core knows them only as "Device 12". Usually one or two (an
    /// access point's switch), so each is looked up on its own; one that
    /// can't be found keeps Core's placeholder.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, string>> SeenByNamesAsync(IReadOnlyList<DeviceNeighbour> neighbours, CancellationToken ct)
    {
        var style = _settings.Current.DeviceNameStyle;
        var lookups = neighbours
            .Where(n => n.Source == NeighbourSource.Neighbour && n.RemoteDeviceId is > 0)
            .Select(n => n.RemoteDeviceId!.Value)
            .Distinct()
            .Select(async id =>
            {
                try
                {
                    var device = await _client.Devices.GetAsync(id.ToString(CultureInfo.InvariantCulture), ct);
                    return (Id: id, Name: device is null ? null : style.Resolve(device, device.Hostname));
                }
                catch (LibreNmsApiException)
                {
                    return (Id: id, Name: (string?)null);
                }
            });

        return (await Task.WhenAll(lookups))
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToDictionary(x => x.Id, x => x.Name!);
    }

    // ------------------------------------------------------------ vlans, fdb, arp

    private async Task<IReadOnlyList<SectionGroup>> VlansAsync(int deviceId, CancellationToken ct)
    {
        // Like sensors, VLANs only come fleet-wide.
        var vlansTask = _client.Vlans.ListAsync(ct);
        var portsTask = _client.Ports.ListForDeviceAsync(deviceId, ct);
        await Task.WhenAll(vlansTask, portsTask);

        var portsPerVlan = portsTask.Result
            .SelectMany(p => p.Vlans.Select(v => v.Vlan).Append(p.IfVlan ?? 0).Where(v => v > 0).Distinct())
            .GroupBy(v => v)
            .ToDictionary(g => g.Key, g => g.Count());

        var rows = vlansTask.Result
            .Where(v => v.DeviceId == deviceId)
            .OrderBy(v => v.VlanNumber)
            .Select(v => new SectionRow($"VLAN {v.VlanNumber}")
            {
                Subtitle = v.VlanName,
                Value = portsPerVlan.TryGetValue(v.VlanNumber, out var count) ? $"{count} {(count == 1 ? "port" : "ports")}" : null,
            });

        return Groups(("VLANs", rows));
    }

    private async Task<IReadOnlyList<SectionGroup>> FdbAsync(int deviceId, CancellationToken ct)
    {
        var fdbTask = _client.Fdb.ListForDeviceAsync(deviceId, ct);
        var portsTask = _client.Ports.ListForDeviceAsync(deviceId, ct);
        await Task.WhenAll(fdbTask, portsTask);

        var ports = portsTask.Result.ToDictionary(p => p.PortId, p => p.DisplayName);
        var rows = fdbTask.Result
            .OrderBy(e => ports.GetValueOrDefault(e.PortId), StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.MacAddress, StringComparer.OrdinalIgnoreCase)
            .Select(e => new SectionRow(Mac(e.MacAddress))
            {
                Subtitle = ports.GetValueOrDefault(e.PortId) ?? $"port {e.PortId}",
                Value = e.VlanId is > 0 ? $"VLAN {e.VlanId}" : null,
            });

        return Groups(("MAC addresses", rows));
    }

    private async Task<IReadOnlyList<SectionGroup>> ArpAsync(int deviceId, CancellationToken ct)
    {
        var arpTask = _client.Arp.ListForDeviceAsync(deviceId, ct);
        var portsTask = _client.Ports.ListForDeviceAsync(deviceId, ct);
        await Task.WhenAll(arpTask, portsTask);

        var ports = portsTask.Result.ToDictionary(p => p.PortId, p => p.DisplayName);
        var rows = arpTask.Result
            .OrderBy(e => IpSortKey(e.Ipv4Address))
            .Select(e => new SectionRow(e.Ipv4Address ?? "—")
            {
                Subtitle = Mac(e.MacAddress),
                Value = ports.GetValueOrDefault(e.PortId),
            });

        return Groups(("ARP entries", rows));
    }

    /// <summary>aa:bb:cc:dd:ee:ff from however LibreNMS stored it (often bare hex).</summary>
    internal static string Mac(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "—";
        }

        var hex = new string(raw.Where(Uri.IsHexDigit).ToArray()).ToLowerInvariant();
        return hex.Length == 12
            ? string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)))
            : raw;
    }

    private static string IpSortKey(string? ip) =>
        System.Net.IPAddress.TryParse(ip, out var address)
            ? string.Concat(address.GetAddressBytes().Select(b => b.ToString("D3", CultureInfo.InvariantCulture)))
            : "~" + ip;

    // ------------------------------------------------------------ routing

    private async Task<IReadOnlyList<SectionGroup>> RoutingAsync(int deviceId, CancellationToken ct)
    {
        var bgpTask = _client.Routing.ListBgpSessionsAsync(deviceId, ct);
        var ospfTask = _client.Routing.ListOspfNeighboursAsync(deviceId, ct);
        var ospf3Task = _client.Routing.ListOspfv3NeighboursAsync(deviceId, ct);
        var vrfTask = _client.Routing.ListVrfsAsync(deviceId, ct);
        await Task.WhenAll(bgpTask, ospfTask, ospf3Task, vrfTask);

        var bgp = bgpTask.Result
            .OrderBy(b => b.IsEstablished)
            .ThenBy(b => b.PeerAddressText, StringComparer.OrdinalIgnoreCase)
            .Select(b => new SectionRow(b.PeerAddressText)
            {
                Subtitle = string.Join(" · ", new[] { $"AS{b.RemoteAs}", b.AsText, b.Description }.Where(s => !string.IsNullOrWhiteSpace(s))),
                Value = b.IsAdminDown ? "Shut down" : b.State,
                Status = b.IsAdminDown ? RowStatus.Inactive : b.IsEstablished ? RowStatus.Ok : RowStatus.Critical,
                Detail = b.IsEstablished ? "Up " + Units.Duration(TimeSpan.FromSeconds(b.EstablishedSeconds)) : b.LastErrorText,
            });

        var ospf = ospfTask.Result.Select(n => OspfRow(n.IpAddress, n.RouterId, n.State, n.ContextName))
            .Concat(ospf3Task.Result.Select(n => OspfRow(n.Address, n.RouterId, n.State, n.ContextName)));

        var vrfs = vrfTask.Result
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .Select(v => new SectionRow(v.Name ?? "VRF")
            {
                Subtitle = v.Description,
                Value = v.RouteDistinguisher,
            });

        return Groups(("BGP sessions", bgp), ("OSPF neighbours", ospf), ("VRFs", vrfs));
    }

    private static SectionRow OspfRow(string? address, string? routerId, string? state, string? context) =>
        new(RoutingText.Address(address) is { Length: > 0 } a ? a : "—")
        {
            Subtitle = string.Join(" · ", new[] { routerId is null ? null : "Router " + routerId, context }.Where(s => !string.IsNullOrWhiteSpace(s))),
            Value = state,
            Status = state?.StartsWith("full", StringComparison.OrdinalIgnoreCase) == true ? RowStatus.Ok : RowStatus.Warning,
        };

    // ------------------------------------------------------------ wireless, inventory, events

    private async Task<IReadOnlyList<SectionGroup>> WirelessAsync(int deviceId, CancellationToken ct)
    {
        var sensors = (await _client.Devices.GetWirelessSensorsAsync(deviceId, ct)).Where(s => !s.Deleted);

        return sensors
            .GroupBy(s => s.SensorClass ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => WirelessSensorClasses.SortRank(g.Key))
            .Select(g => new SectionGroup(WirelessSensorClasses.NameOf(g.Key), g
                .OrderBy(s => s.Description, StringComparer.OrdinalIgnoreCase)
                .Select(s => new SectionRow(string.IsNullOrWhiteSpace(s.Description) ? $"Sensor {s.SensorId}" : s.Description!)
                {
                    Value = s.Current is { } value && double.IsFinite(value)
                        ? WirelessSensorClasses.Format(s.SensorClass, value) : "–",
                    Status = s.Current is { } v && double.IsFinite(v)
                        ? ToStatus(AgainstOwnLimits(v, s.LimitLow, s.LimitLowWarn, s.LimitHighWarn, s.LimitHigh))
                        : RowStatus.None,
                })))
            .ToList();
    }

    private async Task<IReadOnlyList<SectionGroup>> InventoryAsync(int deviceId, CancellationToken ct)
    {
        var tree = InventoryTree.Build(await _client.Devices.GetInventoryAsync(deviceId, ct));

        var rows = tree
            .SelectMany(root => root.DescendantsAndSelf())
            .Select(node => new SectionRow(node.Entry.DisplayName)
            {
                Depth = node.Depth,
                InventoryIndex = node.Entry.Index,
                IsInventoryPort = node.Entry.IsPort,
                Serial = node.Entry.Serial,
                Subtitle = string.Join(" · ", new[] { node.Entry.Description, node.Entry.Model, node.Entry.Manufacturer }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct()),
                Detail = string.Join(" · ", new[] { string.IsNullOrWhiteSpace(node.Entry.Serial) ? null : "Serial " + node.Entry.Serial, string.IsNullOrWhiteSpace(node.Entry.FirmwareRev) ? null : "Firmware " + node.Entry.FirmwareRev }.Where(s => !string.IsNullOrWhiteSpace(s))),
                Value = node.Entry.Class,
            });

        return Groups(("Inventory", rows));
    }

    private async Task<IReadOnlyList<SectionGroup>> EventLogAsync(int deviceId, CancellationToken ct)
    {
        var entries = await _client.Logs.ListEventLogAsync(deviceId, EventLogLimit, ct);

        var rows = entries
            .OrderByDescending(e => e.Timestamp)
            .Select(e => new SectionRow(e.Message ?? "(no message)")
            {
                Subtitle = string.Join(" · ", new[]
                {
                    ServerTime.ToLocal(e.Timestamp, ServerUtc)?.ToString("d MMM HH:mm:ss", CultureInfo.CurrentCulture),
                    e.Type,
                    e.Username,
                }.Where(s => !string.IsNullOrWhiteSpace(s))),
                Status = EventStatus(e.Severity),
            });

        return Groups(("Events", rows));
    }

    /// <summary>
    /// LibreNMS's event severities: 1 ok, 2 info, 3 notice, 4 warning,
    /// 5 critical. Desktop doesn't colour these yet; this does.
    /// </summary>
    internal static RowStatus EventStatus(int? severity) => severity switch
    {
        5 => RowStatus.Critical,
        4 => RowStatus.Warning,
        1 => RowStatus.Ok,
        _ => RowStatus.None,
    };

    // ------------------------------------------------------------ helpers

    /// <summary>Only the groups with anything in them, in the order given.</summary>
    private static IReadOnlyList<SectionGroup> Groups(params (string Name, IEnumerable<SectionRow> Rows)[] groups) =>
        groups
            .Select(g => new SectionGroup(g.Name, g.Rows))
            .Where(g => g.Count > 0)
            .ToList();

    /// <summary>For sensors with no app-wide thresholds: against the sensor's own LibreNMS limits.</summary>
    internal static AlertSeverity AgainstOwnLimits(double value, double? low, double? lowWarn, double? highWarn, double? high) =>
        (high is { } h && value >= h) || (low is { } l && value <= l) ? AlertSeverity.Critical
        : (highWarn is { } hw && value >= hw) || (lowWarn is { } lw && value <= lw) ? AlertSeverity.Warning
        : AlertSeverity.Ok;

    private static RowStatus ToStatus(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => RowStatus.Critical,
        AlertSeverity.Warning => RowStatus.Warning,
        AlertSeverity.Ok => RowStatus.Ok,
        _ => RowStatus.None,
    };

    private static string? Limits(double? low, double? high, string unit) => (low, high) switch
    {
        ({ } l, { } h) => $"Limits {l.ToString("0.##", CultureInfo.CurrentCulture)} to {h.ToString("0.##", CultureInfo.CurrentCulture)}{unit}",
        (null, { } h) => $"Limit {h.ToString("0.##", CultureInfo.CurrentCulture)}{unit}",
        ({ } l, null) => $"Lower limit {l.ToString("0.##", CultureInfo.CurrentCulture)}{unit}",
        _ => null,
    };
}

/// <summary>A sensor's name, reading and colour - see <see cref="DeviceSectionLoader.ReadSensor"/>.</summary>
internal sealed record SensorReading(string Name, string Value, RowStatus Status, string? Limits);
