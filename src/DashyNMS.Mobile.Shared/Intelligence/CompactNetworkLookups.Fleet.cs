using DashyNMS.Agent;
using DashyNMS.Agent.Health;
using DashyNMS.Agent.Rankings;
using DashyNMS.Agent.Summaries;

namespace DashyNMS.Mobile.Intelligence;

public sealed partial class CompactNetworkLookups
{
    private static Reply FleetFailure(HostFailure? failure) => Reply.Say(failure switch
    {
        HostFailure.Refused => Refused,
        HostFailure.InvalidRequest => "The scope or sensor query is invalid. Use an exact group/location; listing all readings also needs a type.",
        _ => Failed,
    });

    private static Row FleetDevice(DeviceBrief? device, Func<DeviceBrief, string> name, string detail, DateTimeOffset at)
    {
        if (device is null) return new("Device unknown; " + detail);
        var text = $"{name(device)}; hostname {Clean(device.Hostname)}; display {Clean(device.Display)}; IP {Clean(device.Ip)}; location {Clean(device.Location)}; state {Clean(device.Status)}; {detail}";
        return TopologyDevice(device.DeviceId, name(device), text, at);
    }

    private static Reply TopPortRows(TopPortsResult found, DateTimeOffset at, Func<DeviceBrief, string> name)
    {
        return Rows(found.Summary + $" Candidates {Id(found.CandidatesTotal)}; ports checked {Id(found.PortsChecked)}.", found.Data.Ports.Select(p =>
            FleetDevice(p.Device, name, $"port {Id(p.PortId)} {Clean(p.Port)}; alias {Clean(p.Alias)}; speed {Number(p.SpeedMbps)} Mbps"
                + (p.InMbps is null && p.OutMbps is null ? "" : $"; in {Number(p.InMbps)} Mbps; out {Number(p.OutMbps)} Mbps")
                + (p.InErrorsPerSec is null && p.OutErrorsPerSec is null ? "" : $"; errors in {Number(p.InErrorsPerSec)}/s; out {Number(p.OutErrorsPerSec)}/s")
                + (p.BusiestDirectionPercent is { } percent ? "; busiest direction " + Number(percent) + "%" : ""), at)),
            alsoOmitted: Math.Max(0, found.CandidatesTotal - found.Data.Ports.Count));
    }

    private static Reply TopDeviceRows(TopDevicesResult found, DateTimeOffset at, Func<DeviceBrief, string> name)
    {
        return Rows(found.Summary + $" Candidates {Id(found.CandidatesTotal)}; ports checked {Id(found.PortsChecked)}.", found.Data.Devices.Select(d =>
            FleetDevice(d.Device, name, d.ActivePorts is { } active
                ? $"in {Number(d.InMbps)} Mbps; out {Number(d.OutMbps)} Mbps; active ports {Id(active)}."
                : $"ports down while enabled {Count(d.PortsDownWhileEnabled)}; ports counting errors {Count(d.PortsCountingErrors)}.", at)),
            alsoOmitted: Math.Max(0, found.CandidatesTotal - found.Data.Devices.Count));
    }

    private static Reply HealthOverviewRows(HealthOverviewResult found, DateTimeOffset at, Func<DeviceBrief, string> name)
    {
        var page = found.Data.Page;
        var totals = found.Totals;
        var heading = (found.DeniedDevices > 0 ? $"Unavailable sensors on {Count(found.DeniedDevices, "device")}; excluded from counts. " : "")
            + $"Sensors checked {Id(totals.Readings)}; critical {Id(totals.Critical)}; warning {Id(totals.Warning)}; OK {Id(totals.Ok)}; unknown {Id(totals.Unknown)}. "
            + $"Matches {Id(page.Total)}; offset {Id(page.Offset)}; limit {Id(page.Limit)}; returned {Id(page.Returned)}."
            + (page.NextOffset is { } next ? $" Next offset {Id(next)}." : "");
        var readings = found.Data.Readings.Select(r =>
        {
            var sensor = SensorRow(r.Reading, r.Device.DeviceId, name(r.Device), at);
            var device = FleetDevice(r.Device, name, sensor.Text, at);
            return device with { References = device.References.Concat(sensor.References).ToList() };
        });
        var types = found.Data.Types.Select(t => new Row($"Type {Clean(t.Type)}: {Id(t.Count)} readings; {Id(t.Critical)} critical; {Id(t.Warning)} warning."));
        return Rows(heading, readings.Concat(types), alsoOmitted: Math.Max(0, page.Total - page.Offset - page.Returned));
    }
}
