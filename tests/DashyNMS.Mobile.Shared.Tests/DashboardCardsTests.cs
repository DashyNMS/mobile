using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using NSubstitute.ExceptionExtensions;

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
        var unknown = new DashboardWidget { WidgetType = "SomethingNew" };
        var settings = new AppSettings { DashboardWidgets = [unknown] };

        DashboardLayout.UseStarter(settings);

        var starter = StarterDashboard.Create();
        Assert.Equal(starter.Select(w => w.WidgetType), DashboardLayout.Current(settings).Select(w => w.WidgetType));
        Assert.Equal(starter.Select(w => (w.Column, w.Row, w.ColumnSpan, w.RowSpan)),
            DashboardLayout.Current(settings).Select(w => (w.Column, w.Row, w.ColumnSpan, w.RowSpan)));
        Assert.Contains(unknown, settings.DashboardWidgets); // a desktop widget the phone doesn't show, kept

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
    public void Card_kinds_are_filed_in_desktops_categories()
    {
        Assert.All(DashboardLayout.Kinds, k => Assert.Contains(k.Category, DashboardLayout.Categories));
        Assert.Equal(["Alerts", "Devices", "Traffic", "Logs", "Sensors and graphs"], DashboardLayout.Categories);
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
    public async Task Sensors_have_setup_and_alerts_do_not()
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

    [Theory]
    [InlineData(DashboardLayout.Sensors, Routes.PickSensors)]
    [InlineData(DashboardLayout.Graph, Routes.PickGraph)]
    [InlineData(DashboardLayout.TopInterfaces, Routes.TopCardSetUp)]
    [InlineData(DashboardLayout.TopErrors, Routes.TopCardSetUp)]
    [InlineData(DashboardLayout.TopDevices, Routes.TopCardSetUp)]
    [InlineData(DashboardLayout.EventLog, Routes.SpotlightPicker)]
    [InlineData(DashboardLayout.Graylog, Routes.SpotlightPicker)]
    public async Task Setup_opens_the_matching_editor_with_the_selected_widget(string type, string route)
    {
        DashboardLayout.Add(_appSettings, type);
        var selected = DashboardLayout.Add(_appSettings, type);
        var navigation = new RecordingNavigation();
        var vm = NewViewModel(navigation);
        var card = vm.Cards.Single(c => c.Widget.Id == selected.Id);

        Assert.True(card.CanSetUp);
        await vm.SetUpCommand.ExecuteAsync(card);

        var visit = Assert.Single(navigation.Visits);
        Assert.Equal(route, visit.Route);
        Assert.Equal(selected.Id, visit.Parameters![Routes.WidgetIdParameter]);
    }

    [Fact]
    public async Task Unsupported_cards_offer_no_setup_and_do_not_navigate()
    {
        var navigation = new RecordingNavigation();
        var vm = NewViewModel(navigation);
        foreach (var type in new[] { DashboardLayout.AlertsGauge, DashboardLayout.Alerts, DashboardLayout.DeviceStatus,
            DashboardLayout.PinnedDevices, DashboardLayout.RecentlyViewed, DashboardLayout.Wireless })
        {
            var card = new DashboardCardOption(new DashboardWidget { WidgetType = type });
            Assert.False(card.CanSetUp);
            await vm.SetUpCommand.ExecuteAsync(card);
        }
        await vm.SetUpCommand.ExecuteAsync(null);
        Assert.Empty(navigation.Visits);
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
    public void A_cards_screen_reader_text_names_it_and_follows_a_new_title()
    {
        var card = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        card.Title = "Core switch temps";
        var option = NewViewModel().Cards.Single();
        var changed = new List<string?>();
        option.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal("Core switch temps, Sensors, none chosen yet", option.AccessibleDescription);
        Assert.Equal("Set up Core switch temps", option.SetUpDescription);

        card.Title = "Rack temps";
        option.Refresh();

        Assert.Equal("Rack temps, Sensors, none chosen yet", option.AccessibleDescription);
        Assert.Equal("Set up Rack temps", option.SetUpDescription);
        Assert.Equal("More for Rack temps", option.MoreDescription);
        Assert.Contains(nameof(DashboardCardOption.AccessibleDescription), changed);
        Assert.Contains(nameof(DashboardCardOption.SetUpDescription), changed);
        Assert.Contains(nameof(DashboardCardOption.MoreDescription), changed);
    }

    [Fact]
    public void Duplicating_copies_the_set_up_just_below_and_Undo_removes_the_copy()
    {
        var graph = DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        graph.Title = "Traffic";
        graph.GraphDeviceId = 4;
        graph.GraphName = "device_bits";
        DashboardLayout.Add(_appSettings, DashboardLayout.Alerts);
        var vm = NewViewModel();

        vm.DuplicateCommand.Execute(vm.Cards[0]);

        var cards = DashboardLayout.Current(_appSettings);
        Assert.Equal(3, cards.Count);
        var copy = cards[1];
        Assert.NotEqual(graph.Id, copy.Id);
        Assert.Equal(("Traffic (copy)", 4, "device_bits"), (copy.Title, copy.GraphDeviceId, copy.GraphName));
        Assert.Equal("Duplicated ‘Traffic’, with its set-up", _toast.Message);
        Assert.Equal(copy.Id, _toast.HighlightId);
        Assert.Equal(copy.Id, vm.Cards[1].Widget.Id);

        _toast.UndoCommand.Execute(null);
        Assert.Equal([graph.Id, cards[2].Id], DashboardLayout.Current(_appSettings).Select(w => w.Id));
    }

    [Fact]
    public async Task A_cards_menu_duplicates_where_it_can_and_adds_after()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.Alerts);
        DashboardLayout.Add(_appSettings, DashboardLayout.TopErrors);
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns("Add a card after");
        var navigation = new RecordingNavigation();
        var vm = new CustomiseDashboardViewModel(_settings, navigation, _toast, dialogs: dialogs);
        vm.Attach();

        Assert.False(vm.Cards[0].CanDuplicate);
        Assert.True(vm.Cards[1].CanDuplicate);

        await vm.MoreCommand.ExecuteAsync(vm.Cards[0]);

        await dialogs.Received(1).ChooseAsync("Needs attention", Arg.Is<IReadOnlyList<string>>(o => o.SequenceEqual(new[] { "Add a card after", "Move later", "Remove" })));
        var visit = Assert.Single(navigation.Visits);
        Assert.Equal(Routes.AddCard, visit.Route);
        Assert.Equal(1, visit.Parameters![Routes.PositionParameter]);

        dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns("Duplicate");
        await vm.MoreCommand.ExecuteAsync(vm.Cards[1]);
        Assert.Equal(3, DashboardLayout.Current(_appSettings).Count);
    }

    [Theory]
    [InlineData("Move earlier", 1, 0)]
    [InlineData("Move later", 0, 1)]
    public async Task Menu_moves_save_the_new_reading_order(string choice, int source, int destination)
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.Alerts);
        DashboardLayout.Add(_appSettings, DashboardLayout.Graph);
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns(choice);
        var vm = new CustomiseDashboardViewModel(_settings, new RecordingNavigation(), _toast, dialogs: dialogs);
        var moved = vm.Cards[source].Widget.Id;
        await vm.MoreCommand.ExecuteAsync(vm.Cards[source]);
        Assert.Equal(moved, DashboardLayout.Current(_appSettings)[destination].Id);
    }

    [Fact]
    public async Task Menu_removal_can_be_undone()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.Alerts);
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns("Remove");
        var vm = new CustomiseDashboardViewModel(_settings, new RecordingNavigation(), _toast, dialogs: dialogs);
        vm.Attach();
        await vm.MoreCommand.ExecuteAsync(vm.Cards[0]);
        Assert.Empty(vm.Cards);
        _toast.UndoCommand.Execute(null);
        Assert.Single(vm.Cards);
        vm.Detach();
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
    public void Category_changes_notify_both_chips_accessible_selection()
    {
        var vm = NewViewModel();
        var all = vm.Categories[0];
        var traffic = vm.Categories.Single(c => c.Name == "Traffic");
        var notifications = new List<string>();
        all.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CardCategory.SelectionDescription)) notifications.Add(all.SelectionDescription);
        };
        traffic.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CardCategory.SelectionDescription)) notifications.Add(traffic.SelectionDescription);
        };

        Assert.Equal("All 13, selected", all.SelectionDescription);
        Assert.Equal("Traffic 3, not selected", traffic.SelectionDescription);
        vm.ChooseCategoryCommand.Execute(traffic);
        Assert.Equal(["All 13, not selected", "Traffic 3, selected"], notifications);
        notifications.Clear();
        vm.SearchText = "errors";
        Assert.Empty(notifications); // Filtering does not announce a spurious selection change.
        vm.ChooseCategoryCommand.Execute(all);
        Assert.Equal(["All 13, selected", "Traffic 3, not selected"], notifications);
    }

    [Fact]
    public void Card_accessible_selection_notifies_and_survives_search_and_category_changes()
    {
        var vm = NewViewModel();
        var choice = vm.Cards.Single(c => c.Kind.Type == DashboardLayout.TopErrors);
        var descriptions = new List<string>();
        choice.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CardChoice.SelectionDescription)) descriptions.Add(choice.SelectionDescription);
        };
        var prefix = $"{choice.Name}, {choice.Subtitle}, ";
        Assert.Equal(prefix + "not selected", choice.SelectionDescription);
        vm.PickCommand.Execute(choice);
        Assert.Equal(prefix + "selected", Assert.Single(descriptions));
        vm.SearchText = "no matching card";
        vm.ChooseCategoryCommand.Execute(vm.Categories.Single(c => c.Name == "Traffic"));
        vm.SearchText = "";
        Assert.Same(choice, vm.Cards.Single(c => c.Kind.Type == DashboardLayout.TopErrors));
        Assert.Equal(prefix + "selected", choice.SelectionDescription);
        Assert.Same(choice, Assert.Single(vm.Ticked));
        vm.PickCommand.Execute(choice);
        Assert.Equal([prefix + "selected", prefix + "not selected"], descriptions);
        Assert.Empty(vm.Ticked);
    }

    [Fact]
    public void Unavailable_card_announces_reason_and_cannot_be_selected()
    {
        _appSettings.EnablePinnedDevices = false;
        var vm = NewViewModel();
        var choice = vm.Cards.Single(c => c.Kind.Type == DashboardLayout.PinnedDevices);
        Assert.Equal($"{choice.Name}, Turn on pinned devices in Settings, Devices first, not selected", choice.SelectionDescription);
        vm.PickCommand.Execute(choice);
        Assert.False(choice.IsSelected);
        Assert.EndsWith(", not selected", choice.SelectionDescription);
        Assert.Empty(vm.Ticked);
    }

    [Fact]
    public void Every_card_filed_by_category_with_counts()
    {
        var vm = NewViewModel();

        Assert.Equal(["All 13", "Alerts 2", "Devices 4", "Traffic 3", "Logs 2", "Sensors and graphs 2"], vm.Categories.Select(c => c.Label));
        Assert.Equal(13, vm.Cards.Count);
        Assert.Empty(vm.Ticked);
        Assert.Equal("Choose cards", vm.AddText);
        Assert.False(vm.AddCommand.CanExecute(null));

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
        Assert.False(alerts.IsSelected);
        Assert.Empty(vm.Ticked);

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
    public async Task Several_ticked_go_in_together_where_the_picker_was_opened_for()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.Alerts);
        DashboardLayout.Add(_appSettings, DashboardLayout.Wireless);
        var vm = NewViewModel();
        vm.Position = 1;

        vm.PickCommand.Execute(vm.Cards.Single(c => c.Kind.Type == DashboardLayout.TopErrors));
        vm.PickCommand.Execute(vm.Cards.Single(c => c.Kind.Type == DashboardLayout.Graph));
        vm.PickCommand.Execute(vm.Cards.Single(c => c.Kind.Type == DashboardLayout.TopDevices));
        vm.PickCommand.Execute(vm.Cards.Single(c => c.Kind.Type == DashboardLayout.TopDevices)); // and untick
        Assert.Equal("Add 2 cards", vm.AddText);

        // Ticks stay when the category changes.
        vm.ChooseCategoryCommand.Execute(vm.Categories.Single(c => c.Name == "Logs"));
        Assert.Equal(2, vm.Ticked.Count);

        await vm.AddCommand.ExecuteAsync(null);

        Assert.Equal(
            [DashboardLayout.Alerts, DashboardLayout.TopErrors, DashboardLayout.Graph, DashboardLayout.Wireless],
            DashboardLayout.Current(_appSettings).Select(w => w.WidgetType));
        Assert.Equal("2 cards added", _toast.Message);
        Assert.Equal(DashboardLayout.Current(_appSettings)[1].Id, _toast.HighlightId);
        Assert.Equal([Routes.Back], _navigation.Visits.Select(v => v.Route)); // several: no set-up to go on to

        _toast.UndoCommand.Execute(null);
        Assert.Equal([DashboardLayout.Alerts, DashboardLayout.Wireless], DashboardLayout.Current(_appSettings).Select(w => w.WidgetType));
    }

    [Fact]
    public void Suggested_picks_out_what_fits_the_set_up_and_opens_first()
    {
        _appSettings.PinnedDevices.Add(new PinnedDevice { DeviceId = 1 });
        _appSettings.PinnedDevices.Add(new PinnedDevice { DeviceId = 2 });
        _appSettings.Graylog = new GraylogSettings { Enabled = true, Server = "graylog.example.com" };

        var vm = NewViewModel();

        Assert.Equal(["All 13", "Suggested 2"], vm.Categories.Take(2).Select(c => c.Label));
        Assert.True(vm.Categories[1].IsSelected);
        Assert.Equal(["Pinned devices", "Graylog"], vm.Cards.Select(c => c.Name));
        Assert.Equal("You've pinned 2 devices", vm.Cards[0].Subtitle);
        Assert.Equal("Graylog is connected", vm.Cards[1].Subtitle);

        // Once one's on the dashboard, it isn't suggested any more.
        DashboardLayout.Add(_appSettings, DashboardLayout.Graylog);
        vm.Load();
        Assert.Equal(["Pinned devices"], vm.Cards.Select(c => c.Name));
    }

    [Fact]
    public void Graylog_needs_setting_up_first()
    {
        var graylog = NewViewModel().Cards.Single(c => c.Kind.Type == DashboardLayout.Graylog);

        Assert.Equal("Set up Graylog in Settings first", graylog.UnavailableReason);
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
            new Device { DeviceId = 1, Hostname = "core-sw", Status = true, Disabled = false, Ignore = false, Os = "ios" },
            new Device { DeviceId = 2, Hostname = "wlc-1", Status = true, Disabled = false, Ignore = false, Os = "arubaos" },
            new Device { DeviceId = 3, Hostname = "wlc-2", Status = false, Disabled = false, Ignore = false, Os = "arubaos" },
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
        var card = vm.Cards.Single();

        // Before first load: chip labels and VoiceOver descriptions omit counts.
        Assert.Equal("Critical \u00b7 On", card.CriticalChipText);
        Assert.Equal("Warning \u00b7 On", card.WarningChipText);
        Assert.Equal("Acknowledged \u00b7 Off", card.AcknowledgedChipText);
        Assert.Equal("Critical, included", card.CriticalFilterDescription);
        Assert.Equal("Acknowledged, excluded", card.AcknowledgedFilterDescription);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal([1, 4, 2], vm.TopAlerts.Select(a => a.Id)); // critical first, acknowledged left out
        Assert.Equal("Critical 2 \u00b7 On", card.CriticalChipText);
        Assert.Equal("Warning 1 \u00b7 On", card.WarningChipText);
        Assert.Equal("Acknowledged 1 \u00b7 Off", card.AcknowledgedChipText);
        Assert.Equal("Critical 2, included", card.CriticalFilterDescription);
        Assert.Equal("Warning 1, included", card.WarningFilterDescription);
        Assert.Equal("Acknowledged 1, excluded", card.AcknowledgedFilterDescription);

        card.ToggleCriticalCommand.Execute(null);
        Assert.Equal([2], vm.TopAlerts.Select(a => a.Id));
        Assert.Equal("Critical 2 \u00b7 Off", card.CriticalChipText);
        Assert.Equal("Critical 2, excluded", card.CriticalFilterDescription);

        card.ToggleAcknowledgedCommand.Execute(null);
        Assert.Equal([2, 3], vm.TopAlerts.Select(a => a.Id).Order());
        Assert.False(widget.AlertsShowCritical);
        Assert.True(widget.AlertsIncludeAcknowledged);
        Assert.Equal("Acknowledged 1 \u00b7 On", card.AcknowledgedChipText);
        Assert.Equal("Acknowledged 1, included", card.AcknowledgedFilterDescription);
        _settings.Received().Save();
    }

    [Fact]
    public async Task Needs_attention_explains_filter_empty_state_and_clears_only_card_filters()
    {
        _appSettings.Filter.ShowCritical = false;
        _appSettings.Filter.ShowWarning = true;
        _appSettings.Filter.SearchText = "core";
        DashboardLayout.Add(_appSettings, DashboardLayout.Alerts);
        var vm = NewViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);
        var card = vm.Cards.Single();

        card.ToggleCriticalCommand.Execute(null);
        card.ToggleWarningCommand.Execute(null);

        Assert.True(vm.HasNoAlerts);
        Assert.Equal("No alerts match these filters.", vm.AlertsEmptyText);
        Assert.True(vm.CanClearAlertFilters);

        card.ClearAlertFiltersCommand.Execute(null);

        Assert.True(card.ShowsCritical);
        Assert.True(card.ShowsWarning);
        Assert.False(card.IncludesAcknowledged);
        Assert.False(vm.HasNoAlerts);
        Assert.False(vm.CanClearAlertFilters);
        Assert.Equal([1, 4, 2], vm.TopAlerts.Select(a => a.Id));
        // Alerts tab saved filters remain untouched.
        Assert.False(_appSettings.Filter.ShowCritical);
        Assert.Equal("core", _appSettings.Filter.SearchText);
    }

    [Fact]
    public async Task Needs_attention_explains_acknowledged_only_fixture()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.Alerts);
        var client = Fakes.Client(
            devices: [new Device { DeviceId = 1, Hostname = "core-sw", Status = true }],
            alerts: [Fakes.Alert(1, 1, "critical", acknowledged: true), Fakes.Alert(2, 1, "warning", acknowledged: true)]);
        var vm = new DashboardViewModel(client, _settings, new RecordingNavigation(), new DeviceBookmarks(_settings, TimeProvider.System));
        await vm.RefreshCommand.ExecuteAsync(null);
        var card = vm.Cards.Single();

        Assert.True(vm.HasNoAlerts);
        Assert.Equal("No unacknowledged critical or warning alerts. Turn on Acknowledged to show them.", vm.AlertsEmptyText);
        Assert.False(vm.CanClearAlertFilters);

        card.ToggleAcknowledgedCommand.Execute(null);
        Assert.False(vm.HasNoAlerts);
        Assert.Equal([1, 2], vm.TopAlerts.Select(a => a.Id));

        card.ToggleCriticalCommand.Execute(null);
        card.ToggleWarningCommand.Execute(null);
        Assert.True(vm.HasNoAlerts);
        Assert.Equal("No alerts match these filters.", vm.AlertsEmptyText);
        Assert.True(vm.CanClearAlertFilters);
    }

    [Fact]
    public async Task Needs_attention_explains_ok_only_fixture_scope()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.Alerts);
        var client = Fakes.Client(
            devices: [new Device { DeviceId = 1, Hostname = "core-sw", Status = true }],
            alerts: [Fakes.Alert(1, 1, "ok"), Fakes.Alert(2, 1, "ok", acknowledged: true)]);
        var vm = new DashboardViewModel(client, _settings, new RecordingNavigation(), new DeviceBookmarks(_settings, TimeProvider.System));
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.HasNoAlerts);
        Assert.Equal("Only critical and warning alerts appear in this card.", vm.AlertsEmptyText);
        Assert.False(vm.CanClearAlertFilters);
    }

    [Fact]
    public async Task Needs_attention_explains_loaded_empty_fleet_and_stays_quiet_on_failed_first_load()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.Alerts);
        var emptyClient = Fakes.Client(devices: [new Device { DeviceId = 1, Hostname = "core-sw", Status = true }], alerts: []);
        var emptyVm = new DashboardViewModel(emptyClient, _settings, new RecordingNavigation(), new DeviceBookmarks(_settings, TimeProvider.System));
        await emptyVm.RefreshCommand.ExecuteAsync(null);

        Assert.True(emptyVm.HasNoAlerts);
        Assert.Equal("No open alerts.", emptyVm.AlertsEmptyText);
        Assert.False(emptyVm.CanClearAlertFilters);

        var failingClient = Fakes.Client();
        failingClient.Alerts.ListAsync(Arg.Any<AlertQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new LibreNmsApiException("Unavailable", System.Net.HttpStatusCode.ServiceUnavailable));
        var failingVm = new DashboardViewModel(failingClient, _settings, new RecordingNavigation(), new DeviceBookmarks(_settings, TimeProvider.System));
        await failingVm.RefreshCommand.ExecuteAsync(null);

        Assert.True(failingVm.HasError);
        Assert.False(failingVm.HasNoAlerts);
        Assert.Equal(string.Empty, failingVm.AlertsEmptyText);
        Assert.False(failingVm.CanClearAlertFilters);
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

    /// <summary>A custom reading with nothing to judge it against stays uncoloured on the card (#532).</summary>
    [Fact]
    public async Task The_sensors_card_keeps_unassessed_readings_uncoloured()
    {
        var card = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        card.Sensors.Add(new PinnedSensor { SensorId = 30, DeviceId = 1 });
        card.Sensors.Add(new PinnedSensor { SensorId = 31, DeviceId = 1 });
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 30, DeviceId = 1, SensorClass = "synthetic_flow", Description = "Flow", Current = 12.34, Unit = "widgets" },
            new Sensor { SensorId = 31, DeviceId = 1, SensorClass = "synthetic_flow", Description = "Flow warn", Current = 15, LimitHighWarn = 10 },
        ]);
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        var sensors = vm.Cards.Single(c => c.Type == DashboardLayout.Sensors).Sensors;
        Assert.Equal([RowStatus.None, RowStatus.Warning], sensors.Select(s => s.Status));
        Assert.Equal("12.3 widgets", sensors[0].Value);
    }

    [Fact]
    public async Task A_slow_sensor_list_doesnt_hold_up_the_rest_of_the_dashboard()
    {
        var card = DashboardLayout.Add(_appSettings, DashboardLayout.Sensors);
        card.Sensors.Add(new PinnedSensor { SensorId = 10, DeviceId = 1 });
        var slow = new TaskCompletionSource<IReadOnlyList<Sensor>>();
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(slow.Task);
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.NotNull(vm.LastUpdated);
        var shown = vm.Cards.Single(c => c.Type == DashboardLayout.Sensors);
        Assert.Equal("Updating…", shown.SensorStatus);
        Assert.False(shown.HasNoSensors);

        slow.SetResult([new Sensor { SensorId = 10, DeviceId = 1, SensorClass = "temperature", Description = "Inlet", Current = 30 }]);
        await vm.SensorsLoaded;

        Assert.Null(shown.SensorStatus);
        Assert.Equal(["Inlet"], shown.Sensors.Select(s => s.Title));
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
        _client.Graphs.GetSvgAsync(1, Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false) // small graphs: no legend (#158)
            .Returns("<svg width=\"1\" height=\"1\" xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        var cards = vm.Cards.Where(c => c.Type == DashboardLayout.Graph).ToList();
        Assert.True(cards[0].GraphNeedsSetUp);
        Assert.Null(cards[0].GraphPage);
        Assert.False(cards[1].GraphNeedsSetUp);
        Assert.Contains("data:image/svg+xml", cards[1].GraphPage);
        Assert.Equal(["Graph", "device_processor · core-sw", "WAN traffic"], cards.Select(c => c.Title));
        await _client.Graphs.Received(1).GetSvgAsync(1, "device_processor", GraphTimeRange.LastWeek, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false);
        await _client.Graphs.Received(1).GetSvgAsync(1, "device_bits", Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false);
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
    public async Task Search_and_toggle_edit_a_draft_until_save()
    {
        var appSettings = new AppSettings();
        var settings = Fakes.Settings(appSettings);
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")]);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 10, DeviceId = 1, SensorClass = "temperature", Description = "Inlet", Current = 30 },
            new Sensor { SensorId = 20, DeviceId = 2, SensorClass = "dbm", Description = "Gi0/1 Rx", Current = -3 },
        ]);
        var vm = new SensorPickerViewModel(client, settings, new RecordingNavigation());
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Empty(vm.Sensors); // nothing picked, nothing searched

        vm.SearchText = "edge";
        Assert.Equal(["Gi0/1 Rx"], vm.Sensors.Select(s => s.Row.Title));

        vm.ToggleCommand.Execute(vm.Sensors[0]);
        Assert.Empty(appSettings.DashboardWidgets);
        settings.DidNotReceive().Save();
        await vm.SaveCommand.ExecuteAsync(null);
        var card = DashboardLayout.Current(appSettings).Single(w => w.WidgetType == DashboardLayout.Sensors);
        Assert.Equal(20, Assert.Single(card.Sensors).SensorId);
        Assert.Equal("dbm", card.Sensors[0].SensorClass);

        var again = new SensorPickerViewModel(client, settings, new RecordingNavigation()) { WidgetId = card.Id };
        await again.LoadCommand.ExecuteAsync(null);
        Assert.Single(again.Sensors);
        again.ToggleCommand.Execute(again.Sensors[0]);
        Assert.Empty(again.Sensors);
        Assert.Single(card.Sensors);
        await again.SaveCommand.ExecuteAsync(null);
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

    [Fact]
    public async Task Devices_and_graphs_are_in_reading_order_and_ports_in_interface_order()
    {
        var client = Fakes.Client(devices: [Fakes.Device(1, "sw-10"), Fakes.Device(2, "sw-2"), Fakes.Device(3, "Sw-1")]);
        client.Graphs.ListAsync(2, Arg.Any<CancellationToken>()).Returns(
        [
            new GraphType { Name = "b", Description = "Processor 10" },
            new GraphType { Name = "a", Description = "Processor 2" },
        ]);
        client.Graphs.ListHealthAsync(2, Arg.Any<CancellationToken>()).Returns([]);
        client.Ports.ListForDeviceAsync(2, Arg.Any<CancellationToken>()).Returns(
        [
            new Port { PortId = 30, IfIndex = 10, IfName = "Gi0/10" },
            new Port { PortId = 31, IfIndex = 2, IfName = "Gi0/2" },
            new Port { PortId = 32, IfIndex = 1, IfName = "Te0/9" },
        ]);
        var vm = new GraphPickerViewModel(client, Fakes.Settings(), new RecordingNavigation());
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["Sw-1", "sw-2", "sw-10"], vm.Devices.Select(d => d.Name));

        await vm.ChooseDeviceCommand.ExecuteAsync(vm.Devices[1]);
        Assert.Equal(["Processor 2", "Processor 10"], vm.Graphs.Select(g => g.Description));
        Assert.Equal(["Te0/9", "Gi0/2", "Gi0/10"], vm.Sources.Skip(1).Select(s => s.IfName));
    }

    [Fact]
    public async Task The_heading_and_device_labels_follow_the_step()
    {
        var client = Fakes.Client(devices: [Fakes.Device(2, "edge-rtr")]);
        client.Graphs.ListAsync(2, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_bits", Description = "Traffic" }]);
        client.Graphs.ListHealthAsync(2, Arg.Any<CancellationToken>()).Returns([]);
        var vm = new GraphPickerViewModel(client, Fakes.Settings(), new RecordingNavigation());
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal("Choose a device", vm.StepTitle);

        await vm.ChooseDeviceCommand.ExecuteAsync(vm.Devices[0]);
        Assert.Equal("Choose a graph", vm.StepTitle);
        Assert.Equal("Change device, currently edge-rtr", vm.ChangeDeviceDescription);
        Assert.False(vm.HasNoGraphs);

        vm.ChangeDeviceCommand.Execute(null);
        Assert.Equal("Choose a device", vm.StepTitle);
    }

    [Fact]
    public async Task An_empty_device_list_says_whether_the_search_matched_nothing()
    {
        var vm = new GraphPickerViewModel(Fakes.Client(devices: [Fakes.Device(1, "core-sw")]), Fakes.Settings(), new RecordingNavigation());
        await vm.LoadCommand.ExecuteAsync(null);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.DeviceSearch = " zzz ";
        Assert.Empty(vm.Devices);
        Assert.Equal("No devices match \"zzz\"", vm.EmptyText);
        Assert.Contains(nameof(vm.EmptyText), changed);

        var none = new GraphPickerViewModel(Fakes.Client(devices: []), Fakes.Settings(), new RecordingNavigation());
        await none.LoadCommand.ExecuteAsync(null);
        Assert.Equal("No devices found", none.EmptyText);
    }

    [Fact]
    public async Task A_device_without_graphs_says_so()
    {
        var client = Fakes.Client(devices: [Fakes.Device(2, "edge-rtr")]);
        client.Graphs.ListAsync(2, Arg.Any<CancellationToken>()).Returns([]);
        client.Graphs.ListHealthAsync(2, Arg.Any<CancellationToken>()).Returns([]);
        var vm = new GraphPickerViewModel(client, Fakes.Settings(), new RecordingNavigation());
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ChooseDeviceCommand.ExecuteAsync(vm.Devices[0]);
        Assert.True(vm.HasNoGraphs);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_failed_device_list_says_how_to_retry_instead_of_claiming_no_devices()
    {
        var client = Fakes.Client();
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var vm = new GraphPickerViewModel(client, Fakes.Settings(), new RecordingNavigation());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal(string.Empty, vm.EmptyText);
        Assert.Equal("To retry, go back and open graph setup again.", vm.RecoveryText);
    }

    [Fact]
    public async Task A_failed_graph_list_points_to_change_device_and_clears_when_it_is_used()
    {
        var client = Fakes.Client(devices: [Fakes.Device(2, "edge-rtr")]);
        client.Graphs.ListAsync(2, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        client.Graphs.ListHealthAsync(2, Arg.Any<CancellationToken>()).Returns([]);
        var vm = new GraphPickerViewModel(client, Fakes.Settings(), new RecordingNavigation());
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ChooseDeviceCommand.ExecuteAsync(vm.Devices[0]);
        Assert.Equal("To retry, use Change device and choose this device again.", vm.RecoveryText);
        Assert.False(vm.HasNoGraphs); // the error explains the empty list

        vm.ChangeDeviceCommand.Execute(null);
        Assert.False(vm.HasError);
        Assert.Null(vm.RecoveryText);
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
        var vm = new SensorPickerViewModel(_client, _settings, new RecordingNavigation()) { WidgetId = second.Id };
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, vm.CardTitle); // still the kind's

        vm.SearchText = "Inlet";
        vm.ToggleCommand.Execute(vm.Sensors[0]);
        vm.CardTitle = "  Core switch temps ";

        Assert.Empty(first.Sensors);
        Assert.Empty(second.Sensors);
        Assert.Equal("Sensors", second.Title);
        _settings.DidNotReceive().Save();
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(10, Assert.Single(second.Sensors).SensorId);
        Assert.Equal("Core switch temps", second.Title);

        var again = new SensorPickerViewModel(_client, _settings, new RecordingNavigation()) { WidgetId = second.Id };
        await again.LoadCommand.ExecuteAsync(null);
        again.CardTitle = string.Empty;
        await again.SaveCommand.ExecuteAsync(null);
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

public sealed class DashboardColumnsTests
{
    [Theory]
    [InlineData(320, 1)]
    [InlineData(691, 1)]
    [InlineData(692, 2)]
    [InlineData(900, 2)]
    [InlineData(1044, 3)]
    [InlineData(1600, 3)]
    public void Editor_and_dashboard_share_column_breakpoints(double width, int expected) =>
        Assert.Equal(expected, DashboardColumns.Count(width));
}

public sealed class DashboardGridLayoutTests
{
    private static readonly string[] DemoCards =
    [
        DashboardLayout.AlertsGauge,
        DashboardLayout.DeviceStatus,
        DashboardLayout.Alerts,
        DashboardLayout.Sensors,
        DashboardLayout.Graph,
        DashboardLayout.PinnedDevices,
        DashboardLayout.TopInterfaces,
        DashboardLayout.EventLog,
    ];

    private static readonly string[] StarterCards =
    [
        DashboardLayout.AlertsGauge,
        DashboardLayout.DeviceStatus,
        DashboardLayout.Alerts,
        DashboardLayout.TopInterfaces,
        DashboardLayout.RecentlyViewed,
    ];

    [Fact]
    public void Wide_three_column_demo_dashboard_spans_needs_attention_and_pulls_telemetry_under_summaries()
    {
        double[] heights = [180, 160, 440, 260, 240, 220, 210, 250];

        var layout = DashboardGridLayout.Arrange(
            availableWidth: 1148,
            cardTypes: DemoCards,
            naturalHeights: heights,
            allowLeadingTriageSpan: true);

        Assert.Equal(3, layout.Columns);
        Assert.True(layout.SpansLeadingTriage);

        // Row 0: Alerts and Devices stay aligned at the taller summary height (180), not stretched to 440.
        Assert.Equal(0, layout.Frames[0].Y);
        Assert.Equal(0, layout.Frames[1].Y);
        Assert.Equal(180, layout.Frames[0].Height);
        Assert.Equal(180, layout.Frames[1].Height);

        // Row 1: Sensors and Graph sit directly beneath Alerts and Devices at y = 180 + 12 = 192.
        Assert.Equal(192, layout.Frames[3].Y);
        Assert.Equal(192, layout.Frames[4].Y);
        Assert.Equal(layout.Frames[0].X, layout.Frames[3].X);
        Assert.Equal(layout.Frames[1].X, layout.Frames[4].X);
        Assert.Equal(260, layout.Frames[3].Height);
        Assert.Equal(260, layout.Frames[4].Height);

        // Needs attention spans Row 0 + gap + Row 1 = 180 + 12 + 260 = 452 in column 2.
        Assert.Equal(0, layout.Frames[2].Y);
        Assert.Equal(452, layout.Frames[2].Height);
        Assert.True(layout.Frames[2].X > layout.Frames[1].X);

        // Row 2: Pinned, Top interfaces and Event log flow across all three columns below y = 452 + 12 = 464.
        Assert.Equal(464, layout.Frames[5].Y);
        Assert.Equal(464, layout.Frames[6].Y);
        Assert.Equal(464, layout.Frames[7].Y);
        Assert.Equal(layout.Frames[0].X, layout.Frames[5].X);
        Assert.Equal(layout.Frames[1].X, layout.Frames[6].X);
        Assert.Equal(layout.Frames[2].X, layout.Frames[7].X);
        Assert.Equal(250, layout.Frames[5].Height);
        Assert.Equal(464 + 250, layout.TotalHeight);
    }

    [Fact]
    public void Tall_needs_attention_expands_second_row_without_stretching_summary_row_or_clipping()
    {
        double[] heights = [180, 160, 560, 220, 200, 210];

        var layout = DashboardGridLayout.Arrange(
            availableWidth: 1148,
            cardTypes: DemoCards[..6],
            naturalHeights: heights,
            allowLeadingTriageSpan: true);

        Assert.True(layout.SpansLeadingTriage);
        Assert.Equal(180, layout.Frames[0].Height);
        Assert.Equal(180, layout.Frames[1].Height);

        // Row 1 grows from 220 to 560 - 180 - 12 = 368 so Needs attention (560) fits without clipping.
        Assert.Equal(368, layout.Frames[3].Height);
        Assert.Equal(368, layout.Frames[4].Height);
        Assert.Equal(560, layout.Frames[2].Height);

        // Row 2 starts cleanly below the spanning card at 560 + 12 = 572.
        Assert.Equal(572, layout.Frames[5].Y);
        Assert.Equal(572 + 210, layout.TotalHeight);
    }

    [Fact]
    public void Starter_dashboard_places_top_interfaces_and_recently_viewed_under_summaries()
    {
        double[] heights = [180, 160, 420, 240, 210];

        var layout = DashboardGridLayout.Arrange(
            availableWidth: 1148,
            cardTypes: StarterCards,
            naturalHeights: heights,
            allowLeadingTriageSpan: true);

        Assert.True(layout.SpansLeadingTriage);
        Assert.Equal(5, layout.Frames.Count);
        Assert.Equal(192, layout.Frames[3].Y);
        Assert.Equal(192, layout.Frames[4].Y);
        Assert.Equal(layout.Frames[0].X, layout.Frames[3].X);
        Assert.Equal(layout.Frames[1].X, layout.Frames[4].X);
        Assert.Equal(432, layout.Frames[2].Height);
    }

    [Theory]
    [InlineData(788, 2)]  // iPad portrait
    [InlineData(850, 2)]  // Duo inner landscape
    [InlineData(637, 1)]  // Duo inner portrait
    [InlineData(370, 1)]  // Standard iPhone portrait
    public void Two_column_and_compact_widths_keep_standard_card_order(double width, int expectedColumns)
    {
        double[] heights = [180, 160, 440, 260, 240];

        var layout = DashboardGridLayout.Arrange(
            availableWidth: width,
            cardTypes: DemoCards[..5],
            naturalHeights: heights,
            allowLeadingTriageSpan: true);

        Assert.Equal(expectedColumns, layout.Columns);
        Assert.False(layout.SpansLeadingTriage);
        var slots = DashboardGridLayout.PlanSlots(expectedColumns, DemoCards[..5], allowLeadingTriageSpan: true);
        Assert.All(slots, s => Assert.Equal(1, s.RowSpan));
        Assert.Equal(Enumerable.Range(0, 5), slots.Select(s => s.Index));
    }

    [Fact]
    public void Reordered_or_incomplete_leading_sequence_uses_standard_three_column_grid()
    {
        string[][] nonMatching =
        [
            [DashboardLayout.Alerts, DashboardLayout.AlertsGauge, DashboardLayout.DeviceStatus, DashboardLayout.Sensors],
            [DashboardLayout.DeviceStatus, DashboardLayout.AlertsGauge, DashboardLayout.Alerts, DashboardLayout.Sensors],
            [DashboardLayout.AlertsGauge, DashboardLayout.Alerts, DashboardLayout.Sensors, DashboardLayout.Graph],
            [DashboardLayout.DeviceStatus, DashboardLayout.Alerts, DashboardLayout.Sensors, DashboardLayout.Graph],
            [DashboardLayout.AlertsGauge, DashboardLayout.DeviceStatus, DashboardLayout.Alerts],
        ];

        foreach (var sequence in nonMatching)
        {
            var heights = Enumerable.Repeat(200d, sequence.Length).ToArray();
            var layout = DashboardGridLayout.Arrange(1148, sequence, heights, allowLeadingTriageSpan: true);
            Assert.False(layout.SpansLeadingTriage);
        }
    }

    [Fact]
    public void Opt_out_flag_keeps_standard_grid_even_at_three_columns()
    {
        double[] heights = [180, 160, 440, 260, 240];

        var layout = DashboardGridLayout.Arrange(
            availableWidth: 1148,
            cardTypes: DemoCards[..5],
            naturalHeights: heights,
            allowLeadingTriageSpan: false);

        Assert.Equal(3, layout.Columns);
        Assert.False(layout.SpansLeadingTriage);
        Assert.Equal(440, layout.Frames[0].Height);
    }

    [Fact]
    public void Visual_reading_order_matches_rendered_placement_top_to_bottom_left_to_right()
    {
        double[] heights = [180, 160, 440, 260, 240, 220, 210, 250];
        var layout = DashboardGridLayout.Arrange(1148, DemoCards, heights, allowLeadingTriageSpan: true);

        var visualOrder = layout.Frames
            .OrderBy(f => f.Y)
            .ThenBy(f => f.X)
            .Select(f => f.Index)
            .ToArray();

        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7], visualOrder);
    }
}
