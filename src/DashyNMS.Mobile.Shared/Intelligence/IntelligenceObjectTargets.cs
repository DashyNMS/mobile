using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Map;
using DashyNMS.Mobile.Topology;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Intelligence;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>One tested mapping for every Ask Intelligence entry; display text never supplies an identity.</summary>
public static class IntelligenceObjectTargets
{
    public static IntelligenceObjectReference? From(object? value)
    {
        var target = value switch
        {
            Device d => new("device", d.DeviceId),
            DeviceItem d => From(d.Device),
            RecentDeviceRow d => new("device", d.DeviceId),
            DeviceDetailViewModel d => From(d.Device),
            Alert a => new(a.Source == AlertSource.Fault ? "fault" : "alert", a.Id, a.DeviceId, a.RuleId),
            AlertItem a => From(a.Alert),
            AlertDetailViewModel a => From(a.Alert),
            Sensor s => new("sensor", s.SensorId, s.DeviceId),
            Port p => new("port", p.PortId, p.DeviceId),
            DeviceGroup g => new("group", g.Id),
            Location l => new("location", l.Id),
            TopRow { PortId: > 0 } p => new("port", p.PortId.Value, p.DeviceId),
            TopRow d => new("device", d.DeviceId),
            SectionRow { IntelligenceObject: { } identity } => identity,
            SectionRow { LinkDeviceId: > 0 } d => new("device", d.LinkDeviceId.Value),
            LogEntryItem { DeviceId: > 0 } l => new("device", l.DeviceId),
            GraylogMessageItem { DeviceId: > 0 } l => new("device", l.DeviceId.Value),
            NeighbourLink l => new("device", l.LocalDeviceId),
            MapPinData p => p.IntelligenceObject,
            MapLocationChoice p => p.Pin.IntelligenceObject,
            // Map pin IDs are map-local indices, not LibreNMS location IDs.
            DashboardCard { Widget.GraphDeviceId: > 0 } c => new("device", c.Widget.GraphDeviceId.Value),
            IntelligenceObjectReference r => r,
            _ => (IntelligenceObjectReference?)null,
        };
        return target is { IsValid: true } ? target : null;
    }
}
