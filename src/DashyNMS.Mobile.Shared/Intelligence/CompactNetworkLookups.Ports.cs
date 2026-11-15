using DashyNMS.Agent;
using DashyNMS.Agent.Ports;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Intelligence;

public sealed partial class CompactNetworkLookups
{
    private static Reply PortFailure(HostFailure? error) => Reply.Say(error switch
    {
        HostFailure.Refused => Refused,
        HostFailure.InvalidRequest => "The port or address is invalid, absent or ambiguous. Give an exact port ID/name, or a MAC/IPv4 address.",
        _ => Failed,
    });

    private static Row TopologyDevice(int id, string? name, string text, DateTimeOffset at)
    {
        if (id <= 0) return new(text);
        return new(text + $" [[device:{Id(id)}]]", [new(new("device:" + Id(id), Clean(name), "", "unknown", at, "Device identified by stored topology data")
            { Object = new("device", id) })]);
    }

    private static Reply PortRows(PortStatusResult found, DateTimeOffset at, string name)
    {
        var data = found.Data;
        var port = data.Port;
        var rows = new List<Row>
        {
            TopologyDevice(data.Device.DeviceId, name, $"{name}; hostname {Clean(data.Device.Hostname)}; display {Clean(data.Device.Display)}; IP {Clean(data.Device.Ip)}; location {Clean(data.Device.Location)}; state {data.Device.State.ToDisplayString()}", at),
            new($"Port {Id(port.PortId)} {Clean(port.IfName)}; description {Clean(port.IfDescr)}; alias {Clean(port.Alias)}."),
            new($"Admin {Clean(port.AdminStatus)}; operational {Clean(port.OperStatus)}; speed {Count(port.SpeedMbps)} Mbps; duplex {Clean(port.Duplex)}; MTU {Count(port.Mtu)}; VLAN {Count(port.Vlan)}."),
            new($"Traffic in {Number(port.InMbps)} Mbps; out {Number(port.OutMbps)} Mbps. Errors in {Number(port.InErrorsPerSec)}/s; out {Number(port.OutErrorsPerSec)}/s; last poll in {Count(port.InErrorsLastPoll)}; out {Count(port.OutErrorsLastPoll)}."),
        };
        if (port.DisabledInLibrenms is { } disabled) rows.Add(new("Disabled in LibreNMS: " + (disabled ? "yes" : "no")));
        rows.AddRange((port.Vlans ?? []).Select(v => new Row($"VLAN {Id(v.Vlan)}: {(v.Untagged ? "untagged" : "tagged")}.")));
        rows.AddRange((data.Neighbours ?? []).Select(n => TopologyDevice(n.RemoteDeviceId ?? 0, n.RemoteHostname,
            $"Neighbour {Clean(n.RemoteHostname)}; {Clean(n.Protocol)}; port {Clean(n.RemotePort)}; platform {Clean(n.RemotePlatform)}.", at)));
        return Rows(AlertUnavailable(data.Unavailable) + found.Summary, rows);
    }

    private static Reply NeighbourRows(DeviceNeighboursResult found, DateTimeOffset at, string name)
    {
        var device = found.Data.Device;
        var heading = TopologyDevice(device.DeviceId, name, $"{name}; hostname {Clean(device.Hostname)}; display {Clean(device.Display)}; IP {Clean(device.Ip)}; location {Clean(device.Location)}; state {Clean(device.Status)}", at);
        return Rows(AlertUnavailable(found.Data.Unavailable) + found.Summary,
            new[] { heading }.Concat(found.Data.Neighbours.Select(n => TopologyDevice(n.RemoteDeviceId ?? 0, n.RemoteHostname,
                $"Local {Clean(n.LocalPort)} → {Clean(n.RemoteHostname)}; remote port {Clean(n.RemotePort)}; platform {Clean(n.RemotePlatform)}; {Clean(n.Protocol)}; seen from {Clean(n.SeenFrom)}.", at))),
            alsoOmitted: found.Omitted);
    }

    internal static Reply TraceRows(TraceEndpointResult found, DateTimeOffset at, TimeProvider time)
    {
        var data = found.Data;
        var rows = new List<Row>();
        Row Observation(string label, EndpointObservation o) => TopologyDevice(o.DeviceId, o.Device,
            $"{label}: {Clean(o.Device)} port {Id(o.PortId)} {Clean(o.Port)}; alias {Clean(o.Alias)}; {Clean(o.OperStatus)}; {Count(o.SpeedMbps)} Mbps; role {Clean(o.Role)}; neighbour {Clean(o.Neighbour)}; MACs {Count(o.MacsOnPort)}"
            + (o.Vlans is { Count: > 0 } vlans ? "; VLANs " + string.Join(",", vlans.Select(Id)) : "")
            + (o.LastSeen is { } last ? "; last seen " + IntelligenceTime.AtAndAgo(last, at, time) : ""), at);
        void Branch(string? mac, bool? located, bool? ambiguous, string? reason, EndpointObservation? edge,
            IReadOnlyList<EndpointObservation>? candidates, IReadOnlyList<string?>? path, IReadOnlyList<EndpointObservation>? seen)
        {
            rows.Add(new($"MAC {Clean(mac)}; found {(located is { } f ? f ? "yes" : "no" : "unknown")}; ambiguous {(ambiguous is { } a ? a ? "yes" : "no" : "unknown")}. {Clean(reason)}"));
            if (edge is not null) rows.Add(Observation("Likely edge", edge));
            rows.AddRange((candidates ?? []).Select(o => Observation("Other candidate", o)));
            if (path is { Count: > 0 }) rows.Add(new("Stored path: " + string.Join(" → ", path.Select(Clean))));
            rows.AddRange((seen ?? []).Select(o => Observation("Seen on", o)));
        }
        if (data.Query is not null || data.Ip is not null) rows.Add(new("Query " + Clean(data.Query ?? data.Ip)
            + (data.Ip is { } ip ? "; IP " + Clean(ip) : "")));
        if (data.Ips is { Count: > 0 }) rows.Add(new("Observed IPs: " + string.Join(", ", data.Ips.Select(Clean))));
        if (data.Traces is { } traces)
            foreach (var branch in traces) Branch(branch.Mac, branch.Found, branch.Ambiguous, branch.Reason, branch.Edge, branch.OtherCandidates, branch.Path, branch.SeenOn);
        else Branch(data.Mac, data.Found, data.Ambiguous, data.Reason, data.Edge, data.OtherCandidates, data.Path, data.SeenOn);
        var failures = (data.Unavailable ?? []).Concat(found.Observations.SelectMany(o => o.Unavailable)).DistinctBy(u => u.Source).ToList();
        // Per-device failures can exceed the entire reply budget. Keep every failure
        // category and its count in the heading rather than losing the partial notice.
        var categories = failures.Where(u => u.Source != "switches").GroupBy(u =>
            u.Source.StartsWith("ports on device ", StringComparison.Ordinal) ? "ports on devices"
            : u.Source.StartsWith("MAC table on device ", StringComparison.Ordinal) ? "MAC tables on devices" : u.Source)
            .Select(g => Clean(g.Key) + (g.Count() > 1 ? $" ({Id(g.Count())})" : ""));
        var notice = failures.Any(u => u.Source != "switches") ? "Unavailable: " + string.Join(", ", categories) + ". " : "";
        if (failures.FirstOrDefault(u => u.Source == "switches") is { } limited) notice += "Limited switch coverage: " + Clean(limited.Message) + " ";
        var heading = notice + "Stored ARP/FDB observations, not proof of ownership. " + Clean(found.Summary);
        return Rows(heading, rows, alsoOmitted: found.Omitted + (data.Traces is null ? 0 : found.Observations.Sum(o => o.Omitted)));
    }

    private static string Count(long? value) => value is { } count ? count.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unknown";
    internal static string Number(double? value) => value is { } number ? number.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "unknown";
}
