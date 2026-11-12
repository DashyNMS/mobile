using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Sensors;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Tests;

/// <summary>State sensors named and coloured from the sensor definitions (#210), mirroring desktop's SensorDefinitionDisplayTests.</summary>
public sealed class StateSensorDisplayTests
{
    private static Sensor FanState(SensorStateResolution? state) => new()
    {
        SensorId = 7, DeviceId = 12, SensorClass = "state", Description = "Fan 1", Current = 2, LimitHigh = 3,
        StateDefinition = state,
    };

    [Theory]
    [InlineData(AlertSeverity.Critical, RowStatus.Critical, "Critical")]
    [InlineData(AlertSeverity.Warning, RowStatus.Warning, "Warning")]
    [InlineData(AlertSeverity.Ok, RowStatus.Ok, "OK")]
    [InlineData(AlertSeverity.Unknown, RowStatus.None, "Unknown")]
    public void State_rows_without_numeric_limits_keep_definition_health(
        AlertSeverity severity, RowStatus expectedStatus, string expectedText)
    {
        var sensor = FanState(new("Synthetic state", severity, severity != AlertSeverity.Unknown, "example/fan", "1.0.0"));
        sensor.LimitHigh = null;
        var row = DeviceSectionLoader.SensorRow(sensor, new AppSettings());

        Assert.Equal(expectedStatus, row.Status);
        Assert.Equal(expectedText, row.SensorStatusText);
        Assert.Equal("Synthetic state", row.Value);
        Assert.Equal("Raw value: 2", row.Subtitle);
        Assert.Contains(expectedText, row.SensorDescription);
        Assert.True(row.Matches(expectedText));
    }

    [Fact]
    public void Unresolved_state_without_numeric_limits_remains_unknown()
    {
        var sensor = FanState(SensorStateResolution.Unresolved);
        sensor.LimitHigh = null;
        var row = DeviceSectionLoader.SensorRow(sensor, new AppSettings());
        Assert.Equal(RowStatus.None, row.Status);
        Assert.Equal("Unknown", row.SensorStatusText);
        Assert.Equal("2", row.Value);
    }

    [Fact]
    public void A_defined_state_shows_its_name_and_colour_with_the_number_beneath()
    {
        var reading = DeviceSectionLoader.ReadSensor(FanState(new("Ready", AlertSeverity.Ok, true, "example/fan", "1.0.0")), new AppSettings());

        Assert.Equal(("Ready", RowStatus.Ok, "Raw value: 2"), (reading.Value, reading.Status, reading.Limits));
    }

    [Fact]
    public void Without_definitions_the_reading_goes_back_to_its_number()
    {
        var reading = DeviceSectionLoader.ReadSensor(FanState(null), new AppSettings());

        Assert.Equal("2", reading.Value);
    }

    [Fact]
    public void Unresolved_or_label_only_states_never_show_OK()
    {
        // Under its own high limit, which would otherwise read as OK.
        var unresolved = DeviceSectionLoader.ReadSensor(FanState(SensorStateResolution.Unresolved), new AppSettings());
        Assert.Equal(("2", RowStatus.None), (unresolved.Value, unresolved.Status));

        var labelOnly = DeviceSectionLoader.ReadSensor(FanState(new("Secondary", AlertSeverity.Unknown, false, "example/role", "1.0.0")), new AppSettings());
        Assert.Equal(("Secondary", RowStatus.None), (labelOnly.Value, labelOnly.Status));
    }

    [Fact]
    public void A_critical_definition_colours_the_row_critical()
    {
        var reading = DeviceSectionLoader.ReadSensor(FanState(new("Fan failed", AlertSeverity.Critical, true, "example/fan", "1.0.0")), new AppSettings());

        Assert.Equal(("Fan failed", RowStatus.Critical), (reading.Value, reading.Status));
    }
}

/// <summary>The definitions reaching the screens through the shared sensor list (#210).</summary>
public sealed class StateSensorDefinitionsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"dashynms-{Guid.NewGuid():N}");
    private readonly FakeGitHub _github = new();
    private readonly SensorDefinitions _definitions;
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly ILibreNmsClient _client;
    private readonly CorePolling _polling;

    public StateSensorDefinitionsTests()
    {
        Directory.CreateDirectory(_folder);
        _definitions = new SensorDefinitions(_folder, new HttpClient(_github));
        _settings = Fakes.Settings(_appSettings);
        var device = Fakes.Device(12, "core-sw");
        device.Os = "example-os";
        _client = Fakes.Client([device]);
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        [
            new Sensor { SensorId = 7, DeviceId = 12, SensorClass = "state", SensorType = "exampleFanState", Oid = ".1.3.6.1.4.1.32473.1.7", Description = "Fan 1", Current = 2 },
        ]);
        var session = Fakes.Session();
        session.IsConnected.Returns(true);
        _polling = new CorePolling(_client, session, _settings, definitions: _definitions);
    }

    public void Dispose()
    {
        _polling.Dispose();
        _definitions.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private async Task<DashboardCard> SensorsCardAsync()
    {
        var widget = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        widget.Sensors.Add(new PinnedSensor { SensorId = 7, DeviceId = 12 });
        var vm = new DashboardViewModel(_client, _settings, new RecordingNavigation(), new DeviceBookmarks(_settings, TimeProvider.System), polling: _polling);
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.SensorsLoaded;
        return vm.Cards.Single(c => c.Type == DashboardLayout.Sensors);
    }

    [Fact]
    public async Task A_fan_state_with_a_definition_shows_its_name_and_colour_on_the_dashboard_and_in_Device_View()
    {
        await _definitions.StartAsync();

        var row = Assert.Single((await SensorsCardAsync()).Sensors);
        Assert.Equal(("Ready", RowStatus.Ok), (row.Value, row.Status));

        var groups = await new DeviceSectionLoader(_client, _settings, _polling).LoadAsync(DeviceSection.Sensors, 12);
        var section = Assert.Single(Assert.Single(groups));
        Assert.Equal(("Ready", RowStatus.Ok, "Raw value: 2"), (section.Value, section.Status, section.Subtitle));
    }

    [Fact]
    public async Task Turning_definitions_off_goes_back_to_raw_readings()
    {
        await _definitions.StartAsync();
        Assert.Equal("Ready", Assert.Single((await SensorsCardAsync()).Sensors).Value);
        var version = _polling.DefinitionsVersion;

        await _definitions.Store.ConfigureAsync(new SensorDefinitionOptions { Enabled = false });

        Assert.NotEqual(version, _polling.DefinitionsVersion);
        _appSettings.DashboardWidgets.Clear();
        Assert.Equal("2", Assert.Single((await SensorsCardAsync()).Sensors).Value);
    }

    [Fact]
    public async Task Nothing_is_fetched_for_definitions_while_the_app_is_in_the_background()
    {
        await _definitions.StartAsync();
        _github.Requests.Clear();
        _polling.SetForeground(false);

        var sensors = await _polling.SensorsAsync();

        Assert.Empty(_github.Requests);
        Assert.Null(Assert.Single(sensors.Sensors).StateDefinition);
    }
}
