using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Widgets;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Sensors;
using NSubstitute;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// A custom reading with no registered class, no state definition and no limits has
/// nothing to be judged against (#532). Every surface that reads a sensor through
/// <see cref="DeviceSectionLoader.ReadSensor"/> - Device Sensors, the Dashboard card,
/// the picker and widget snapshots - keeps it neutral rather than OK, while readings
/// that can be judged keep their evaluation. All readings are synthetic.
/// </summary>
public sealed class UnassessedSensorTests
{
    private static Sensor Custom(int id, double? current, double? low = null, double? lowWarn = null, double? highWarn = null, double? high = null) => new()
    {
        SensorId = id, DeviceId = 1, SensorClass = "synthetic_flow", Description = $"Custom {id}", Unit = "widgets",
        Current = current, LimitLow = low, LimitLowWarn = lowWarn, LimitHighWarn = highWarn, LimitHigh = high,
    };

    [Fact]
    public void A_reading_with_nothing_to_judge_it_is_unassessed_and_neutral()
    {
        var reading = DeviceSectionLoader.ReadSensor(Custom(1, 12.34), new AppSettings());

        Assert.False(reading.Assessed);
        Assert.Equal(RowStatus.None, reading.Status);
        Assert.Equal("12.3 widgets", reading.Value);
        Assert.Null(reading.Limits);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Non_finite_limits_built_in_code_count_as_no_limits(double limit)
    {
        var reading = DeviceSectionLoader.ReadSensor(Custom(1, 12.34, low: limit, lowWarn: limit, highWarn: limit, high: limit), new AppSettings());

        Assert.False(reading.Assessed);
        Assert.Equal(RowStatus.None, reading.Status);
    }

    /// <summary>Core's JSON reader drops non-finite limits before mobile sees them.</summary>
    [Theory]
    [InlineData("\"nan\"")]
    [InlineData("\"Infinity\"")]
    [InlineData("\"-Infinity\"")]
    public void Non_finite_limits_from_the_api_load_as_no_limits(string limit)
    {
        var sensor = System.Text.Json.JsonSerializer.Deserialize<Sensor>(
            $$"""{"sensor_id":1,"sensor_class":"synthetic_flow","sensor_current":5,"sensor_limit":{{limit}},"sensor_limit_low":{{limit}}}""")!;

        Assert.Null(sensor.LimitHigh);
        Assert.Null(sensor.LimitLow);
        Assert.False(DeviceSectionLoader.ReadSensor(sensor, new AppSettings()).Assessed);
    }

    [Theory]
    [InlineData(5, RowStatus.Ok)]
    [InlineData(15, RowStatus.Warning)]
    public void Warning_only_limits_still_judge_the_reading(double current, RowStatus expected)
    {
        var reading = DeviceSectionLoader.ReadSensor(Custom(1, current, highWarn: 10), new AppSettings());

        Assert.True(reading.Assessed);
        Assert.Equal(expected, reading.Status);
    }

    [Fact]
    public void A_state_definition_judges_the_reading_without_limits()
    {
        var sensor = Custom(1, 2);
        sensor.StateDefinition = new SensorStateResolution("Fan failed", AlertSeverity.Critical, true, "example/fan", "1.0.0");

        var reading = DeviceSectionLoader.ReadSensor(sensor, new AppSettings());

        Assert.True(reading.Assessed);
        Assert.Equal(RowStatus.Critical, reading.Status);
        Assert.Equal("Fan failed", reading.Value);
    }

    [Fact]
    public void A_registered_class_keeps_its_settings_thresholds_without_sensor_limits()
    {
        var reading = DeviceSectionLoader.ReadSensor(
            new Sensor { SensorId = 1, SensorClass = "temperature", Current = 20 }, new AppSettings());

        Assert.True(reading.Assessed);
        Assert.Equal(RowStatus.Ok, reading.Status);
    }

    [Fact]
    public void Device_sensors_say_no_limits_and_stay_uncoloured()
    {
        var row = DeviceSectionLoader.SensorRow(Custom(1, 12.34), new AppSettings());

        Assert.Equal("No limits", row.SensorStatusText);
        Assert.Equal(RowStatus.None, row.Status);
    }

    [Fact]
    public void Widget_snapshots_export_unassessed_readings_as_unknown_never_ok()
    {
        var settings = new AppSettings();
        var card = DashboardLayout.Add(settings, DashboardLayout.Sensors);
        card.Sensors.Add(new PinnedSensor { SensorId = 1, DeviceId = 1 });
        card.Sensors.Add(new PinnedSensor { SensorId = 2, DeviceId = 1 });
        card.Sensors.Add(new PinnedSensor { SensorId = 3, DeviceId = 1 });

        var snapshot = WidgetSnapshot.Build([], DateTimeOffset.UnixEpoch, settings, [Fakes.Device(1, "core")],
            [Custom(1, 12.34), Custom(2, 15, highWarn: 10), Custom(3, null)]);

        Assert.Equal(["unknown", "warning", "unknown"], snapshot.Sensors.Select(s => s.Status));
        Assert.Null(snapshot.Sensors[0].Position);
    }

    [Fact]
    public async Task The_sensor_picker_shows_unassessed_readings_uncoloured()
    {
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw")]);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns([Custom(1, 12.34), Custom(2, 15, highWarn: 10)]);
        var vm = new SensorPickerViewModel(client, Fakes.Settings(new AppSettings()), new RecordingNavigation());
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SearchText = "Custom";

        Assert.Equal([RowStatus.None, RowStatus.Warning], vm.Sensors.OrderBy(s => s.Row.Title).Select(s => s.Row.Status));
    }
}
