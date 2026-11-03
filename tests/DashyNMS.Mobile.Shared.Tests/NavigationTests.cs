using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Tests;

/// <summary>The More tab and the tab bar's pins (#68).</summary>
public sealed class NavigationTests
{
    private readonly InMemoryPreferences _preferences = new();
    private readonly TabPins _pins;

    public NavigationTests()
    {
        _pins = new TabPins(_preferences);
    }

    [Fact]
    public void Devices_alerts_and_health_are_pinned_until_the_user_chooses()
    {
        Assert.Equal([AppPage.Devices, AppPage.Alerts, AppPage.Health], _pins.Pinned);
        Assert.False(_pins.CanPinMore);
    }

    [Fact]
    public void Pinning_is_limited_to_three_and_kept_in_page_order()
    {
        Assert.False(_pins.Toggle(AppPage.Map)); // full
        Assert.True(_pins.Toggle(AppPage.Alerts));
        Assert.True(_pins.Toggle(AppPage.Map));
        Assert.True(_pins.Toggle(AppPage.Settings) == false);

        Assert.Equal([AppPage.Devices, AppPage.Health, AppPage.Map], new TabPins(_preferences).Pinned);
    }

    [Fact]
    public void Unpinning_everything_is_remembered_rather_than_going_back_to_the_defaults()
    {
        _pins.Toggle(AppPage.Devices);
        _pins.Toggle(AppPage.Alerts);
        _pins.Toggle(AppPage.Health);

        Assert.Empty(new TabPins(_preferences).Pinned);
    }

    [Fact]
    public void Pages_that_no_longer_exist_are_skipped()
    {
        _preferences.Set(TabPins.Key, "Alerts,Unimus,Alerts,Map");

        Assert.Equal([AppPage.Alerts, AppPage.Map], _pins.Pinned);
    }

    [Fact]
    public void A_change_is_announced_for_the_tab_bar()
    {
        var raised = 0;
        _pins.Changed += (_, _) => raised++;

        _pins.Toggle(AppPage.Health);
        _pins.Toggle(AppPage.Map);
        _pins.Toggle(AppPage.Logs); // full: no change

        Assert.Equal(2, raised);
    }

    [Theory]
    [InlineData(Routes.Alerts, Routes.Alerts)]
    [InlineData(Routes.Devices, Routes.Devices)]
    [InlineData(Routes.Settings, "//main/more/settingspage")]
    [InlineData(Routes.AlertDetail, Routes.AlertDetail)]
    [InlineData(Routes.Main, Routes.Main)]
    public void Tab_routes_go_to_the_tab_when_pinned_and_through_More_when_not(string route, string expected)
    {
        Assert.Equal(expected, AppPages.Resolve(route, _pins));
    }

    [Fact]
    public void Nested_alert_and_back_routes_never_switch_tabs_when_pins_change()
    {
        foreach (var page in new[] { AppPage.Alerts, AppPage.Devices, AppPage.Health })
        {
            _pins.Toggle(page);
            foreach (var route in new[] { Routes.AlertDetail, Routes.AlertRule, Routes.AlertTemplate, Routes.Back })
                Assert.Equal(route, AppPages.Resolve(route, _pins));
        }
    }

    [Fact]
    public void An_unpinned_tab_keeps_its_query_when_pushed_onto_More()
    {
        _pins.Toggle(AppPage.Alerts);

        Assert.Equal("//main/more/alertspage?show=critical", AppPages.Resolve(Routes.Alerts + "?show=critical", _pins));
    }

    [Fact]
    public void More_lists_every_page_under_its_heading_with_its_pin()
    {
        var vm = new MoreViewModel(_pins, new RecordingNavigation(), Substitute.For<IDialogService>());

        Assert.Equal(["Monitor", "Network", "Logs", "App"], vm.Groups.Select(g => g.Name));
        Assert.Equal(AppPages.All.Count, vm.Groups.Sum(g => g.Items.Count));
        Assert.Equal("3 of 3 pinned", vm.PinnedText);

        var map = vm.Groups[1].Items[0];
        Assert.Equal(AppPage.Map, map.Page);
        Assert.False(map.IsPinned);
        Assert.False(map.CanToggle); // the bar is full
        Assert.True(vm.Groups[0].Items[0].CanToggle);
    }

    [Fact]
    public async Task Pinning_to_a_full_bar_says_why_nothing_happened()
    {
        var dialogs = Substitute.For<IDialogService>();
        var vm = new MoreViewModel(_pins, new RecordingNavigation(), dialogs);

        await vm.TogglePinCommand.ExecuteAsync(vm.Groups[1].Items[0]);

        await dialogs.Received(1).AlertAsync("The tab bar is full", Arg.Any<string>());
        Assert.False(_pins.IsPinned(AppPage.Map));
    }

    [Fact]
    public async Task Unpinning_makes_room_and_updates_the_count()
    {
        var vm = new MoreViewModel(_pins, new RecordingNavigation(), Substitute.For<IDialogService>());
        var health = vm.Groups[0].Items.Single(i => i.Page == AppPage.Health);

        await vm.TogglePinCommand.ExecuteAsync(health);

        Assert.False(health.IsPinned);
        Assert.Equal("2 of 3 pinned", vm.PinnedText);
        Assert.True(vm.Groups[1].Items[0].CanToggle);
        Assert.Equal("Pin Health to the tab bar", health.PinDescription);
    }

    [Fact]
    public async Task Opening_a_page_goes_to_its_tab_or_pushes_it()
    {
        var navigation = new RecordingNavigation();
        var vm = new MoreViewModel(_pins, navigation, Substitute.For<IDialogService>());

        await vm.OpenCommand.ExecuteAsync(vm.Groups[0].Items[1]); // Alerts, pinned
        await vm.OpenCommand.ExecuteAsync(vm.Groups[1].Items[0]); // Map

        Assert.Equal([Routes.Alerts, Routes.Map], navigation.Visits.Select(v => v.Route));
    }

    [Fact]
    public async Task Pin_feedback_and_open_action_follow_capacity_and_survive_relaunch()
    {
        var navigation = new RecordingNavigation();
        var vm = new MoreViewModel(_pins, navigation, Substitute.For<IDialogService>());
        var items = vm.Groups.SelectMany(group => group.Items).ToList();
        var map = items.Single(item => item.Page == AppPage.Map);
        var health = items.Single(item => item.Page == AppPage.Health);
        Assert.Contains("Unpin another page", map.PinState);
        Assert.Contains("Back returns to More", map.OpenHint);
        var changes = new List<string?>();
        map.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        await vm.TogglePinCommand.ExecuteAsync(health);
        Assert.Equal("Not pinned", map.PinState);
        Assert.Contains(nameof(MoreItem.PinState), changes);
        await vm.TogglePinCommand.ExecuteAsync(map);
        Assert.Equal("Pinned to the tab bar", map.PinState);
        Assert.Equal("Unpin", map.PinLabel);
        Assert.Contains("Switches", map.OpenHint);
        Assert.Contains(nameof(MoreItem.OpenDescription), changes);
        Assert.Contains(nameof(MoreItem.OpenHint), changes);
        await vm.OpenCommand.ExecuteAsync(map);
        Assert.Equal(Routes.Main + "/" + AppPages.TabRoute(AppPage.Map), navigation.Visits.Single().Route);

        var restored = new MoreViewModel(new TabPins(_preferences), navigation, Substitute.For<IDialogService>());
        Assert.True(restored.Groups.SelectMany(group => group.Items).Single(item => item.Page == AppPage.Map).IsPinned);
        Assert.False(restored.Groups.SelectMany(group => group.Items).Single(item => item.Page == AppPage.Health).IsPinned);
        Assert.All(items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Description));
            Assert.Contains(item.Title, item.OpenDescription);
            Assert.Contains(item.PinState, item.OpenDescription);
        });
    }
}
