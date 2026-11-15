using DashyNMS.Agent.Devices;

namespace DashyNMS.Mobile.Intelligence;

public sealed partial class CompactNetworkLookups
{
    internal static Reply DeviceDetailRows(DeviceDetailsResult found, DateTimeOffset at, TimeProvider time, bool timestampsAreUtc, string name)
    {
        var data = found.Data;
        var device = data.Device;
        var rows = new List<Row>
        {
            TopologyDevice(device.DeviceId, name, $"{name}; hostname {Clean(device.Hostname)}; display {Clean(device.Display)}; IP {Clean(device.Ip)}; location {Clean(device.Location)}; state {Clean(device.Status)}", at),
        };
        var pages = data.Pages;
        if (pages is not null)
            rows.AddRange(pages.Select(p => new Row($"{Clean(p.Key)} page: offset {Id(p.Value.Offset)}; limit {Id(p.Value.Limit)}; returned {Id(p.Value.Returned)}; total {Id(p.Value.Total)}"
                + (p.Value.NextOffset is { } next ? $"; next offset {Id(next)}" : "") + ".")));
        if (data.Inventory is { } inventory)
            rows.AddRange(inventory.Select(i => new Row($"Inventory {Clean(i.Name)}; class {Clean(i.Class)}; description {Clean(i.Description)}; model {Clean(i.Model)}; serial {Clean(i.Serial)}; manufacturer {Clean(i.Manufacturer)}; hardware rev {Clean(i.HardwareRev)}; firmware rev {Clean(i.FirmwareRev)}; software rev {Clean(i.SoftwareRev)}; replaceable {YesNo(i.Replaceable)}.")));
        if (data.Vlans is { } vlans)
            rows.AddRange(vlans.Select(v => new Row($"VLAN {Id(v.Vlan)} {Clean(v.Name)}; untagged ports {Names(v.UntaggedPorts)}; tagged ports {Names(v.TaggedPorts)}.")));
        if (data.Fdb is { } fdb)
            rows.AddRange(fdb.Select(f => new Row($"FDB {Clean(f.Mac)}; port {Id(f.PortId)} {Clean(f.Port)}; VLAN {Count(f.Vlan)}"
                + (f.UpdatedAt is { } updated ? "; updated " + IntelligenceTime.AtAndAgo(DesktopNMS.Core.ServerTime.ToLocal(updated, timestampsAreUtc).ToUniversalTime(), at, time) : "") + ".")));
        if (data.Arp is { } arp)
            rows.AddRange(arp.Select(a => new Row($"ARP {Clean(a.Ip)}; MAC {Clean(a.Mac)}; port {Clean(a.Port)}.")));
        if (data.Poe is { } poe)
        {
            if (poe.None is { } none) rows.Add(new("PoE: " + Clean(none)));
            if (poe.DevicesConnected is { } connected) rows.Add(new($"PoE connected devices: {Id(connected)}."));
            rows.AddRange((poe.Budgets ?? []).Select(p => new Row($"PoE {Clean(p.Label)}; used {Number(p.UsedWatts)} W; total {Number(p.TotalWatts)} W; remaining {Number(p.RemainingWatts)} W; used {Number(p.UsedPercent)}%.")));
        }
        if (data.Wireless is { } wireless)
            rows.AddRange(wireless.Select(w => new Row($"Wireless {Clean(w.Type)} {Clean(w.Description)}; value {Number(w.Value)}; state {Clean(w.State)}.")));
        if (data.Routing is { } routing)
        {
            rows.AddRange(routing.BgpSessions.Select(b => new Row($"BGP {Clean(b.Peer)}; remote AS {b.RemoteAs.ToString(System.Globalization.CultureInfo.InvariantCulture)} {Clean(b.RemoteAsName)}; description {Clean(b.Description)}; state {Clean(b.State)}; admin down {YesNo(b.AdminDown)}; established seconds {Count(b.EstablishedSeconds)}; last error {Clean(b.LastError)}.")));
            rows.AddRange(routing.OspfNeighbours.Select(o => new Row($"OSPF {Clean(o.Address)}; router ID {Clean(o.RouterId)}; state {Clean(o.State)}; port {Clean(o.Port)}.")));
            rows.AddRange(routing.Vrfs.Select(v => new Row($"VRF {Clean(v.Name)}; route distinguisher {Clean(v.RouteDistinguisher)}; description {Clean(v.Description)}.")));
        }
        if (data.Availability is { } availability)
            rows.AddRange(availability.Select(a => new Row($"Availability over {Clean(a.Over)}: {a.Percent.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}%.")));
        if (data.Addresses is { } addresses)
            rows.AddRange(addresses.Select(a => new Row($"Address {Clean(a.Address)}; family {Clean(a.Family)}; port {Clean(a.Port)}.")));
        var heading = AlertUnavailable(data.Unavailable) + (found.SectionTotals.Count == 0 ? "No sections available."
            : "Section totals: " + string.Join(", ", found.SectionTotals.Select(s => Clean(s.Key) + " " + Id(s.Value))) + ".");
        var unfetched = pages?.Values.Sum(p => Math.Max(0, p.Total - p.Offset - p.Returned)) ?? 0;
        return Rows(heading, rows, alsoOmitted: unfetched);
    }

    private static string YesNo(bool? value) => value switch { true => "yes", false => "no", _ => "unknown" };

    private static string Names(IReadOnlyList<string?> names) => names.Count == 0 ? "none" : string.Join(", ", names.Select(Clean));
}
