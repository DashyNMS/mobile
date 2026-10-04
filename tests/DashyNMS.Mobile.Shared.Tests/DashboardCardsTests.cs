using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class DashboardLayoutTests
{
    [Fact]
    public void An_empty_list_is_an_empty_dashboard()
    {
        Assert.Empty(DashboardLayout.Current(new AppSettings()));
    }

    [Fact]
    public void Desktops_list_in_order_graphs_and_sensors_as_often_as_they_appear_others_once()
    {
        var settings = new AppSettings
        {
            DashboardWidgets =
            [
                new DashboardWidget { WidgetType = "Graph" },
                new DashboardWidget { WidgetType = "SomethingNew" },
                new DashboardWidget { WidgetType = "Alerts" },
                new DashboardWidget { WidgetType = "Graph" },
                new DashboardWidget { WidgetType = "Alerts" },
            ],
        };

        Assert.Equal(["Graph", "Alerts", "Graph"], DashboardLayout.Current(settings).Select(w => w.WidgetType));
    }

    [Fact]
    public void Saving_an_order_keeps_each_cards_own_set_up()
    {
        var settings = new AppSettings();
        var alerts = DashboardLayout.Add(settings, DashboardLayout.Alerts);
        var graph = DashboardLayout.Add(settings, DashboardLayout.Graph);
        graph.GraphDeviceId = 7;

        DashboardLayout.Save(settings, [graph, alerts]);

        Assert.Equal(["Graph", "Alerts"], settings.DashboardWidgets.Select(w => w.WidgetType));
        Assert.Equal(7, settings.DashboardWidgets[0].GraphDeviceId);
    }

    [Fact]
    public void Saving_on_the_phone_keeps_desktops_widgets_it_doesnt_show()
    {
        var unknown = new DashboardWidget { WidgetType = "SomethingNew", Title = "From desktop" };
        var secondAlerts = new DashboardWidget { WidgetType = "Alerts", Title = "Critical only" };
        var alerts = new DashboardWidget { WidgetType = "Alerts" };
        var graph = new DashboardWidget { WidgetType = "Graph" };
        var settings = new AppSettings { DashboardWidgets = [alerts, unknown, graph, secondAlerts] };

        DashboardLayout.Save(settings, [graph]); // Alerts removed on the phone

        Assert.Equal([graph, unknown, secondAlerts], settings.DashboardWidgets);
    }

    [Fact]
    public void Graphs_and_sensors_can_be_added_again_and_again_others_only_once()
    {
        var settings = new AppSettings();

        var first = DashboardLayout.Add(settings, DashboardLayout.Graph);
        var second = DashboardLayout.Add(settings, DashboardLayout.Graph);
        var wireless = DashboardLayout.Add(settings, DashboardLayout.Wireless);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Same(wireless, DashboardLayout.Add(settings, DashboardLayout.Wireless));
        Assert.Equal(2, DashboardLayout.CountOf(settings, DashboardLayout.Graph));
        Assert.Same(second, DashboardLayout.Find(settings, second.Id));
    }

    [Fact]
    public void The_starter_is_desktops_with_its_places_on_desktops_grid_and_the_phones_names()
    {
        var unknown = new DashboardWidget { WidgetType = "EventLog" };
        var settings = new AppSettings { DashboardWidgets = [unknown] };

        DashboardLayout.UseStarter(settings);

        var starter = StarterDashboard.Create();
        Assert.Equal(starter.Select(w => w.WidgetType), DashboardLayout.Current(settings).Select(w => w.WidgetType));
        Assert.Equal(starter.Select(w => (w.Column, w.Row, w.ColumnSpan, w.RowSpan)),
            DashboardLayout.Current(settings).Select(w => (w.Column, w.Row, w.ColumnSpan, w.RowSpan)));
        Assert.Contains(unknown, settings.DashboardWidgets); // desktop's log widget kept

        // Desktop's titles ("Alerts gauge") aren't titles of its own: the phone's names show.
        Assert.Equal(["Alerts", "Devices", "Needs attention", "Top interfaces", "Recently viewed"],
            DashboardLayout.Current(settings).Select(DashboardLayout.TitleOf));
        Assert.Equal("Alerts, Devices, Needs attention, Top interfaces and Recently viewed", DashboardLayout.StarterContents);
    }

    [Fact]
    public void A_new_card_is_named_as_desktop_names_it()
    {
        Assert.Equal("Alerts gauge", DashboardLayout.New(DashboardLayout.AlertsGauge).Title);
        Assert.Equal("Wireless", DashboardLayout.New(DashboardLayout.Wireless).Title);
        Assert.False(DashboardLayout.HasOwnTitle(DashboardLayout.New(DashboardLayout.AlertsGauge)));
    }

    [Fact]
    public void An_install_in_use_keeps_the_dashboard_it_had_a_new_one_starts_empty()
    {
        var fresh = new AppSettings();
        Assert.False(DashboardLayout.KeepPreviousDefaults(fresh));
        Assert.Empty(fresh.DashboardWidgets);

        var used = new AppSettings { RecentlyViewedDevices = [new RecentlyViewedDevice { DeviceId = 1 }] };
        Assert.True(DashboardLayout.KeepPreviousDefaults(used));
        Assert.Equal(DashboardLayout.PreviousDefaultTypes, DashboardLayout.Current(used).Select(w => w.WidgetType));

        // Cards already chosen are never touched.
        Assert.False(DashboardLayout.KeepPreviousDefaults(used));
    }

    [Fact]
    public void Card_kinds_are_filed_in_desktops_categories_without_logs()
    {
        Assert.All(DashboardLayout.Kinds, k => Assert.Contains(k.Category, DashboardLayout.Categories));
        Assert.DoesNotContain(DashboardLayout.Kinds, k => k.Type is DashboardWidgetTypes.EventLog or DashboardWidgetTypes.Graylog);
    }
}

public sealed class CustomiseDashboardViewModelTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly DashboardToast _toast;

    public CustomiseDashboardViewModelTests()
    {
        _settings = Fakes.Settings(_appSettings);
        _toast = new DashboardToast(_settings) { ShowFor = TimeSpan.Zero, HighlightFor = TimeSpan.Zero };
    }

    private static string[] Shown(AppSettings settings) => DashboardLayout.Current(settings).Select(w => w.WidgetType).ToArray();

    private CustomiseDashboardViewModel NewViewModel(RecordingNavigation? navigation = null)
    {
        var vm = new CustomiseDashboardViewModel(_settings, navigation ?? new RecordingNavigation(), _toast);
        vm.Attach();
        return vm;
    }

    [Fact]
    public void Lists_the_cards_on_the_dashboard_only()
    {
        DashboardLayout.UseStarter(_appSettings);

        var vm = NewViewModel();

        Assert.Equal(Shown(_appSettings), vm.Cards.Select(c => c.Kind.Type));
        Assert.Equal("Alerts", vm.Cards[0].Title);
        Assert.Equal("Top 3, ranked by in and out", vm.Cards.Single(c => c.Kind.Type == DashboardLayout.TopInterfaces).Subtitle);
    }

    [Fact]
    public void Any_card_is_removed_at_once_and_Undo_puts_it_back()
    {
        DashboardLayout.UseStarter(_appSettings);
        var vm = NewViewModel();
        var devices = vm.Cards.Single(c => c.Kind.Type == DashboardLayout.DeviceStatus);

        vm.RemoveCommand.Execute(devices);

        Assert.DoesNotContain(DashboardLayout.DeviceStatus, Shown(_appSettings));
        Assert.Equal("Devices removed", _toast.Message);
        _settings.Received().Save();

        _toast.UndoCommand.Execute(null);

        Assert.Equal(DashboardLayout.DeviceStatus, Shown(_appSettings)[1]);
        Assert.Contains(vm.Cards, c => c.Kind.Type == DashboardLayout.DeviceStatus); // the page read them again
        Assert.False(_toast.IsShowing);
    }

    [Fact]
    public void Removing_every_card_leaves_an_empty_dashboard()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.Wireless);
        var vm = NewViewModel();

        vm.RemoveCommand.Execute(vm.Cards[0]);

        Assert.True(vm.IsEmpty);
        Assert.Empty(Shown(_appSettings));
    }

    [Fact]
    public void A_new_order_is_saved()
    {
        DashboardLayout.UseStarter(_appSettings);
        var vm = NewViewModel();

        vm.Cards.Move(4, 0);
        vm.SaveOrderCommand.Execute(null);

        Assert.Equal(DashboardLayout.RecentlyViewed, Shown(_appSettings)[0]);
    }

    [Fact]
    public async Task Sensors_graph_and_top_cards_have_a_set_up_others_dont()
    {
        var sensors = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        DashboardLayout.Add(_appSettings, DashboardLayout.Alerts);
        var navigation = new RecordingNavigation();
        var vm = NewViewModel(navigation);

        Assert.True(vm.Cards[0].CanSetUp);
        Assert.False(vm.Cards[1].CanSetUp);

        await vm.SetUpCommand.ExecuteAsync(vm.Cards[0]);
        await vm.SetUpCommand.ExecuteAsync(vm.Cards[1]);

        var visit = Assert.Single(navigation.Visits);
        Assert.Equal(Routes.PickSensors, visit.Route);
        Assert.Equal(sensors.Id, visit.Parameters![Routes.WidgetIdParameter]);
    }

    [Fact]
    public void A_card_with_its_own_title_is_listed_by_it()
    {
        var card = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        card.Title = "Core switch temps";

        var vm = NewViewModel();

        Assert.Equal("Core switch temps", vm.Cards.Single().Title);
        Assert.Equal("Sensors · none chosen yet", vm.Cards.Single().Subtitle);
    }

    [Fact]
    public async Task Add_card_opens_the_card_picker()
    {
        var navigation = new RecordingNavigation();
        var vm = NewViewModel(navigation);

        await vm.AddCardCommand.ExecuteAsync(null);

        Assert.Equal(Routes.AddCard, Assert.Single(navigation.Visits).Route);
    }
}

public sealed class AddCardViewModelTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly DashboardToast _toast;
    private readonly RecordingNavigation _navigation = new();

    public AddCardViewModelTests()
    {
        _settings = Fakes.Settings(_appSettings);
        _toast = new DashboardToast(_settings) { ShowFor = TimeSpan.Zero, HighlightFor = TimeSpan.Zero };
    }

    private AddCardViewModel NewViewModel() =>
        new(_settings, _navigation, _toast, new DeviceBookmarks(_settings, TimeProvider.System));

    [Fact]
    public void Every_card_filed_by_category_with_counts()
    {
        var vm = NewViewModel();

        Assert.Equal(["All 11", "Alerts 2", "Devices 4", "Traffic 3", "Sensors and graphs 2"], vm.Categories.Select(c => c.Label));
        Assert.Equal(11, vm.Cards.Count);
        Assert.Same(vm.Cards[0], vm.Selected); // the first that can be added, as desktop's

        vm.ChooseCategoryCommand.Execute(vm.Categories.Single(c => c.Name == "Traffic"));
        Assert.Equal(["Top interfaces", "Top errors", "Top devices"], vm.Cards.Select(c => c.Name));
        Assert.True(vm.Categories.Single(c => c.Name == "Traffic").IsSelected);

        vm.SearchText = "gauge"; // desktop's name finds the phone's card
        Assert.Empty(vm.Cards);
        vm.ChooseCategoryCommand.Execute(vm.Categories[0]);
        Assert.Equal(["Alerts"], vm.Cards.Select(c => c.Name));
    }

    [Fact]
    public void A_card_on_the_dashboard_is_greyed_out_unless_there_can_be_several()
    {
        DashboardLayout.UseStarter(_appSettings);
        var vm = NewViewModel();

        var alerts = vm.Cards.Single(c => c.Kind.Type == DashboardLayout.AlertsGauge);
        Assert.False(alerts.IsAvailable);
        Assert.Equal("On your dashboard", alerts.Subtitle);

        vm.PickCommand.Execute(alerts);
        Assert.NotSame(alerts, vm.Selected);

        var top = vm.Cards.Single(c => c.Kind.Type == DashboardLayout.TopInterfaces);
        Assert.True(top.IsAvailable && top.HasSome);
        Assert.Equal("1 on your dashboard · add another", top.Subtitle);
    }

    [Fact]
    public void Pinned_devices_needs_pinning_on()
    {
        _appSettings.EnablePinnedDevices = false;

        var pinned = NewViewModel().Cards.Single(c => c.Kind.Type == DashboardLayout.PinnedDevices);

        Assert.Equal("Turn on pinned devices in Settings, Devices first", pinned.UnavailableReason);
    }

    [Fact]
    public async Task Adding_puts_it_at_the_bottom_says_so_outlines_it_and_goes_back()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.Alerts);
        var vm = NewViewModel();
        vm.PickCommand.Execute(vm.Cards.Single(c => c.Kind.Type == DashboardLayout.TopErrors));
        Assert.Equal("Add Top errors", vm.AddText);

        await vm.AddCommand.ExecuteAsync(null);

        var added = DashboardLayout.Current(_appSettings)[^1];
        Assert.Equal(DashboardLayout.TopErrors, added.WidgetType);
        Assert.Equal("Top errors added at the bottom", _toast.Message);
        Assert.Equal(added.Id, _toast.HighlightId);
        Assert.Equal([Routes.Back], _navigation.Visits.Select(v => v.Route)); // a Top card shows at once

        _toast.UndoCommand.Execute(null);
        Assert.Equal([DashboardLayout.Alerts], DashboardLayout.Current(_appSettings).Select(w => w.WidgetType));
    }

    [Fact]
    public async Task A_sensors_or_graph_card_goes_on_to_its_set_up()
    {
        var vm = NewViewModel();
        vm.PickCommand.Execute(vm.Cards.Single(c => c.Kind.Type == DashboardLayout.Graph));

        await vm.AddCommand.ExecuteAsync(null);

        Assert.Equal([Routes.Back, Routes.PickGraph], _navigation.Visits.Select(v => v.Route));
        Assert.Equal(DashboardLayout.Current(_appSettings)[0].Id, _navigation.Visits[1].Parameters![Routes.WidgetIdParameter]);
    }
}

public sealed class DashboardWelcomeTests
{
    private readonly AppSettings _appSettings = new() { ServerUrl = "https://nms.example.net/" };
    private readonly ISettingsStore _settings;
    private readonly RecordingNavigation _navigation = new();
    private readonly InMemoryPreferences _preferences = new();

    public DashboardWelcomeTests() => _settings = Fakes.Settings(_appSettings);

    private DashboardViewModel NewDashboard(DashboardWelcome? welcome = null)
    {
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")], alerts: [Fakes.Alert(1, 1, "critical")]);
        var toast = new DashboardToast(_settings) { ShowFor = TimeSpan.Zero, HighlightFor = TimeSpan.Zero };
        return new DashboardViewModel(client, _settings, _navigation, new DeviceBookmarks(_settings, TimeProvider.System),
            toast: toast, welcome: welcome, preferences: _preferences);
    }

    [Fact]
    public async Task An_empty_dashboard_welcomes_with_the_server_and_its_counts()
    {
        var vm = NewDashboard();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(vm.Cards);
        Assert.True(vm.ShowWelcome);
        Assert.False(vm.ShowEmpty);
        Assert.Equal("Connected to nms.example.net · 2 devices · 1 active alert", vm.Welcome.ServerSummary);
        Assert.Equal("Starter: Alerts, Devices, Needs attention, Top interfaces and Recently viewed", vm.Welcome.StarterText);
    }

    [Fact]
    public async Task The_starter_dashboard_fills_it_with_Undo()
    {
        var vm = NewDashboard();

        await vm.UseStarterCommand.ExecuteAsync(null);

        Assert.Equal(StarterDashboard.Create().Select(w => w.WidgetType), vm.Cards.Select(c => c.Type));
        Assert.False(vm.ShowWelcome);
        Assert.Equal("Starter dashboard added", vm.Toast.Message);

        vm.Toast.UndoCommand.Execute(null);
        Assert.Empty(DashboardLayout.Current(_appSettings));
    }

    [Fact]
    public async Task Choose_cards_opens_the_card_picker_and_dont_show_again_is_desktops_setting()
    {
        var vm = NewDashboard();

        await vm.ChooseCardsCommand.ExecuteAsync(null);
        vm.DismissWelcomeCommand.Execute(null);

        Assert.Equal(Routes.AddCard, Assert.Single(_navigation.Visits).Route);
        Assert.True(_appSettings.WelcomeDismissed);
        Assert.False(vm.ShowWelcome);
        Assert.True(vm.ShowEmpty);
    }

    [Fact]
    public async Task The_checklist_ticks_itself_off()
    {
        var notifier = Substitute.For<IAlertNotifier>();
        notifier.RequestPermissionAsync().Returns(true);
        var widgets = Substitute.For<DashyNMS.Mobile.Widgets.IHomeWidgets>();
        var welcome = new DashboardWelcome(_settings, _navigation, _preferences, notifier, widgets);
        Assert.Equal("1 of 5", welcome.StepsText);

        await welcome.Steps.Single(s => s.Text == "Allow notifications").Action!.ExecuteAsync(null);
        _appSettings.PinnedDevices.Add(new PinnedDevice { DeviceId = 1 });
        _appSettings.Graylog = new GraylogSettings { Enabled = true, Server = "graylog.example.net" };
        widgets.IsInUse.Returns(true);
        welcome.RebuildSteps();

        Assert.Equal("5 of 5", welcome.StepsText);
        Assert.All(welcome.Steps, s => Assert.False(s.ShowAction));
    }

    [Fact]
    public void An_install_in_use_keeps_its_dashboard_once()
    {
        _appSettings.RecentlyViewedDevices.Add(new RecentlyViewedDevice { DeviceId = 1 });

        var vm = NewDashboard();
        Assert.Equal(DashboardLayout.PreviousDefaultTypes, vm.Cards.Select(c => c.Type));

        // Removed on purpose later: not put back.
        _appSettings.DashboardWidgets.Clear();
        Assert.Empty(NewDashboard().Cards);
    }
}

public sealed class DashboardCardsTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly ILibreNmsClient _client = Fakes.Client(
        devices:
        [
            new Device { DeviceId = 1, Hostname = "core-sw", Status = true, Os = "ios" },
            new Device { DeviceId = 2, Hostname = "wlc-1", Status = true, Os = "arubaos" },
            new Device { DeviceId = 3, Hostname = "wlc-2", Status = false, Os = "arubaos" },
        ],
        alerts: [Fakes.Alert(1, 1, "critical"), Fakes.Alert(2, 1, "warning"), Fakes.Alert(3, 1, "warning", acknowledged: true), Fakes.Alert(4, 1, "critical")]);

    public DashboardCardsTests() => _settings = Fakes.Settings(_appSettings);

    private DashboardViewModel NewViewModel() => new(_client, _settings, new RecordingNavigation(), new DeviceBookmarks(_settings, TimeProvider.System));

    [Fact]
    public async Task The_starter_cards_dont_fetch_sensors_graphs_or_wireless()
    {
        DashboardLayout.UseStarter(_appSettings);
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Same(vm, vm.Cards[0].Dashboard);
        await _client.Sensors.DidNotReceiveWithAnyArgs().ListAsync(default);
        await _client.Graphs.DidNotReceiveWithAnyArgs().GetSvgAsync(default, default!, default!, default, default, default);
        await _client.Devices.DidNotReceiveWithAnyArgs().GetWirelessSensorsAsync(default, default);
    }

    [Fact]
    public async Task Needs_attentions_chips_narrow_it_as_desktops_alerts_widget()
    {
        var widget = DashboardLayout.Add(_appSettings, DashboardLayout.Alerts);
        var vm = NewViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);
        var card = vm.Cards.Single();
        Assert.Equal([1, 4, 2], vm.TopAlerts.Select(a => a.Id)); // critical first, acknowledged left out

        card.ToggleCriticalCommand.Execute(null);
        Assert.Equal([2], vm.TopAlerts.Select(a => a.Id));

        card.ToggleAcknowledgedCommand.Execute(null);
        Assert.Equal([2, 3], vm.TopAlerts.Select(a => a.Id).Order());
        Assert.False(widget.AlertsShowCritical);
        Assert.True(widget.AlertsIncludeAcknowledged);
        _settings.Received().Save();
    }

    [Fact]
    public async Task Recently_viewed_rows_show_each_devices_state_and_when()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.RecentlyViewed);
        _appSettings.RecentlyViewedDevices.Add(new RecentlyViewedDevice { DeviceId = 3, DisplayName = "wlc-2", ViewedAt = DateTimeOffset.Now.AddMinutes(-5) });
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        var row = Assert.Single(vm.RecentlyViewed);
        Assert.Equal(("wlc-2", DeviceState.Down, "5m ago"), (row.Name, row.Status, row.ViewedText));
    }

    [Fact]
    public async Task The_sensors_card_shows_the_picked_sensors_in_order_coloured()
    {
        var card = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        card.Sensors.Add(new PinnedSensor { SensorId = 20, DeviceId = 1 });
        card.Sensors.Add(new PinnedSensor { SensorId = 10, DeviceId = 1 });
        card.Sensors.Add(new PinnedSensor { SensorId = 99, DeviceId = 1 }); // gone from LibreNMS
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 10, DeviceId = 1, SensorClass = "temperature", Description = "Inlet", Current = 30 },
            new Sensor { SensorId = 20, DeviceId = 1, SensorClass = "temperature", Description = "CPU", Current = 80 },
        ]);
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        var sensors = vm.Cards.Single(c => c.Type == DashboardLayout.Sensors).Sensors;
        Assert.Equal(["CPU", "Inlet"], sensors.Select(s => s.Title));
        Assert.Equal(RowStatus.Critical, sensors[0].Status);
        Assert.Equal("core-sw", sensors[0].Subtitle);
    }

    [Fact]
    public async Task Several_sensors_cards_each_show_their_own_from_one_request()
    {
        var temps = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        temps.Title = "Core switch temps";
        temps.Sensors.Add(new PinnedSensor { SensorId = 10, DeviceId = 1 });
        var cpu = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        cpu.Sensors.Add(new PinnedSensor { SensorId = 20, DeviceId = 1 });
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 10, DeviceId = 1, SensorClass = "temperature", Description = "Inlet", Current = 30 },
            new Sensor { SensorId = 20, DeviceId = 1, SensorClass = "temperature", Description = "CPU", Current = 80 },
        ]);
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        var cards = vm.Cards.Where(c => c.Type == DashboardLayout.Sensors).ToList();
        Assert.Equal(["Core switch temps", "Sensors"], cards.Select(c => c.Title));
        Assert.Equal(["Inlet"], cards[0].Sensors.Select(s => s.Title));
        Assert.Equal(["CPU"], cards[1].Sensors.Select(s => s.Title));
        await _client.Sensors.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Each_graph_card_draws_its_own_graph_or_asks_to_be_set_up()
    {
        var empty = DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        var cpu = DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        cpu.GraphDeviceId = 1;
        cpu.GraphName = "device_processor";
        cpu.GraphTimeRangePreset = GraphTimeRangePreset.Week;
        var traffic = DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        traffic.GraphDeviceId = 1;
        traffic.GraphName = "device_bits";
        traffic.Title = "WAN traffic";
        _client.Graphs.GetSvgAsync(1, Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns("<svg width=\"1\" height=\"1\" xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        var cards = vm.Cards.Where(c => c.Type == DashboardLayout.Graph).ToList();
        Assert.True(cards[0].GraphNeedsSetUp);
        Assert.Null(cards[0].GraphPage);
        Assert.False(cards[1].GraphNeedsSetUp);
        Assert.Contains("data:image/svg+xml", cards[1].GraphPage);
        Assert.Equal(["Graph", "device_processor · core-sw", "WAN traffic"], cards.Select(c => c.Title));
        await _client.Graphs.Received(1).GetSvgAsync(1, "device_processor", GraphTimeRange.LastWeek, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _client.Graphs.Received(1).GetSvgAsync(1, "device_bits", Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_cards_hint_opens_that_cards_own_set_up()
    {
        var navigation = new RecordingNavigation();
        var second = DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        var vm = new DashboardViewModel(_client, _settings, navigation, new DeviceBookmarks(_settings, TimeProvider.System));
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.OpenGraphCommand.ExecuteAsync(vm.Cards.First(c => c.Widget.Id == second.Id));

        var visit = Assert.Single(navigation.Visits);
        Assert.Equal(Routes.PickGraph, visit.Route);
        Assert.Equal(second.Id, visit.Parameters![Routes.WidgetIdParameter]);
    }

    [Fact]
    public async Task A_graph_card_opens_its_own_graph_and_range()
    {
        var navigation = new RecordingNavigation();
        var card = DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        card.GraphDeviceId = 3;
        card.GraphName = "device_processor";
        card.GraphTimeRangePreset = GraphTimeRangePreset.Week;
        var vm = new DashboardViewModel(_client, _settings, navigation, new DeviceBookmarks(_settings, TimeProvider.System));

        await vm.OpenGraphCommand.ExecuteAsync(vm.Cards.Single(c => c.Widget.Id == card.Id));

        var visit = Assert.Single(navigation.Visits);
        Assert.Equal(Routes.DeviceGraphs, visit.Route);
        Assert.Equal(3, visit.Parameters![Routes.DeviceIdParameter]);
        Assert.Equal("device_processor", visit.Parameters[Routes.GraphParameter]); // not the device's first graph (#119)
        Assert.Equal(GraphTimeRangePreset.Week, visit.Parameters[Routes.GraphRangeParameter]);
    }

    [Fact]
    public async Task Wireless_asks_one_device_per_os_then_only_the_wireless_ones_down_first()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.Wireless);
        _client.Devices.GetWirelessSensorsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ci => (int)ci[0] switch
        {
            1 => Array.Empty<WirelessSensor>(),
            2 => [new WirelessSensor { SensorClass = "clients", Current = 40 }, new WirelessSensor { SensorClass = "ap-count", Current = 12 }],
            _ => [new WirelessSensor { SensorClass = "clients", Current = 5 }],
        });
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["wlc-2", "wlc-1"], vm.WirelessControllers.Select(c => c.Title));
        Assert.Equal(RowStatus.Critical, vm.WirelessControllers[0].Status);
        Assert.Equal("40 clients", vm.WirelessControllers[1].Value);
        Assert.Equal("12 access points", vm.WirelessControllers[1].Subtitle);
        Assert.Equal("2 controllers", vm.WirelessSummary);
        Assert.Equal(("45", "clients on 12 access points"), (vm.WirelessClientsText, vm.WirelessClientsNote));

        // Probing is done once: a second refresh only asks the controllers.
        _client.Devices.ClearReceivedCalls();
        await vm.RefreshCommand.ExecuteAsync(null);
        await _client.Devices.DidNotReceive().GetWirelessSensorsAsync(1, Arg.Any<CancellationToken>());
    }
}

public sealed class SensorPickerViewModelTests
{
    [Fact]
    public async Task Search_then_tap_adds_a_sensor_to_the_card_and_tapping_again_removes_it()
    {
        var appSettings = new AppSettings();
        var settings = Fakes.Settings(appSettings);
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")]);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 10, DeviceId = 1, SensorClass = "temperature", Description = "Inlet", Current = 30 },
            new Sensor { SensorId = 20, DeviceId = 2, SensorClass = "dbm", Description = "Gi0/1 Rx", Current = -3 },
        ]);
        var vm = new SensorPickerViewModel(client, settings);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Empty(vm.Sensors); // nothing picked, nothing searched

        vm.SearchText = "edge";
        Assert.Equal(["Gi0/1 Rx"], vm.Sensors.Select(s => s.Row.Title));

        vm.ToggleCommand.Execute(vm.Sensors[0]);
        var card = DashboardLayout.Current(appSettings).Single(w => w.WidgetType == DashboardLayout.Sensors);
        Assert.Equal(20, Assert.Single(card.Sensors).SensorId);
        Assert.Equal("dbm", card.Sensors[0].SensorClass);

        vm.SearchText = string.Empty;
        Assert.Single(vm.Sensors); // what's on the card
        vm.ToggleCommand.Execute(vm.Sensors[0]);
        Assert.Empty(card.Sensors);
        settings.Received(2).Save();
    }
}

public sealed class GraphPickerViewModelTests
{
    [Fact]
    public async Task Pick_a_device_then_a_graph_and_range_then_save()
    {
        var appSettings = new AppSettings();
        var settings = Fakes.Settings(appSettings);
        var navigation = new RecordingNavigation();
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")]);
        client.Graphs.ListAsync(2, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_bits", Description = "Traffic" }]);
        client.Graphs.ListHealthAsync(2, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_temperature", Description = "Temperature" }]);
        var vm = new GraphPickerViewModel(client, settings, navigation);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.DeviceSearch = "edge";
        Assert.Equal(["edge-rtr"], vm.Devices.Select(d => d.Name));
        Assert.False(vm.SaveCommand.CanExecute(null));

        await vm.ChooseDeviceCommand.ExecuteAsync(vm.Devices[0]);
        Assert.Equal(["Temperature", "Traffic"], vm.Graphs.Select(g => g.Description));
        vm.SelectedGraph = vm.Graphs[1];
        vm.SelectedRange = vm.Ranges.Single(r => r.Preset == GraphTimeRangePreset.Week);
        await vm.SaveCommand.ExecuteAsync(null);

        var card = DashboardLayout.Current(appSettings).Single(w => w.WidgetType == DashboardLayout.Graph);
        Assert.Equal((2, "device_bits", GraphTimeRangePreset.Week), (card.GraphDeviceId, card.GraphName, card.GraphTimeRangePreset));
        Assert.Equal("Traffic · edge-rtr", card.Title);
        Assert.Equal(Routes.Back, Assert.Single(navigation.Visits).Route);
    }
}

public sealed class DashboardCardSetUpTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly ILibreNmsClient _client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")]);

    public DashboardCardSetUpTests()
    {
        _settings = Fakes.Settings(_appSettings);
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 10, DeviceId = 1, SensorClass = "temperature", Description = "Inlet", Current = 30 },
        ]);
        _client.Graphs.ListAsync(2, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_bits", Description = "Traffic" }]);
        _client.Graphs.ListHealthAsync(2, Arg.Any<CancellationToken>()).Returns([]);
    }

    [Fact]
    public async Task The_sensor_picker_sets_up_the_card_it_was_opened_for_and_names_it()
    {
        var first = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        var second = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        var vm = new SensorPickerViewModel(_client, _settings) { WidgetId = second.Id };
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, vm.CardTitle); // still the kind's

        vm.SearchText = "Inlet";
        vm.ToggleCommand.Execute(vm.Sensors[0]);
        vm.CardTitle = "  Core switch temps ";

        Assert.Empty(first.Sensors);
        Assert.Equal(10, Assert.Single(second.Sensors).SensorId);
        Assert.Equal("Core switch temps", second.Title);

        vm.CardTitle = string.Empty; // back to the kind's
        Assert.Equal("Sensors", second.Title);
    }

    [Fact]
    public async Task The_graph_picker_keeps_a_typed_title_or_names_the_card_after_its_graph()
    {
        var card = DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        var vm = new GraphPickerViewModel(_client, _settings, new RecordingNavigation()) { WidgetId = card.Id };
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.ChooseDeviceCommand.ExecuteAsync(vm.Devices.Single(d => d.DeviceId == 2));
        vm.SelectedGraph = vm.Graphs[0];
        vm.CardTitle = "WAN traffic";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(("WAN traffic", 2, "device_bits"), (card.Title, card.GraphDeviceId, card.GraphName));
        Assert.Single(DashboardLayout.Current(_appSettings), w => w.WidgetType == DashboardLayout.Graph);

        // Opened again, it shows the title it was given.
        var again = new GraphPickerViewModel(_client, _settings, new RecordingNavigation()) { WidgetId = card.Id };
        await again.LoadCommand.ExecuteAsync(null);
        Assert.Equal("WAN traffic", again.CardTitle);
    }
}
