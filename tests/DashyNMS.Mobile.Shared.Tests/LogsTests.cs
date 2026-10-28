using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Logs;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class LogsViewModelTests
{
    private static readonly DateTime Noon = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Local);

    private readonly ILibreNmsClient _client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")]);
    private readonly RecordingNavigation _navigation = new();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly InMemoryPreferences _preferences = new();

    private ILogsApi Logs => _client.Logs;

    private LogsViewModel NewViewModel() => new(_client, Fakes.Settings(), _navigation, _dialogs, _preferences)
    {
        SearchDelay = TimeSpan.Zero,
        Now = () => Noon,
    };

    /// <summary>Newest first, a minute apart, alternating devices; the first critical.</summary>
    private static IReadOnlyList<EventLogEntry> Events(int count, string type = "interface") => Enumerable.Range(1, count)
        .Select(i => new EventLogEntry
        {
            Id = i,
            DeviceId = 1 + i % 2,
            Message = $"Event {i}",
            Type = type,
            Severity = i == 1 ? 5 : 2,
            Timestamp = Noon.AddMinutes(-i),
        })
        .ToList();

    private void Returns(IReadOnlyList<EventLogEntry> entries) =>
        Logs.ListEventLogAsync(Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(entries);

    private List<string> ChoicesOffered(string title)
    {
        var offered = new List<string>();
        _dialogs.ChooseAsync(title, Arg.Do<IReadOnlyList<string>>(o => offered.AddRange(o)));
        return offered;
    }

    [Fact]
    public async Task Shows_every_devices_events_with_the_device_named()
    {
        Logs.ListEventLogAsync(null, LogsViewModel.PageSize, Arg.Any<CancellationToken>()).Returns(Events(3));
        var vm = NewViewModel();

        await vm.EnsureLoadedAsync();

        Assert.Equal(["Event 1", "Event 2", "Event 3"], vm.Entries.Select(e => e.Message));
        Assert.Equal(("edge-rtr", "interface", "1m"), (vm.Entries[0].DeviceName, vm.Entries[0].MetaText, vm.Entries[0].ShortAge));
        Assert.Equal(RowStatus.Critical, vm.Entries[0].Status);
        Assert.Equal("3 events, newest first", vm.SummaryText);
        Assert.False(vm.CanLoadMore); // fewer than a page: that's all there is
    }

    [Fact]
    public async Task Load_more_asks_for_a_bigger_page()
    {
        Logs.ListEventLogAsync(null, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ci => Events((int)ci[1]));
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();
        Assert.True(vm.CanLoadMore);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(2 * LogsViewModel.PageSize, vm.Entries.Count);
        await Logs.Received(1).ListEventLogAsync(null, 2 * LogsViewModel.PageSize, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_type_chip_offers_each_type_once_whatever_its_case()
    {
        Logs.ListEventLogAsync(null, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
        [
            new EventLogEntry { Id = 3, DeviceId = 1, Message = "Gi1/0/3 down", Type = "interface", Timestamp = Noon.AddMinutes(-1) },
            new EventLogEntry { Id = 2, DeviceId = 2, Message = "Rebooted", Type = "Reboot", Timestamp = Noon.AddMinutes(-2) },
            new EventLogEntry { Id = 1, DeviceId = 1, Message = "Gi1/0/4 down", Type = "Interface", Timestamp = Noon.AddMinutes(-3) },
        ]);
        var offered = ChoicesOffered("Type");
        _dialogs.ChooseAsync("Type", Arg.Any<IReadOnlyList<string>>()).Returns("interface");
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();

        await vm.ChooseTypeCommand.ExecuteAsync(null);

        Assert.Equal(["Any type", "interface", "Reboot"], offered);
        Assert.Equal(["Gi1/0/3 down", "Gi1/0/4 down"], vm.Entries.Select(e => e.Message));
        Assert.Equal("2 interface events in the last 3", vm.SummaryText);
        Assert.True(vm.HasActiveFilters);
    }

    [Fact]
    public async Task The_type_is_remembered_for_next_time()
    {
        Returns(Events(3));
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();

        vm.EventType = "interface";

        var next = NewViewModel();
        Assert.Equal("interface", next.EventType);
        await next.EnsureLoadedAsync();
        await Logs.Received().ListEventLogAsync(null, EventLogFeed.FilteredFetchLimit, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_matches_the_sysname_and_the_name_shown_and_says_what_it_looked_through()
    {
        Returns(
        [
            new EventLogEntry { Id = 2, DeviceId = 2, Message = "Port up", SysName = "dist-03", Timestamp = Noon.AddMinutes(-1) },
            new EventLogEntry { Id = 1, DeviceId = 1, Message = "Port down", Timestamp = Noon.AddMinutes(-2) },
        ]);
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();

        vm.SearchText = "dist-03";
        Assert.Equal(["Port up"], vm.Entries.Select(e => e.Message));

        vm.SearchText = "core-sw";
        Assert.Equal(["Port down"], vm.Entries.Select(e => e.Message));

        vm.SearchText = "reboot";
        Assert.True(vm.IsEmpty);
        Assert.Equal("No events in the last 2. Clear the filters to see the rest.", vm.EmptyText);
    }

    [Fact]
    public async Task A_type_or_search_fetches_deeper_but_only_while_theres_more()
    {
        Logs.ListEventLogAsync(null, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ci => Events((int)ci[1]));
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();

        // The time range can't be asked of LibreNMS, so it only narrows.
        vm.RangeIndex = 1;
        await Logs.DidNotReceive().ListEventLogAsync(null, EventLogFeed.FilteredFetchLimit, Arg.Any<CancellationToken>());

        vm.EventType = "interface";
        await Logs.Received(1).ListEventLogAsync(null, EventLogFeed.FilteredFetchLimit, Arg.Any<CancellationToken>());

        // 250 loaded already: a search on top needs no more.
        vm.SearchText = "Event 1";
        await Logs.Received(1).ListEventLogAsync(null, EventLogFeed.FilteredFetchLimit, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_time_range_narrows_whats_loaded()
    {
        Returns(
        [
            new EventLogEntry { Id = 2, DeviceId = 1, Message = "Recent", Timestamp = Noon.AddMinutes(-30) },
            new EventLogEntry { Id = 1, DeviceId = 1, Message = "Yesterday", Timestamp = Noon.AddHours(-26) },
        ]);
        _dialogs.ChooseAsync("Time range", Arg.Any<IReadOnlyList<string>>()).Returns("Last 24 hours");
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();

        await vm.ChooseRangeCommand.ExecuteAsync(null);

        Assert.Equal(["Recent"], vm.Entries.Select(e => e.Message));
        Assert.Equal("Last 24 hours", vm.RangeChipText);
        Assert.Equal("1 event in the last 2 · last 24 hours", vm.SummaryText);

        vm.ClearFiltersCommand.Execute(null);
        Assert.Equal(2, vm.Entries.Count);
        Assert.False(vm.HasActiveFilters);
    }

    [Fact]
    public async Task The_device_chip_asks_LibreNMS_for_that_devices_log()
    {
        Returns(Events(5));
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();

        // Busiest first: devices 2 (three entries) and 1 (two).
        Assert.Equal([(2, 3), (1, 2)], vm.Senders().Select(s => (s.Filter.DeviceId!.Value, s.Count)));

        vm.ShowDevice(GraylogDeviceFilter.ForDevice(1, "core-sw"));

        await Logs.Received(1).ListEventLogAsync(1, LogsViewModel.PageSize, Arg.Any<CancellationToken>());
        Assert.Equal("core-sw", vm.DeviceChipText);
        Assert.False(vm.ShowsDevice);
        Assert.True(vm.CanClearDevice);

        vm.ClearDeviceCommand.Execute(null);
        Assert.False(vm.HasDeviceFilter);
    }

    [Fact]
    public async Task The_alert_log_names_rules_and_filters_by_state()
    {
        Returns([]);
        Logs.ListAlertLogAsync(null, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
        [
            new AlertLogEntry { Id = 2, RuleId = 4, DeviceId = 2, StateValue = 1, TimeLogged = Noon.AddMinutes(-5) },
            new AlertLogEntry { Id = 1, RuleId = 9, DeviceId = 1, StateValue = 0, TimeLogged = Noon.AddMinutes(-9) },
        ]);
        _client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns([new AlertRule { Id = 4, Name = "Port down" }]);
        _dialogs.ChooseAsync("State", Arg.Any<IReadOnlyList<string>>()).Returns("Recovered");
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();

        vm.ShowAlertLogCommand.Execute(null);
        await vm.EnsureLoadedAsync();

        Assert.Equal(["Port down", "Rule 9"], vm.Entries.Select(e => e.Message));
        Assert.Equal(("edge-rtr", RowStatus.Critical, "Active · alert #2"), (vm.Entries[0].DeviceName, vm.Entries[0].Status, vm.Entries[0].MetaText));

        await vm.ChooseStateCommand.ExecuteAsync(null);

        Assert.Equal(["Rule 9"], vm.Entries.Select(e => e.Message));
        Assert.Equal(RowStatus.Ok, vm.Entries[0].Status);
        Assert.Equal("1 recovered alert in the last 2", vm.SummaryText);
    }

    [Fact]
    public async Task A_devices_event_log_is_this_page_with_the_device_fixed()
    {
        Returns(Events(2));
        _preferences.Set("chips.logs.type", "reboot");
        var vm = NewViewModel();

        vm.Initialise(1, "core-sw");
        await vm.EnsureLoadedAsync();

        await Logs.Received(1).ListEventLogAsync(1, LogsViewModel.PageSize, Arg.Any<CancellationToken>());
        Assert.Equal("Event log", vm.Title);
        Assert.False(vm.ShowsLogSwitch);
        Assert.Null(vm.EventType); // the fleet's type isn't this device's
        Assert.False(vm.CanClearDevice);

        vm.ShowDevice(null);
        vm.ClearFiltersCommand.Execute(null);
        Assert.Equal("core-sw", vm.DeviceChipText);

        await vm.ChooseDeviceCommand.ExecuteAsync(null);
        Assert.Empty(_navigation.Visits);
    }

    [Fact]
    public async Task An_entry_opens_its_page_which_narrows_the_list_to_entries_like_it()
    {
        Returns(Events(2, type: "discovery"));
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();

        await vm.OpenCommand.ExecuteAsync(vm.Entries[0]);
        var visit = Assert.Single(_navigation.Visits);
        Assert.Equal(Routes.LogEntry, visit.Route);

        var entry = new LogEntryViewModel(_navigation);
        entry.Load((LogEntryItem)visit.Parameters![Routes.LogEntryParameter], (LogsViewModel)visit.Parameters[Routes.LogsListParameter]);
        Assert.Equal("Only edge-rtr's events", entry.ShowOnlyDeviceText);
        Assert.Equal("Only discovery events", entry.ShowOnlyKindText);
        Assert.Contains(new KeyValuePair<string, string>("Severity", "Critical"), entry.Entry!.Fields);

        await entry.ShowOnlyKindCommand.ExecuteAsync(null);

        Assert.Equal("discovery", vm.EventType);
        Assert.False(entry.CanShowOnlyKind);
        Assert.Equal(Routes.Back, _navigation.Visits[^1].Route);

        await entry.OpenDeviceCommand.ExecuteAsync(null);
        Assert.Equal(2, _navigation.Visits[^1].Parameters![Routes.DeviceIdParameter]);
    }

    [Fact]
    public async Task An_alert_entry_opens_its_rule()
    {
        Returns([]);
        Logs.ListAlertLogAsync(null, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            [new AlertLogEntry { Id = 7, RuleId = 4, DeviceId = 1, StateValue = 0, TimeLogged = Noon }]);
        var vm = NewViewModel();
        vm.ShowAlertLogCommand.Execute(null);
        await vm.EnsureLoadedAsync();

        var entry = new LogEntryViewModel(_navigation);
        entry.Load(vm.Entries[0], vm);
        await entry.OpenRuleCommand.ExecuteAsync(null);

        Assert.Equal("Only recovered alerts", entry.ShowOnlyKindText);
        Assert.Equal((Routes.AlertRule, (object)4), (_navigation.Visits[^1].Route, _navigation.Visits[^1].Parameters![Routes.RuleIdParameter]));
    }

    [Fact]
    public async Task The_device_chooser_offers_the_devices_in_the_log_and_no_unknown_addresses()
    {
        Returns(Events(3));
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();
        var settings = Fakes.Settings();
        var picker = new GraylogDevicePickerViewModel(_client, settings, new DeviceBookmarks(settings, TimeProvider.System), _navigation)
        {
            SearchDelay = TimeSpan.Zero,
        };

        await picker.LoadAsync(vm);

        Assert.Equal("In these entries", picker.Groups[0].Name);
        Assert.Equal(["edge-rtr", "core-sw"], picker.Groups[0].Select(c => c.Name));

        picker.SearchText = "10.9.9.9";
        Assert.DoesNotContain(picker.Groups, g => g.Name == "Not in LibreNMS");

        picker.SearchText = "core";
        await picker.ChooseCommand.ExecuteAsync(picker.Groups[0][0]);
        Assert.Equal("core-sw", vm.DeviceChipText);
    }
}
