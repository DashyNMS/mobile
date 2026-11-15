using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class IntelligenceThresholdTests
{
    private static CompactNetworkLookups Lookups(AppSettings settings, Sensor sensor)
    {
        var client = Fakes.Client([Fakes.Device(1, "switch.example.net")]);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { sensor });
        var session = Fakes.Session();
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token"));
        return new(client, Fakes.Settings(settings), session, deviceTransport: Fakes.DeviceTransport(client));
    }

    private static string Label(AlertSeverity severity) => severity == AlertSeverity.Ok ? "OK" : severity.ToString().ToLowerInvariant();

    [Theory]
    [InlineData("temperature", 48)]
    [InlineData("fanspeed", 750)]
    [InlineData("dbm", -13)]
    [InlineData("signal", -75)]
    public async Task The_next_question_uses_changed_settings_without_changing_an_existing_attempt(string type, double value)
    {
        var settings = new AppSettings();
        var sensor = new Sensor { SensorId = 1, DeviceId = 1, SensorClass = type, Description = "Example reading", Current = value };
        var defaultSeverity = SensorCategoryRegistry.Resolve(type)!.Thresholds(settings, sensor).Evaluate(value);
        var provider = Lookups(settings, sensor);
        using var first = provider.Open(_ => { });
        settings.TemperatureThresholds.HighWarning = 30;
        settings.TemperatureThresholds.HighCritical = 40;
        settings.FanSpeedThresholds.LowWarning = 1200;
        settings.FanSpeedThresholds.LowCritical = 1000;
        settings.DbmThresholds.WarningThreshold = -12;
        settings.DbmThresholds.CriticalThreshold = -12.5;
        settings.SignalThresholds.WarningThreshold = -70;
        settings.SignalThresholds.CriticalThreshold = -72;
        var text = await first.Lookups.Single(l => l.Name == "device_sensors").Run("1", default);
        Assert.Contains("; " + Label(defaultSeverity), text);
        var evidence = new List<IntelligenceEvidence>();
        using var next = provider.Open(evidence.AddRange);
        var changedSeverity = SensorCategoryRegistry.Resolve(type)!.Thresholds(settings, sensor).Evaluate(value);
        text = await next.Lookups.Single(l => l.Name == "device_sensors").Run("1", default);
        Assert.Equal(AlertSeverity.Critical, changedSeverity);
        Assert.Contains("; " + Label(changedSeverity), text);
        Assert.Equal("critical", Assert.Single(evidence, e => e.Reference.Key == "sensor:1").Reference.Tone);
    }

    [Theory]
    [InlineData(false, AlertSeverity.Ok)]
    [InlineData(true, AlertSeverity.Critical)]
    public async Task Sensor_override_matches_the_Health_page_evaluator(bool overrideLimits, AlertSeverity expected)
    {
        var settings = new AppSettings { OverrideSensorLimitsWithAppThresholds = overrideLimits,
            TemperatureThresholds = new() { LowCritical = -10, LowWarning = 0, HighWarning = 30, HighCritical = 40 } };
        var sensor = new Sensor { SensorId = 1, DeviceId = 1, SensorClass = "temperature", Description = "Example reading", Current = 48,
            LimitHighWarn = 60, LimitHigh = 80 };
        var core = SensorCategoryRegistry.Resolve("temperature")!.Thresholds(settings, sensor).Evaluate(sensor.Current);
        Assert.Equal(expected, core);
        using var set = Lookups(settings, sensor).Open(_ => { });
        Assert.Contains("; " + Label(core), await set.Lookups.Single(l => l.Name == "device_sensors").Run("1", default));
    }
}
