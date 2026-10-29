using System.Text.Json;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

/// <summary>The Event log and Graylog cards (#213), as desktop's widgets (DashyNMS/desktop#202, #203, #304).</summary>
public sealed class LogCardsTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly RecordingNavigation _navigation = new();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IGraylogApi _graylog = Substitute.For<IGraylogApi>();
    private readonly ILibreNmsClient _client = Fakes.Client(devices:
    [
        Fakes.Device(1, "core-sw1.example.net", ip: "192.0.2.1"),
        Fakes.Device(2, "edge-rtr", ip: "192.0.2.2"),
    ]);

    public LogCardsTests()
    {
        _settings = Fakes.Settings(_appSettings);
        _client.Logs.ListEventLogAsync(Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
        [
            new EventLogEntry { Id = 3, DeviceId = 1, Message = "Gi1/0/3 down", Type = "interface", Timestamp = DateTime.Now.AddMinutes(-1) },
            new EventLogEntry { Id = 2, DeviceId = 2, Message = "Rebooted", Type = "reboot", Timestamp = DateTime.Now.AddMinutes(-2) },
            new EventLogEntry { Id = 1, DeviceId = 1, Message = "Gi1/0/1 up", Type = "interface", Timestamp = DateTime.Now.AddMinutes(-3) },
        ]);
        _graylog.GetStreamsAsync(Arg.Any<CancellationToken>()).Returns([new GraylogStream { Id = "s1", Title = "Network" }]);
        _graylog.SearchAsync(default!, default, default, default, default, default, default).ReturnsForAnyArgs(new GraylogSearchResult
        {
            TotalResults = 2,
            Messages =
            [
                Message("m1", "core-sw1", "LINK-3-UPDOWN: Gi1/0/3 down"),
                Message("m2", "192.0.2.99", "sshd: session opened"),
            ],
        });
    }

    private static GraylogMessageEnvelope Message(string id, string source, string text) => new()
    {
        Message = new GraylogMessage(new Dictionary<string, JsonElement>
        {
            ["_id"] = JsonSerializer.SerializeToElement(id),
            ["source"] = JsonSerializer.SerializeToElement(source),
            ["message"] = JsonSerializer.SerializeToElement(text),
            ["level"] = JsonSerializer.SerializeToElement(3),
            ["timestamp"] = JsonSerializer.SerializeToElement("2026-01-01T12:00:00.000Z"),
        }),
    };

    private DashboardViewModel NewViewModel()
    {
        var setup = new GraylogSetup(_graylog, Substitute.For<IGraylogPasswordProtector>(), _settings, Fakes.Secrets(), NullLogger<GraylogSetup>.Instance);
        return new(_client, _settings, _navigation, new DeviceBookmarks(_settings, TimeProvider.System),
            dialogs: _dialogs, graylogSetup: setup, graylog: _graylog);
    }

    private static DashboardCard CardOf(DashboardViewModel vm, string type) => vm.Cards.Single(c => c.Type == type);

    [Fact]
    public void Both_are_in_the_card_picker_under_desktops_Logs()
    {
        Assert.Equal("Logs", DashboardLayout.KindOf(DashboardLayout.EventLog).Category);
        Assert.Equal("Logs", DashboardLayout.KindOf(DashboardLayout.Graylog).Category);
        Assert.True(DashboardLayout.KindOf(DashboardLayout.Graylog).AllowsSeveral);
    }

    [Fact]
    public void Both_can_be_added_ordered_and_removed_and_survive_desktops_settings_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dashynms-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "settings.json");
        try
        {
            var store = new SettingsStore(NullLogger<SettingsStore>.Instance, file);
            store.Load();
            var alerts = DashboardLayout.Add(store.Current, DashboardLayout.Alerts);
            var events = DashboardLayout.Add(store.Current, DashboardLayout.EventLog);
            events.LogType = "interface";
            events.LogCount = 25;
            var graylog = DashboardLayout.Add(store.Current, DashboardLayout.Graylog);
            graylog.GraylogStreamId = "s1";
            graylog.GraylogRangeSeconds = 3600;
            graylog.LogSearch = "level:<=3";

            DashboardLayout.Save(store.Current, [graylog, events]);
            store.Save();

            var reloaded = new SettingsStore(NullLogger<SettingsStore>.Instance, file).Load();
            var shown = DashboardLayout.Current(reloaded);
            Assert.Equal([DashboardLayout.Graylog, DashboardLayout.EventLog], shown.Select(w => w.WidgetType));
            Assert.Equal(("interface", 25), (shown[1].LogType, shown[1].LogCount));
            Assert.Equal(("s1", 3600, "level:<=3"), (shown[0].GraylogStreamId, shown[0].GraylogRangeSeconds, shown[0].LogSearch));
            Assert.DoesNotContain(reloaded.DashboardWidgets, w => w.Id == alerts.Id);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task The_event_log_card_shows_the_newest_events_of_its_type_and_opens_one_with_its_device()
    {
        var widget = DashboardLayout.Add(_appSettings, DashboardLayout.EventLog);
        widget.LogType = "interface";
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        var card = CardOf(vm, DashboardLayout.EventLog);
        Assert.Equal(["Gi1/0/3 down", "Gi1/0/1 up"], card.EventRows.Select(r => r.Message));
        Assert.Equal("core-sw1.example.net", card.EventRows[0].DeviceName);
        Assert.Equal("interface ▾", card.LogTypeText);
        Assert.False(card.HasLogStatus);

        await vm.OpenLogEntryCommand.ExecuteAsync(card.EventRows[0]);

        var visit = _navigation.Visits.Single();
        Assert.Equal(Routes.LogEntry, visit.Route);
        var entry = Assert.IsType<LogEntryItem>(visit.Parameters![Routes.LogEntryParameter]);
        Assert.Equal(1, entry.DeviceId);
    }

    [Fact]
    public async Task Choosing_a_type_saves_it_on_the_widget_and_see_all_opens_Logs_on_it()
    {
        var widget = DashboardLayout.Add(_appSettings, DashboardLayout.EventLog);
        var vm = NewViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);
        var card = CardOf(vm, DashboardLayout.EventLog);
        Assert.Equal(3, card.EventRows.Count);
        _dialogs.ChooseAsync("Event type", Arg.Any<IReadOnlyList<string>>()).Returns("reboot");

        await card.ChooseLogTypeCommand.ExecuteAsync(null);
        await vm.LogsLoaded;

        Assert.Equal("reboot", widget.LogType);
        Assert.Equal(["Rebooted"], card.EventRows.Select(r => r.Message));
        _settings.Received().Save();

        await card.SeeAllLogsCommand.ExecuteAsync(null);
        var visit = _navigation.Visits.Single();
        Assert.Equal((Routes.Logs, "reboot"), (visit.Route, visit.Parameters![Routes.LogTypeParameter]));
    }

    [Fact]
    public async Task The_event_log_cards_fetch_only_while_one_is_showing()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.Alerts);
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        await _client.Logs.DidNotReceiveWithAnyArgs().ListEventLogAsync(default, default, default);
        await _graylog.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, default, default, default, default, default);
    }

    [Fact]
    public async Task A_Graylog_card_without_Graylog_set_up_says_so_and_opens_its_settings()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.Graylog);
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        var card = CardOf(vm, DashboardLayout.Graylog);
        Assert.Equal("Set up Graylog in Settings first.", card.LogStatus);
        Assert.Equal("Set up Graylog", card.SeeAllLogsText);
        await card.SeeAllLogsCommand.ExecuteAsync(null);
        Assert.Equal(Routes.GraylogSettings, _navigation.Visits.Single().Route);
    }

    [Fact]
    public async Task A_Graylog_card_searches_its_stream_and_range_and_opens_a_message_with_its_device()
    {
        _graylog.IsConfigured.Returns(true);
        var widget = DashboardLayout.Add(_appSettings, DashboardLayout.Graylog);
        widget.GraylogStreamId = "s1";
        widget.GraylogRangeSeconds = 3600;
        widget.LogCount = 25;
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        await _graylog.Received(1).SearchAsync("*", 3600, 25, 0, "timestamp:desc", "streams:s1", Arg.Any<CancellationToken>());
        var card = CardOf(vm, DashboardLayout.Graylog);
        Assert.Equal(("Network ▾", "60 min ▾", "Latest 25 ▾"), (card.GraylogStreamText, card.GraylogRangeText, card.LogCountText));
        Assert.Equal(["core-sw1.example.net", "192.0.2.99"], card.GraylogRows.Select(r => r.SourceText));
        Assert.Equal(1, card.GraylogRows[0].DeviceId);

        await vm.OpenGraylogMessageCommand.ExecuteAsync(card.GraylogRows[0]);
        Assert.Equal(Routes.GraylogMessage, _navigation.Visits[0].Route);

        await card.SeeAllLogsCommand.ExecuteAsync(null);
        var seeAll = _navigation.Visits[1];
        Assert.Equal((Routes.Graylog, "s1", 3600), (seeAll.Route, seeAll.Parameters![Routes.GraylogStreamParameter], seeAll.Parameters[Routes.GraylogRangeParameter]));
    }

    [Fact]
    public async Task A_failed_refresh_keeps_the_last_rows_under_the_error()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.EventLog);
        var vm = NewViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);
        _client.Logs.ListEventLogAsync(Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<EventLogEntry>>(_ => throw new LibreNmsApiException("Server error"));

        await vm.RefreshCommand.ExecuteAsync(null);

        var card = CardOf(vm, DashboardLayout.EventLog);
        Assert.Equal(3, card.EventRows.Count);
        Assert.Equal("Couldn't reach LibreNMS for the event log.", card.LogStatus);
    }
}
