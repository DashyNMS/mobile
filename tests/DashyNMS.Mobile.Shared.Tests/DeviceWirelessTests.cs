using DashyNMS.Mobile.DeviceSections;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class DeviceWirelessTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Unavailable_current_cannot_be_healthy(double? current)
    {
        var row = DeviceSectionLoader.WirelessRow(new() { Current = current, LimitHigh = 100 });
        Assert.Equal(RowStatus.None, row.Status);
        Assert.Equal("Reading unavailable", row.SensorStatusText);
        Assert.DoesNotContain("OK", row.SensorDescription);
    }

    [Theory]
    [InlineData(0, RowStatus.Critical)]
    [InlineData(10, RowStatus.Warning)]
    [InlineData(50, RowStatus.Ok)]
    [InlineData(90, RowStatus.Warning)]
    [InlineData(100, RowStatus.Critical)]
    public void Finite_source_limits_keep_inclusive_priority(double current, RowStatus expected)
    {
        var row = DeviceSectionLoader.WirelessRow(new()
        {
            Current = current, LimitLow = 0, LimitLowWarn = 10, LimitHighWarn = 90, LimitHigh = 100,
        });
        Assert.Equal(expected, row.Status);
        Assert.True(row.Matches(row.SensorStatusText!));
        Assert.Contains(row.SensorStatusText!, row.SensorDescription);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Any_nonfinite_bound_prevents_false_OK(int index)
    {
        var sensor = new WirelessSensor { Current = 42, LimitHigh = 100 };
        switch (index)
        {
            case 0: sensor.LimitLow = double.NaN; break;
            case 1: sensor.LimitLowWarn = double.NegativeInfinity; break;
            case 2: sensor.LimitHighWarn = double.PositiveInfinity; break;
            case 3: sensor.LimitHigh = double.NaN; break;
        }
        var row = DeviceSectionLoader.WirelessRow(sensor);
        Assert.Equal(RowStatus.None, row.Status);
        Assert.Equal("Limits unavailable", row.SensorStatusText);
    }

    [Theory]
    [InlineData("clients", 42, "42")]
    [InlineData("channel", 11, "11")]
    [InlineData("rssi", -70, "-70 dBm")]
    [InlineData("rate", 300000000, "300 Mbps")]
    [InlineData("unknown-class", 7, "7")]
    public void Unassessed_metrics_keep_units_and_context_without_OK(string kind, double current, string value)
    {
        var name = new string('x', 300);
        var row = DeviceSectionLoader.WirelessRow(new() { Description = name, SensorClass = kind, Current = current });
        Assert.Equal(name, row.Title);
        Assert.Equal(value, row.Value);
        Assert.Equal(RowStatus.None, row.Status);
        Assert.Equal("No limits", row.SensorStatusText);
        Assert.True(row.SensorLayout);
        Assert.Equal(WirelessSensorClasses.NameOf(kind), row.Subtitle);
        Assert.Contains(name, row.SensorDescription);
        Assert.True(row.Matches(row.Subtitle!));
    }

    [Fact]
    public async Task Loader_preserves_supported_classes_and_overview_health_without_deleted_rows()
    {
        var client = Fakes.Client();
        client.Devices.GetWirelessSensorsAsync(7, Arg.Any<CancellationToken>()).Returns(new WirelessSensor[]
        {
            new() { SensorId = 1, SensorClass = "channel", Current = 11 },
            new() { SensorId = 2, SensorClass = "clients", Current = 42 },
            new() { SensorId = 3, SensorClass = "rssi", Current = -90, LimitLow = -80 },
            new() { SensorId = 4, SensorClass = "other-metric", Current = 8 },
            new() { SensorId = 5, SensorClass = "clients", Current = 99, Deleted = true },
        });
        var groups = await new DeviceSectionLoader(client, Fakes.Settings()).LoadAsync(DeviceSection.Wireless, 7);
        Assert.Equal(4, groups.Sum(g => g.Count));
        var card = new DeviceSectionCard(DeviceSectionInfo.For(DeviceSection.Wireless));
        card.Show(groups);
        Assert.Equal(4, card.Count);
        Assert.Equal(3, card.Highlights.Count);
        Assert.Equal("Sensor 3", card.Highlights[0].Title);
        Assert.Equal(RowStatus.Critical, card.Status);
        Assert.All(card.Highlights, row => Assert.True(row.SensorLayout));
        Assert.All(groups.SelectMany(g => g).Where(r => r.Title != "Sensor 3"), row => Assert.Equal(RowStatus.None, row.Status));
    }
}
