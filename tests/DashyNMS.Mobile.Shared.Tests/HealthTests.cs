using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class HealthViewModelTests
{
    private readonly ILibreNmsClient _client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")]);
    private readonly RecordingNavigation _navigation = new();

    public HealthViewModelTests()
    {
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 1, DeviceId = 2, SensorClass = "temperature", Description = "Inlet", Current = 30, LimitHigh = 70 },
            new Sensor { SensorId = 2, DeviceId = 1, SensorClass = "temperature", Description = "CPU", Current = 90, LimitHigh = 70 },
            new Sensor { SensorId = 3, DeviceId = 1, SensorClass = "temperature", Description = "Board", Current = 40, LimitHigh = 70 },
            new Sensor { SensorId = 4, DeviceId = 1, SensorClass = "dbm", Description = "Gi0/1 Rx", Current = -3 },
            new Sensor { SensorId = 5, DeviceId = 1, SensorClass = "voltage", Description = "PSU", Current = 12 }, // not a Health category
        ]);
    }

    private async Task<HealthViewModel> Loaded(string category)
    {
        var vm = new HealthViewModel(_client, Fakes.Settings(), _navigation);
        vm.SelectCategoryCommand.Execute(vm.Categories.Single(c => c.SensorClass == category));
        await vm.RefreshIfStaleAsync();
        return vm;
    }

    private CorePolling Polling()
    {
        var session = Substitute.For<DesktopNMS.Services.ISessionService>();
        session.IsConnected.Returns(true);
        return new CorePolling(_client, session, Fakes.Settings());
    }

    private static Sensor Inlet(double current) =>
        new() { SensorId = 1, DeviceId = 2, SensorClass = "temperature", Description = "Inlet", Current = current, LimitHigh = 70 };

    [Fact]
    public async Task A_slow_sensor_list_keeps_the_earlier_readings_on_screen_until_the_new_ones_arrive()
    {
        using var polling = Polling();
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns([Inlet(30)]);
        await polling.SensorsAsync();
        var slow = new TaskCompletionSource<IReadOnlyList<Sensor>>();
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(slow.Task);
        var vm = new HealthViewModel(_client, Fakes.Settings(), _navigation, polling: polling);
        vm.SelectCategoryCommand.Execute(vm.Categories.Single(c => c.SensorClass == "temperature"));

        var refresh = vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("30 °C", Assert.Single(vm.Sensors).Value);
        Assert.Equal("Updating…", vm.CountText);
        Assert.False(vm.IsLoadingFirstTime);

        slow.SetResult([Inlet(31)]);
        await refresh;

        Assert.Equal("31 °C", Assert.Single(vm.Sensors).Value);
        Assert.Equal("1 temperature sensor", vm.CountText);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task A_sensor_list_timeout_shows_the_kept_readings_and_their_time()
    {
        using var polling = Polling();
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns([Inlet(30)]);
        var good = await polling.SensorsAsync();
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Sensor>>(_ => throw new LibreNmsApiException("Timed out") { IsTimeout = true });
        var vm = new HealthViewModel(_client, Fakes.Settings(), _navigation, polling: polling);
        vm.SelectCategoryCommand.Execute(vm.Categories.Single(c => c.SensorClass == "temperature"));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.IsBusy);
        Assert.Equal("30 °C", Assert.Single(vm.Sensors).Value);
        Assert.Equal(
            $"LibreNMS took too long to send the sensor list - showing readings from {good.ObservedAt!.Value.LocalDateTime:HH:mm}.",
            vm.ErrorMessage);
    }

    [Fact]
    public async Task A_sensor_list_timeout_with_nothing_kept_says_to_try_again()
    {
        using var polling = Polling();
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Sensor>>(_ => throw new LibreNmsApiException("Timed out") { IsTimeout = true });
        var vm = new HealthViewModel(_client, Fakes.Settings(), _navigation, polling: polling);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.IsBusy);
        Assert.Empty(vm.Sensors);
        Assert.Equal("LibreNMS took too long to send the sensor list. Try refreshing again.", vm.ErrorMessage);
    }

    [Fact]
    public async Task Unavailable_readings_are_not_counted_as_healthy()
    {
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [new Sensor { DeviceId = 1, SensorClass = "temperature", Description = "Missing", Current = null }]);
        var vm = await Loaded("temperature");
        Assert.Equal((0, 0, 0), (vm.CriticalCount, vm.WarningCount, vm.OkCount));
        Assert.Equal("–", Assert.Single(vm.Sensors).Value);
    }

    [Fact]
    public void Unavailable_description_omits_placeholder()
    {
        var row = new SectionRow("Synthetic inlet") { Value = "–", Status = RowStatus.None };
        Assert.Equal("Synthetic inlet, Unavailable", row.HealthDescription);
    }

    [Theory]
    [InlineData(RowStatus.Critical, "Critical")]
    [InlineData(RowStatus.Warning, "Warning")]
    [InlineData(RowStatus.Ok, "OK")]
    [InlineData(RowStatus.None, "Unavailable")]
    [InlineData(RowStatus.Inactive, "Inactive")]
    public void Row_description_preserves_reading_limits_and_textual_severity(RowStatus status, string text)
    {
        var row = new SectionRow("Synthetic inlet")
        {
            Subtitle = "example.net", Value = "30 °C", Detail = "High 70 °C", Status = status,
        };
        Assert.Equal(text, row.HealthStatusText);
        Assert.Equal($"Synthetic inlet, example.net, 30 °C, High 70 °C, {text}", row.HealthDescription);
    }

    [Fact]
    public async Task Unknown_stays_visible_when_all_severities_are_excluded_and_counts_stay_category_wide()
    {
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [Inlet(30), new Sensor { DeviceId = 1, SensorClass = "temperature", Description = "Missing", Current = null }]);
        var vm = await Loaded("temperature");
        vm.ShowCritical = vm.ShowWarning = vm.ShowOk = false;
        Assert.Equal("Unavailable", Assert.Single(vm.Sensors).HealthStatusText);
        Assert.Equal((0, 0, 1), (vm.CriticalCount, vm.WarningCount, vm.OkCount));
        Assert.Equal("OK 1, excluded", vm.OkFilterDescription);
        vm.ClearFiltersCommand.Execute(null);
        Assert.Equal(2, vm.Sensors.Count);
        Assert.Equal("OK 1, included", vm.OkFilterDescription);
    }

    [Fact]
    public async Task Returning_after_threshold_save_rebuilds_descriptions_and_counts_without_a_fetch()
    {
        var settings = Fakes.Settings();
        var vm = new HealthViewModel(_client, settings, _navigation);
        vm.SelectCategoryCommand.Execute(vm.Categories.Single(c => c.SensorClass == "temperature"));
        await vm.RefreshIfStaleAsync();
        settings.Current.OverrideSensorLimitsWithAppThresholds = true;
        settings.Current.TemperatureThresholds.HighCritical = 25;
        settings.Current.TemperatureThresholds.HighWarning = 20;
        settings.Changed += Raise.Event<EventHandler<DesktopNMS.Core.Configuration.AppSettings>>(settings, settings.Current);
        await vm.RefreshIfStaleAsync();
        Assert.Equal(3, vm.CriticalCount);
        Assert.All(vm.Sensors, row => Assert.Contains("Critical", row.HealthDescription));
        await _client.Sensors.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Has_desktops_four_categories_in_its_order()
    {
        var vm = new HealthViewModel(_client, Fakes.Settings(), _navigation);

        Assert.Equal(["dBm", "Signal", "Temperature", "Fan speed"], vm.Categories.Select(c => c.Label));
        Assert.True(vm.Categories[0].IsSelected);
    }

    [Fact]
    public async Task Problems_first_then_by_device_with_the_device_beneath_each_sensor()
    {
        var vm = await Loaded("temperature");

        Assert.Equal(["CPU", "Board", "Inlet"], vm.Sensors.Select(s => s.Title));
        Assert.Equal(RowStatus.Critical, vm.Sensors[0].Status);
        Assert.Equal("core-sw", vm.Sensors[0].Subtitle);
        Assert.Equal((1, 0, 2), (vm.CriticalCount, vm.WarningCount, vm.OkCount));
        Assert.Equal("3 temperature sensors", vm.CountText);
        Assert.True(vm.Categories.Single(c => c.SensorClass == "temperature").IsSelected);
        Assert.False(vm.Categories[0].IsSelected);
    }

    [Fact]
    public async Task Severity_chips_and_search_narrow_the_list()
    {
        var vm = await Loaded("temperature");

        vm.ToggleOkCommand.Execute(null);
        Assert.Equal(["CPU"], vm.Sensors.Select(s => s.Title));
        Assert.Equal("1 of 3 temperature sensors", vm.CountText);

        vm.ClearFiltersCommand.Execute(null);
        vm.SearchText = "edge";
        Assert.Equal(["Inlet"], vm.Sensors.Select(s => s.Title));
        Assert.True(vm.HasActiveFilters);
    }

    [Fact]
    public void Severity_chips_are_remembered()
    {
        var preferences = new InMemoryPreferences();
        var vm = new HealthViewModel(_client, Fakes.Settings(), _navigation, preferences);

        vm.ToggleOkCommand.Execute(null);

        var next = new HealthViewModel(_client, Fakes.Settings(), _navigation, preferences);
        Assert.False(next.ShowOk); // #81
        Assert.True(next.ShowCritical);
    }

    [Fact]
    public async Task Tapping_a_sensor_opens_its_devices_sensors()
    {
        var vm = await Loaded("temperature");

        await vm.OpenSensorCommand.ExecuteAsync(vm.Sensors[0]);

        var visit = Assert.Single(_navigation.Visits);
        Assert.Equal(Routes.DeviceSection, visit.Route);
        Assert.Equal(1, visit.Parameters![Routes.DeviceIdParameter]);
        Assert.Equal(DeviceSection.Sensors, visit.Parameters[Routes.SectionParameter]);
    }

    [Fact]
    public async Task Coming_back_soon_after_doesnt_fetch_every_sensor_again()
    {
        var vm = await Loaded("temperature");

        await vm.RefreshIfStaleAsync();

        await _client.Sensors.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }
}
