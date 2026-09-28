using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class DevicesViewModelTests
{
    private readonly ILibreNmsClient _client = Fakes.Client(devices:
    [
        Fakes.Device(1, "core-sw", ip: "10.0.0.10", type: "network", location: "London", uptime: 86400 * 30),
        Fakes.Device(2, "access-sw", up: false, ip: "10.0.0.9", type: "network", location: "Leeds"),
        Fakes.Device(3, "backup-rtr", disabled: true, type: "network", location: "London"),
        Fakes.Device(4, "Branch-fw", ip: "10.9.0.1", type: "firewall", location: "London", uptime: 3600),
        Fakes.Device(5, "lab-server", ignore: true, ip: "192.168.1.5", type: "server", uptime: 60),
    ]);

    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly DeviceBookmarks _bookmarks;
    private readonly RecordingNavigation _navigation = new();

    public DevicesViewModelTests()
    {
        _settings = Fakes.Settings(_appSettings);
        _bookmarks = new DeviceBookmarks(_settings, _time);
        _client.DeviceGroups.GetMembershipByDeviceAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, IReadOnlyList<string>>
            {
                [1] = ["Core", "London kit"],
                [4] = ["London kit"],
            });
    }

    private DevicesViewModel NewViewModel() => new(_client, _navigation, _settings, _bookmarks, _time);

    private async Task<DevicesViewModel> Loaded()
    {
        var vm = NewViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;
        return vm;
    }

    private static string[] Names(DevicesViewModel vm) => vm.Devices.Select(d => d.Name).ToArray();

    [Fact]
    public async Task Everything_by_name_to_start_with()
    {
        var vm = await Loaded();

        Assert.Equal(["access-sw", "backup-rtr", "Branch-fw", "core-sw", "lab-server"], Names(vm));
        Assert.Equal("5 devices", vm.CountText);
        Assert.False(vm.HasActiveFilters);
    }

    [Fact]
    public async Task State_chips_count_and_filter_with_ignored_counted_as_disabled()
    {
        var vm = await Loaded();

        Assert.Equal((2, 1, 0, 2), (vm.UpCount, vm.DownCount, vm.MaintenanceCount, vm.DisabledCount));

        vm.ToggleUpCommand.Execute(null);
        vm.ToggleDisabledCommand.Execute(null);

        Assert.Equal(["access-sw"], Names(vm));
        Assert.Equal("1 of 5 devices", vm.CountText);
        Assert.True(vm.HasActiveFilters);
    }

    [Theory]
    [InlineData("10.9.", "Branch-fw")]     // IP
    [InlineData("FIREWALL", "Branch-fw")]  // type, any case
    [InlineData("leeds", "access-sw")]     // location
    [InlineData("5", "lab-server")]        // device id
    public async Task Search_looks_where_desktop_does(string term, string expected)
    {
        var vm = await Loaded();

        vm.SearchText = term;

        Assert.Equal([expected], Names(vm));
    }

    [Fact]
    public async Task Type_and_location_filters_list_what_is_there_with_counts()
    {
        var vm = await Loaded();

        Assert.Equal(["All types", "Network (3)", "Firewall (1)", "Server (1)"], vm.TypeOptions.Select(o => o.Label));
        Assert.Equal(["All locations", "London (3)", "Leeds (1)", "Unspecified (1)"], vm.LocationOptions.Select(o => o.Label));

        vm.SelectedType = vm.TypeOptions.Single(o => o.Key == "network");
        vm.SelectedLocation = vm.LocationOptions.Single(o => o.Key == "London");

        Assert.Equal(["backup-rtr", "core-sw"], Names(vm));
        Assert.True(vm.HasPanelFilters);
    }

    [Fact]
    public async Task Group_filter_includes_devices_in_no_group()
    {
        var vm = await Loaded();

        Assert.Equal(["All groups", "Not in a group (3)", "London kit (2)", "Core (1)"], vm.GroupOptions.Select(o => o.Label));

        vm.SelectedGroup = vm.GroupOptions.Single(o => o.Label.StartsWith("London kit", StringComparison.Ordinal));
        Assert.Equal(["Branch-fw", "core-sw"], Names(vm));

        vm.SelectedGroup = vm.GroupOptions.Single(o => o.Label.StartsWith("Not in", StringComparison.Ordinal));
        Assert.Equal(["access-sw", "backup-rtr", "lab-server"], Names(vm));
    }

    [Fact]
    public async Task Without_groups_the_group_filter_just_stays_at_all()
    {
        _client.DeviceGroups.GetMembershipByDeviceAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyDictionary<int, IReadOnlyList<string>>>(_ => throw new HttpRequestException("no"));

        var vm = await Loaded();

        Assert.Equal(["All groups"], vm.GroupOptions.Select(o => o.Label));
        Assert.Equal(5, vm.Devices.Count);
    }

    [Fact]
    public async Task A_chosen_filter_survives_a_refresh()
    {
        var vm = await Loaded();
        vm.SelectedType = vm.TypeOptions.Single(o => o.Key == "firewall");

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("firewall", vm.SelectedType.Key);
        Assert.Equal(["Branch-fw"], Names(vm));
    }

    [Fact]
    public async Task Clear_brings_everything_back()
    {
        var vm = await Loaded();
        vm.ShowDown = false;
        vm.SelectedLocation = vm.LocationOptions.Single(o => o.Key == "London");
        vm.SearchText = "core";

        vm.ClearFiltersCommand.Execute(null);

        Assert.Equal(5, vm.Devices.Count);
        Assert.False(vm.HasActiveFilters);
        Assert.Null(vm.SelectedLocation.Key);
    }

    [Theory]
    [InlineData(DeviceSort.Status, new[] { "access-sw", "Branch-fw", "core-sw", "backup-rtr", "lab-server" })]
    [InlineData(DeviceSort.IpAddress, new[] { "access-sw", "core-sw", "Branch-fw", "lab-server", "backup-rtr" })]
    // Only devices that are up have an uptime worth sorting by; down, disabled
    // and ignored ones (lab-server) go last, as desktop shows "—" for them.
    [InlineData(DeviceSort.Uptime, new[] { "Branch-fw", "core-sw", "access-sw", "backup-rtr", "lab-server" })]
    [InlineData(DeviceSort.Location, new[] { "access-sw", "backup-rtr", "Branch-fw", "core-sw", "lab-server" })]
    public async Task Sorts(DeviceSort sort, string[] expected)
    {
        var vm = await Loaded();

        vm.SelectedSort = vm.SortOptions.Single(o => o.Sort == sort);

        Assert.Equal(expected, Names(vm));
    }

    [Fact]
    public async Task Ip_sort_is_numeric_not_alphabetical()
    {
        var vm = await Loaded();
        vm.SelectedSort = vm.SortOptions.Single(o => o.Sort == DeviceSort.IpAddress);

        // 10.0.0.9 before 10.0.0.10, which plain text would put the other way round.
        Assert.Equal(["access-sw", "core-sw"], Names(vm).Take(2));
    }

    [Fact]
    public async Task Pinned_devices_stay_on_top_whatever_the_sort()
    {
        var vm = await Loaded();

        vm.TogglePinCommand.Execute(vm.Devices.Single(d => d.Name == "lab-server"));
        Assert.Equal("lab-server", Names(vm)[0]);
        Assert.True(vm.Devices[0].IsPinned);

        vm.SelectedSort = vm.SortOptions.Single(o => o.Sort == DeviceSort.Status);
        Assert.Equal("lab-server", Names(vm)[0]);

        Assert.Equal(5, Assert.Single(_appSettings.PinnedDevices).DeviceId);
        _settings.Received().Save();
    }

    [Fact]
    public async Task Groups_and_locations_open_the_list_filtered_to_them_even_before_it_loads()
    {
        var vm = NewViewModel();
        vm.SearchText = "something old";
        vm.ShowUp = false;

        // As arriving from Groups & locations: set, then the tab refreshes.
        vm.ShowOnly(group: "London kit");
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;

        Assert.Equal(["Branch-fw", "core-sw"], Names(vm));
        Assert.StartsWith("London kit (", vm.SelectedGroup.Label); // picked up its count
        Assert.True(vm.ShowUp);
        Assert.Equal(string.Empty, vm.SearchText);

        vm.ShowOnly(location: "Leeds");
        Assert.Equal(["access-sw"], Names(vm));
        Assert.Null(vm.SelectedGroup.Key);
    }

    [Fact]
    public async Task Unpinning_puts_it_back_in_order()
    {
        _appSettings.PinnedDevices.Add(new PinnedDevice { DeviceId = 5, DisplayName = "lab-server" });
        var vm = await Loaded();
        Assert.Equal("lab-server", Names(vm)[0]);

        vm.TogglePinCommand.Execute(vm.Devices[0]);

        Assert.Equal("lab-server", Names(vm)[^1]);
        Assert.Empty(_appSettings.PinnedDevices);
    }

    [Fact]
    public async Task Maintenance_is_looked_up_after_the_list_loads()
    {
        _client.Devices.IsUnderMaintenanceAsync(1, Arg.Any<CancellationToken>()).Returns(true);

        var vm = await Loaded();

        var core = vm.Devices.Single(d => d.DeviceId == 1);
        Assert.Equal(DeviceState.Maintenance, core.State);
        Assert.Equal((1, 1, 1), (vm.UpCount, vm.DownCount, vm.MaintenanceCount));

        vm.ShowMaintenance = false;
        Assert.DoesNotContain(core, vm.Devices);
    }

    [Fact]
    public async Task Maintenance_skips_disabled_devices_and_waits_a_minute_between_scans()
    {
        var vm = await Loaded();
        await _client.Devices.DidNotReceive().IsUnderMaintenanceAsync(3, Arg.Any<CancellationToken>());
        await _client.Devices.ReceivedWithAnyArgs(4).IsUnderMaintenanceAsync(default, default);

        _time.Advance(TimeSpan.FromSeconds(30));
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;
        await _client.Devices.ReceivedWithAnyArgs(4).IsUnderMaintenanceAsync(default, default);

        _time.Advance(TimeSpan.FromSeconds(31));
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;
        await _client.Devices.ReceivedWithAnyArgs(8).IsUnderMaintenanceAsync(default, default);
    }

    [Fact]
    public async Task One_device_failing_the_maintenance_check_does_not_spoil_the_rest()
    {
        _client.Devices.IsUnderMaintenanceAsync(2, Arg.Any<CancellationToken>()).Returns<bool>(_ => throw new HttpRequestException("timeout"));
        _client.Devices.IsUnderMaintenanceAsync(4, Arg.Any<CancellationToken>()).Returns(true);

        var vm = await Loaded();

        Assert.Equal(DeviceState.Maintenance, vm.Devices.Single(d => d.DeviceId == 4).State);
        Assert.Equal(DeviceState.Down, vm.Devices.Single(d => d.DeviceId == 2).State);
    }

    [Fact]
    public async Task Names_follow_the_device_name_setting()
    {
        _client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns([Fakes.Device(1, "10.0.0.1", sysName: "core-sw-01")]);
        _appSettings.DeviceNameStyle = DeviceNameStyle.SysName;

        var vm = await Loaded();

        var device = Assert.Single(vm.Devices);
        Assert.Equal("core-sw-01", device.Name);
        Assert.Equal("10.0.0.1", device.AlternateName);
    }

    [Fact]
    public async Task Recently_viewed_shows_while_browsing_but_not_while_searching()
    {
        _bookmarks.RecordViewed(4, "Branch-fw");
        var vm = await Loaded();

        Assert.Equal([4], vm.RecentlyViewed.Select(r => r.DeviceId));
        Assert.True(vm.ShowRecentlyViewed);

        vm.SearchText = "core";
        Assert.False(vm.ShowRecentlyViewed);

        await vm.OpenRecentCommand.ExecuteAsync(vm.RecentlyViewed[0]);
        Assert.Equal(4, _navigation.Visits.Single().Parameters![Routes.DeviceIdParameter]);
    }
}

public sealed class DeviceBookmarksTests
{
    private readonly AppSettings _appSettings = new();
    private readonly DeviceBookmarks _bookmarks;

    public DeviceBookmarksTests() => _bookmarks = new DeviceBookmarks(Fakes.Settings(_appSettings), TimeProvider.System);

    [Fact]
    public void Recently_viewed_is_newest_first_without_repeats_and_capped()
    {
        _appSettings.RecentlyViewedDeviceCount = 3;

        foreach (var id in new[] { 1, 2, 3, 1, 4 })
        {
            _bookmarks.RecordViewed(id, $"device {id}");
        }

        Assert.Equal([4, 1, 3], _bookmarks.RecentlyViewed.Select(r => r.DeviceId));
    }

    [Fact]
    public void Turning_pinning_off_in_settings_unpins_without_forgetting()
    {
        _bookmarks.SetPinned(7, "edge", pinned: true);

        _appSettings.EnablePinnedDevices = false;
        Assert.False(_bookmarks.IsPinned(7));
        Assert.Empty(_bookmarks.PinnedIds);

        _appSettings.EnablePinnedDevices = true;
        Assert.True(_bookmarks.IsPinned(7));
    }

    [Fact]
    public void Pinning_twice_keeps_one_pin()
    {
        _bookmarks.SetPinned(7, "edge", pinned: true);
        _bookmarks.SetPinned(7, "edge", pinned: true);

        Assert.Single(_appSettings.PinnedDevices);
    }
}

public sealed class DeviceDetailBookmarkTests
{
    [Fact]
    public async Task Opening_a_device_records_it_and_it_can_be_pinned_from_there()
    {
        var appSettings = new AppSettings();
        var settings = Fakes.Settings(appSettings);
        var bookmarks = new DeviceBookmarks(settings, TimeProvider.System);
        var client = Fakes.Client();
        client.Devices.GetAsync("7", Arg.Any<CancellationToken>()).Returns(Fakes.Device(7, "edge-rtr"));
        var vm = new DeviceDetailViewModel(client, settings, Substitute.For<ILauncherService>(), bookmarks, Substitute.For<IDialogService>(), new RecordingNavigation());

        await vm.LoadAsync(7);

        Assert.Equal(7, Assert.Single(appSettings.RecentlyViewedDevices).DeviceId);
        Assert.Equal("Pin", vm.PinText);

        vm.TogglePinCommand.Execute(null);

        Assert.True(vm.IsPinned);
        Assert.Equal("Unpin", vm.PinText);
        Assert.Equal("edge-rtr", Assert.Single(appSettings.PinnedDevices).DisplayName);
    }
}

public sealed class DeviceNameSettingTests
{
    [Fact]
    public void The_device_name_setting_is_saved()
    {
        var appSettings = new AppSettings();
        var settings = Fakes.Settings(appSettings);
        var session = Substitute.For<ISessionService>();
        var coordinator = new AlertWatchCoordinator(
            session, settings, null!, Substitute.For<IBackgroundAlertScheduler>(), new InMemoryWatchStore(), new NoAppBadge(), new DashyNMS.Mobile.Widgets.NoHomeWidgets(), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AlertWatchCoordinator>.Instance);
        var vm = new SettingsViewModel(session, settings, Substitute.For<IDialogService>(), new RecordingNavigation(), new RecordingNotifier(), coordinator, new NoAppBadge(), new InMemoryAppearance());

        Assert.Equal(["Hostname", "sysName", "LibreNMS display name"], vm.DeviceNameStyles);
        vm.DeviceNameStyleIndex = 1;

        Assert.Equal(DeviceNameStyle.SysName, appSettings.DeviceNameStyle);
        settings.Received(1).Save();
    }
}

public sealed class DeviceFilterNullTests
{
    [Fact]
    public async Task A_picker_clearing_its_choice_means_all_not_a_crash()
    {
        var settings = Fakes.Settings();
        var vm = new DevicesViewModel(
            Fakes.Client(devices: [Fakes.Device(1, "a", type: "network")]),
            new RecordingNavigation(),
            settings,
            new DeviceBookmarks(settings, TimeProvider.System),
            TimeProvider.System);
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.SelectedType = null!;
        vm.SelectedSort = null!;

        Assert.Null(vm.SelectedType.Key);
        Assert.Equal(DeviceSort.Name, vm.SelectedSort.Sort);
        Assert.Single(vm.Devices);
    }
}

public sealed class DashboardBookmarkTests
{
    [Fact]
    public async Task Pinned_devices_show_live_and_in_pin_order_with_recents()
    {
        var appSettings = new AppSettings();
        var settings = Fakes.Settings(appSettings);
        var bookmarks = new DeviceBookmarks(settings, TimeProvider.System);
        bookmarks.SetPinned(2, "old name", pinned: true);
        bookmarks.SetPinned(1, "core", pinned: true);
        bookmarks.SetPinned(9, "gone", pinned: true); // no longer in LibreNMS
        bookmarks.RecordViewed(1, "core");
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr", up: false)]);
        var navigation = new RecordingNavigation();
        var vm = new DashboardViewModel(client, settings, navigation, bookmarks);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["edge-rtr", "core-sw"], vm.PinnedDevices.Select(d => d.Name));
        Assert.Equal(DeviceState.Down, vm.PinnedDevices[0].State);
        Assert.Equal([1], vm.RecentlyViewed.Select(r => r.DeviceId));
        Assert.True(vm.HasPinnedDevices && vm.HasRecentlyViewed);

        await vm.OpenPinnedCommand.ExecuteAsync(vm.PinnedDevices[0]);
        Assert.Equal(2, navigation.Visits.Single().Parameters![Routes.DeviceIdParameter]);
    }
}
