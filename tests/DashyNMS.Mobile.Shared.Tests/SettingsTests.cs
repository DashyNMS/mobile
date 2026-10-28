using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

public sealed class MoreSettingsTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly InMemoryAppearance _appearance = new();
    private readonly AlertCountThreshold _threshold;
    private readonly RecordingNavigation _navigation = new();
    private readonly SettingsViewModel _vm;

    public MoreSettingsTests()
    {
        _settings = Fakes.Settings(_appSettings);
        _threshold = new AlertCountThreshold(_settings, new InMemoryPreferences());
        var session = Substitute.For<ISessionService>();
        var store = new InMemoryWatchStore();
        var watcher = new AlertWatcher(
            Fakes.Client(), session, Fakes.Secrets(), _settings, store, new SelfActionTracker(), new RecordingNotifier(),
            new NoAppBadge(), new DashyNMS.Mobile.Widgets.NoHomeWidgets(), TimeProvider.System, NullLogger<AlertWatcher>.Instance);
        var coordinator = new AlertWatchCoordinator(
            session, _settings, watcher, Substitute.For<IBackgroundAlertScheduler>(), store, new NoAppBadge(),
            new DashyNMS.Mobile.Widgets.NoHomeWidgets(), TimeProvider.System, NullLogger<AlertWatchCoordinator>.Instance);
        _vm = new SettingsViewModel(session, _settings, Substitute.For<IDialogService>(), _navigation, new RecordingNotifier(), coordinator, new NoAppBadge(), _appearance, new DashyNMS.Mobile.Widgets.NoHomeWidgets(), countThreshold: _threshold);
    }

    [Fact]
    public void Each_section_row_summarises_its_values()
    {
        _appSettings.Notifications.Enabled = true;
        _appSettings.Notifications.Critical.Enabled = true;
        _appSettings.Notifications.Warning.Enabled = false;
        _appSettings.Notifications.QuietHoursEnabled = true;
        _appSettings.Notifications.QuietHoursStartHour = 22;
        _appSettings.Notifications.QuietHoursEndHour = 7;

        Assert.Equal("Same as the phone · jiggle physics", _vm.AppearanceSummary); // jiggle on by default since Core 802980c
        Assert.Equal("Every 1 minute while open", _vm.AlertChecksSummary); // no badge on this platform
        Assert.Equal("Critical only · quiet 22:00–07:00", _vm.NotificationsSummary);
        Assert.EndsWith("recently viewed", _vm.DevicesSummary);
        Assert.Equal("Not signed in", _vm.ServerHost);

        _appSettings.Notifications.Enabled = false;
        Assert.Equal("Off", _vm.NotificationsSummary);
    }

    [Fact]
    public async Task A_section_row_opens_its_page()
    {
        await _vm.OpenSectionCommand.ExecuteAsync(SettingsSection.Notifications);

        var visit = Assert.Single(_navigation.Visits);
        Assert.Equal(Routes.SettingsSection, visit.Route);
        Assert.Equal(SettingsSection.Notifications, visit.Parameters![Routes.SettingsSectionParameter]);
        Assert.Equal("Notifications", SettingsViewModel.Title(SettingsSection.Notifications));
    }

    [Fact]
    public void Poll_interval_is_desktops_setting_from_30_seconds_up()
    {
        Assert.Equal("1 minute", _vm.PollIntervalLabels[_vm.PollIntervalIndex]); // desktop's default, 60 s
        Assert.Equal("30 seconds", _vm.PollIntervalLabels[0]);

        _vm.PollIntervalIndex = 3;

        Assert.Equal(300, _appSettings.PollIntervalSeconds);
        _settings.Received(1).Save();
    }

    [Fact]
    public void A_poll_interval_desktop_allows_but_the_list_doesnt_shows_as_the_next_choice_up()
    {
        _appSettings.PollIntervalSeconds = 45;

        Assert.Equal("1 minute", _vm.PollIntervalLabels[_vm.PollIntervalIndex]);
    }

    [Fact]
    public void The_badge_threshold_is_chosen_from_three_and_kept_on_the_phone()
    {
        Assert.Equal(["Every alert", "Critical and warning", "Critical only"], _vm.BadgeThresholdLabels); // Core's words, as desktop's (#168)
        Assert.Equal(0, _vm.BadgeThresholdIndex);

        _vm.BadgeThresholdIndex = 2;

        Assert.Equal(AlertSeverity.Critical, _threshold.Minimum);
        Assert.Equal(2, _vm.BadgeThresholdIndex);

        _vm.BadgeThresholdIndex = 7; // out of range: ignored
        Assert.Equal(AlertSeverity.Critical, _threshold.Minimum);
    }

    [Fact]
    public void Appearance_follows_the_phone_until_chosen()
    {
        Assert.Equal(0, _vm.AppearanceIndex);
        var desktopTheme = _appSettings.Theme;

        _vm.AppearanceIndex = 2;

        Assert.Equal(AppearanceChoice.Dark, _appearance.Current);
        Assert.Equal(desktopTheme, _appSettings.Theme); // desktop's own setting untouched
        _vm.AppearanceIndex = 1;
        Assert.Equal(AppearanceChoice.Light, _appearance.Current);
        Assert.Equal(desktopTheme, _appSettings.Theme);
    }

    [Fact]
    public async Task Health_thresholds_open_from_settings()
    {
        await _vm.OpenThresholdsCommand.ExecuteAsync(null);

        Assert.Equal(Routes.Thresholds, Assert.Single(_navigation.Visits).Route);
    }
}

public sealed class ThresholdsViewModelTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly RecordingNavigation _navigation = new();

    public ThresholdsViewModelTests() => _settings = Fakes.Settings(_appSettings);

    [Fact]
    public void Starts_from_the_current_settings()
    {
        var vm = new ThresholdsViewModel(_settings, _navigation);

        Assert.Equal("-12.5", vm.DbmWarning.Replace(',', '.'));
        Assert.Equal("60", vm.TemperatureHighWarning);
        Assert.Equal("25000", vm.FanHighCritical);
    }

    [Fact]
    public async Task Saves_every_band_and_goes_back()
    {
        var vm = new ThresholdsViewModel(_settings, _navigation)
        {
            TemperatureHighWarning = "55",
            TemperatureHighCritical = "70",
            DbmCritical = "-16",
            OverrideSensorLimits = true,
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.Equal((55, 70), (_appSettings.TemperatureThresholds.HighWarning, _appSettings.TemperatureThresholds.HighCritical));
        Assert.Equal(-16, _appSettings.DbmThresholds.CriticalThreshold);
        Assert.True(_appSettings.OverrideSensorLimitsWithAppThresholds);
        _settings.Received(1).Save();
        Assert.Equal(Routes.Back, Assert.Single(_navigation.Visits).Route);
    }

    [Theory]
    [InlineData(nameof(ThresholdsViewModel.FanHighWarning), "fast", "must be a number")]
    [InlineData(nameof(ThresholdsViewModel.DbmCritical), "-10", "dBm critical must be at or below")]
    [InlineData(nameof(ThresholdsViewModel.TemperatureLowWarning), "80", "Temperature thresholds must go")]
    public async Task Refuses_a_band_that_isnt_a_number_or_is_out_of_order(string field, string value, string error)
    {
        var vm = new ThresholdsViewModel(_settings, _navigation);
        typeof(ThresholdsViewModel).GetProperty(field)!.SetValue(vm, value);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Contains(error, vm.ErrorMessage);
        _settings.DidNotReceive().Save();
        Assert.Empty(_navigation.Visits);
    }

    [Fact]
    public void Reset_puts_desktops_defaults_in_the_fields_without_saving()
    {
        _appSettings.TemperatureThresholds.HighCritical = 99;
        var vm = new ThresholdsViewModel(_settings, _navigation);

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Equal("75", vm.TemperatureHighCritical);
        Assert.Equal(99, _appSettings.TemperatureThresholds.HighCritical);
        _settings.DidNotReceive().Save();
    }
}

public sealed class HealthRecolourTests
{
    [Fact]
    public async Task Changing_the_thresholds_recolours_the_health_tab_without_refetching()
    {
        var settings = new AppSettings();
        var store = Fakes.Settings(settings);
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw")]);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>())
            .Returns([new Sensor { SensorId = 1, DeviceId = 1, SensorClass = "temperature", Description = "CPU", Current = 65 }]);
        var vm = new HealthViewModel(client, store, new RecordingNavigation());
        vm.SelectCategoryCommand.Execute(vm.Categories.Single(c => c.SensorClass == "temperature"));
        await vm.RefreshIfStaleAsync();
        Assert.Equal(RowStatus.Warning, vm.Sensors.Single().Status); // 65 is past desktop's 60 °C warning

        settings.TemperatureThresholds.HighWarning = 70;
        store.Changed += Raise.Event<EventHandler<AppSettings>>(store, settings);

        Assert.Equal(RowStatus.Ok, vm.Sensors.Single().Status);
        await client.Sensors.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }
}
