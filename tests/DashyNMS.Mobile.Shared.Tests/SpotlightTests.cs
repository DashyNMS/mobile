using System.Text.Json;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Security;
using DesktopNMS.Core.Spotlights;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

/// <summary>Log cards kept to a Spotlight (#270, desktop#552): devices, a group or a location.</summary>
public sealed class SpotlightTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly RecordingNavigation _navigation = new();
    private readonly IGraylogApi _graylog = Substitute.For<IGraylogApi>();
    private readonly ILibreNmsClient _client = Fakes.Client(devices:
    [
        Fakes.Device(1, "core-sw1.example.net", ip: "192.0.2.1"),
        Fakes.Device(2, "edge-rtr", ip: "192.0.2.2"),
        Fakes.Device(3, "lab-sw", ip: "192.0.2.3"),
    ]);

    private static readonly DateTime Now = DateTime.Now;

    public SpotlightTests()
    {
        _settings = Fakes.Settings(_appSettings);
        _client.Logs.ListEventLogAsync(null, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
        [
            new EventLogEntry { Id = 3, DeviceId = 3, Message = "Lab rebooted", Timestamp = Now.AddMinutes(-1) },
            new EventLogEntry { Id = 2, DeviceId = 2, Message = "Edge port down", Timestamp = Now.AddMinutes(-2) },
            new EventLogEntry { Id = 1, DeviceId = 1, Message = "Core port up", Timestamp = Now.AddMinutes(-3) },
        ]);
        _client.Logs.ListEventLogAsync(2, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            [new EventLogEntry { Id = 2, DeviceId = 2, Message = "Edge port down", Timestamp = Now.AddMinutes(-2) }]);
        _client.Logs.ListEventLogAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            [new EventLogEntry { Id = 1, DeviceId = 1, Message = "Core port up", Timestamp = Now.AddMinutes(-3) }]);
        _client.DeviceGroups.ListAsync(Arg.Any<CancellationToken>()).Returns([new DeviceGroup { Id = 7, Name = "Core routers", Description = "The backbone" }]);
        _client.DeviceGroups.GetMemberDeviceIdsAsync(7, Arg.Any<CancellationToken>()).Returns([1, 2]);
        _client.Locations.ListAsync(Arg.Any<CancellationToken>()).Returns([new Location { Id = 4, Name = "Comms room" }]);
        _graylog.IsConfigured.Returns(true);
        _graylog.SearchAsync(default!, default, default, default, default, default, default)
            .ReturnsForAnyArgs(new GraylogSearchResult { Messages = [] });
    }

    private DashboardViewModel NewViewModel()
    {
        var setup = new GraylogSetup(_graylog, Substitute.For<IGraylogPasswordProtector>(), _settings, Fakes.Secrets(), NullLogger<GraylogSetup>.Instance);
        return new(_client, _settings, _navigation, new DeviceBookmarks(_settings, TimeProvider.System),
            dialogs: Substitute.For<IDialogService>(), graylogSetup: setup, graylog: _graylog);
    }

    private static DashboardCard CardOf(DashboardViewModel vm, string type) => vm.Cards.Single(c => c.Type == type);

    [Fact]
    public async Task An_old_layout_with_no_Spotlight_is_the_whole_network()
    {
        // As saved before Spotlights: no "Spotlight" at all.
        var json = JsonSerializer.Serialize(DashboardLayout.New(DashboardLayout.EventLog)).Replace(",\"Spotlight\":[]", string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("Spotlight", json, StringComparison.Ordinal);
        var widget = JsonSerializer.Deserialize<DashboardWidget>(json)!;
        widget.Normalise();
        _appSettings.DashboardWidgets.Add(widget);
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        var card = CardOf(vm, DashboardLayout.EventLog);
        Assert.Empty(widget.Spotlight);
        Assert.Equal("Whole network ▾", card.SpotlightText);
        Assert.Equal(3, card.EventRows.Count);
    }

    [Fact]
    public async Task A_device_Spotlight_reads_that_devices_own_log()
    {
        var widget = DashboardLayout.Add(_appSettings, DashboardLayout.EventLog);
        widget.Spotlight = [SpotlightTarget.Device(2, "edge-rtr")];
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        var card = CardOf(vm, DashboardLayout.EventLog);
        Assert.Equal(["Edge port down"], card.EventRows.Select(r => r.Message));
        Assert.Equal("edge-rtr ▾", card.SpotlightText);
        await _client.Logs.Received().ListEventLogAsync(2, Arg.Any<int>(), Arg.Any<CancellationToken>());

        // See all: Logs on that device.
        await card.SeeAllLogsCommand.ExecuteAsync(null);
        Assert.Equal(2, _navigation.Visits.Single().Parameters![Routes.DeviceIdParameter]);
    }

    [Fact]
    public async Task A_group_Spotlight_shows_its_members_and_says_so_when_theres_nothing()
    {
        var widget = DashboardLayout.Add(_appSettings, DashboardLayout.EventLog);
        widget.Spotlight = [SpotlightTarget.Group(7, "Core")];
        widget.LogType = "reboot";
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        var card = CardOf(vm, DashboardLayout.EventLog);
        Assert.Equal("Core routers ▾", card.SpotlightText); // the group's current name
        Assert.Equal("No events for Core routers.", card.LogStatus);
    }

    [Fact]
    public async Task A_removed_group_stays_saved_and_shows_as_unknown()
    {
        var widget = DashboardLayout.Add(_appSettings, DashboardLayout.EventLog);
        widget.Spotlight = [SpotlightTarget.Group(9, "Old group")];
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("Unknown group (Old group) ▾", CardOf(vm, DashboardLayout.EventLog).SpotlightText);
        Assert.Single(widget.Spotlight);
    }

    [Fact]
    public async Task A_Graylog_card_searches_only_its_Spotlights_devices()
    {
        var widget = DashboardLayout.Add(_appSettings, DashboardLayout.Graylog);
        widget.Spotlight = [SpotlightTarget.Location(4, "Comms room")];
        _client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Device { DeviceId = 1, Hostname = "core-sw1.example.net", Ip = "192.0.2.1", LocationId = 4, Status = true },
            new Device { DeviceId = 2, Hostname = "edge-rtr", Ip = "192.0.2.2", Status = true },
        ]);
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        await _graylog.Received().SearchAsync(
            Arg.Is<string>(q => q.Contains("core-sw1.example.net", StringComparison.Ordinal) && !q.Contains("edge-rtr", StringComparison.Ordinal)),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Is(0), Arg.Is("timestamp:desc"), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        Assert.Equal("No messages from Comms room in the last 15 min.", CardOf(vm, DashboardLayout.Graylog).LogStatus);
    }

    [Fact]
    public async Task The_picker_ticks_several_devices_or_one_group_and_saves_on_the_widget()
    {
        var widget = DashboardLayout.Add(_appSettings, DashboardLayout.EventLog);
        widget.Spotlight = [SpotlightTarget.Device(1, "core-sw1.example.net"), SpotlightTarget.Device(99, "gone-sw")];
        var vm = new SpotlightPickerViewModel(_client, _settings, _navigation) { SearchDelay = TimeSpan.Zero };

        await vm.LoadAsync(widget.Id);

        Assert.True(vm.IsDevices);
        Assert.Equal("Unknown device (gone-sw)", vm.Choices[0].Name); // kept, at the top, until unticked
        Assert.True(vm.Choices[0].IsSelected);
        vm.PickCommand.Execute(vm.Choices[0]);
        vm.PickCommand.Execute(vm.Choices.Single(c => c.Target.Id == 2));
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal([1, 2], widget.Spotlight.Select(t => t.Id).Order());

        // A group is one choice, and switching kind starts afresh.
        vm.ChooseKindCommand.Execute("1");
        Assert.Equal(["Core routers"], vm.Choices.Select(c => c.Name));
        Assert.Equal("The backbone", vm.Choices[0].Detail);
        vm.PickCommand.Execute(vm.Choices[0]);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(SpotlightKind.Group, widget.Spotlight.Single().Kind);

        await vm.WholeNetworkCommand.ExecuteAsync(null);
        Assert.Empty(widget.Spotlight);
    }

    [Fact]
    public async Task The_picker_searches_by_name()
    {
        var widget = DashboardLayout.Add(_appSettings, DashboardLayout.EventLog);
        var vm = new SpotlightPickerViewModel(_client, _settings, _navigation) { SearchDelay = TimeSpan.Zero };
        await vm.LoadAsync(widget.Id);

        vm.SearchText = "edge";

        Assert.Equal(["edge-rtr"], vm.Choices.Select(c => c.Name));
        vm.SearchText = "nothing";
        Assert.True(vm.IsEmpty);
        Assert.Equal("Nothing matches.", vm.EmptyText);
    }
}
