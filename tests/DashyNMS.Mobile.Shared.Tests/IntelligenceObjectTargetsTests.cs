using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Map;
using DesktopNMS.Core.Intelligence;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class IntelligenceObjectTargetsTests
{
    [Fact]
    public void Faults_alerts_sensors_and_ports_keep_their_own_identity()
    {
        Assert.Equal(new("fault", 12, 3, 4), IntelligenceObjectTargets.From(new Alert { Id = 12, DeviceId = 3, RuleId = 4, Source = AlertSource.Fault }));
        Assert.Equal(new("alert", 12, 3, 4), IntelligenceObjectTargets.From(new Alert { Id = 12, DeviceId = 3, RuleId = 4 }));
        Assert.Equal(new("sensor", 12, 3), IntelligenceObjectTargets.From(new Sensor { SensorId = 12, DeviceId = 3 }));
        Assert.Equal(new("port", 12, 3), IntelligenceObjectTargets.From(new Port { PortId = 12, DeviceId = 3 }));
    }
    [Fact]
    public void Rankings_and_sensor_rows_keep_the_object_instead_of_just_the_device()
    {
        Assert.Equal(new("port", 12, 3), IntelligenceObjectTargets.From(new TopRow(3, 12, "Port", null, "", "", 0)));
        Assert.Equal(new("device", 3), IntelligenceObjectTargets.From(new TopRow(3, null, "Device", null, "", "", 0)));
        var sensor = new IntelligenceObjectReference("sensor", 12, 3);
        Assert.Equal(sensor, IntelligenceObjectTargets.From(new SectionRow("Sensor") { IntelligenceObject = sensor, LinkDeviceId = 3 }));
    }
    [Fact]
    public void Map_indices_never_become_location_ids()
    {
        var pin = new MapPinData(12, "Example", 0, 0, 1, "up");
        Assert.Null(IntelligenceObjectTargets.From(pin));
        Assert.Equal(new("location", 99), IntelligenceObjectTargets.From(pin with { IntelligenceObject = new("location", 99) }));
    }
    [Fact]
    public void Device_group_location_and_neighbour_fallbacks_are_exact()
    {
        Assert.Equal(new("device", 3), IntelligenceObjectTargets.From(new Device { DeviceId = 3 }));
        Assert.Equal(new("group", 4), IntelligenceObjectTargets.From(new DeviceGroup { Id = 4 }));
        Assert.Equal(new("location", 5), IntelligenceObjectTargets.From(new Location { Id = 5 }));
        Assert.Equal(new("device", 3), IntelligenceObjectTargets.From(new SectionRow("Neighbour") { LinkDeviceId = 3 }));
        Assert.Null(IntelligenceObjectTargets.From(new Sensor { SensorId = 12 }));
        Assert.Null(IntelligenceObjectTargets.From(new object()));
    }
}
