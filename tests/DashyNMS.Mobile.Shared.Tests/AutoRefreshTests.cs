using System.Text.Json;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Security;
using DesktopNMS.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

/// <summary>Logs and Graylog refresh themselves while open (#214), as desktop's (DashyNMS/desktop#407).</summary>
public sealed class AutoRefreshTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly IAppActivity _activity = Substitute.For<IAppActivity>();
    private readonly InMemoryPreferences _preferences = new();
    private int _refreshes;

    private AutoRefresh NewAutoRefresh(Func<Task>? refresh = null) =>
        new(refresh ?? (() => { _refreshes++; return Task.CompletedTask; }), _preferences, "test", _clock, _activity) { PostsToCaller = false };

    [Fact]
    public void Off_until_switched_on_then_remembered_with_its_interval()
    {
        var auto = NewAutoRefresh();
        Assert.False(auto.IsOn);
        Assert.Equal("Every 30 seconds", auto.IntervalText);

        auto.IsOn = true;
        auto.Seconds = 60;

        var again = NewAutoRefresh();
        Assert.True(again.IsOn);
        Assert.Equal("Every minute", again.IntervalText);
    }

    [Fact]
    public void Ticks_only_while_the_page_is_showing()
    {
        using var auto = NewAutoRefresh();
        auto.IsOn = true;

        _clock.Advance(TimeSpan.FromSeconds(90));
        Assert.Equal(0, _refreshes);

        auto.SetShowing(true);
        _clock.Advance(TimeSpan.FromSeconds(30));
        _clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(2, _refreshes);

        auto.SetShowing(false);
        Assert.False(auto.IsRunning);
        _clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(2, _refreshes);
    }

    [Fact]
    public void Stops_while_the_app_is_in_the_background()
    {
        using var auto = NewAutoRefresh();
        auto.IsOn = true;
        auto.SetShowing(true);

        _activity.IsInBackground.Returns(true);
        _activity.Changed += Raise.Event();
        _clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(0, _refreshes);

        _activity.IsInBackground.Returns(false);
        _activity.Changed += Raise.Event();
        _clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(1, _refreshes);
    }

    [Fact]
    public void Overlapping_ticks_dont_start_a_second_request()
    {
        var slow = new TaskCompletionSource();
        using var auto = NewAutoRefresh(() => { _refreshes++; return slow.Task; });
        auto.IsOn = true;
        auto.SetShowing(true);

        _clock.Advance(TimeSpan.FromSeconds(30));
        _clock.Advance(TimeSpan.FromSeconds(30));
        _clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(1, _refreshes);

        slow.SetResult();
        _clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(2, _refreshes);
    }

    [Fact]
    public void A_new_interval_takes_effect_at_once()
    {
        using var auto = NewAutoRefresh();
        auto.IsOn = true;
        auto.SetShowing(true);

        auto.Seconds = 10;
        _clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(1, _refreshes);
    }
}

public sealed class LogsAutoRefreshTests
{
    private static readonly DateTime Noon = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Local);

    private readonly ILibreNmsClient _client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")]);

    private LogsViewModel NewViewModel() => new(_client, Fakes.Settings(), new RecordingNavigation(), Substitute.For<IDialogService>())
    {
        SearchDelay = TimeSpan.Zero,
        Now = () => Noon,
    };

    private static EventLogEntry Event(int id, string type = "interface") =>
        new() { Id = id, DeviceId = 1, Message = $"Event {id}", Type = type, Timestamp = Noon.AddMinutes(-(100 - id)) };

    private void Returns(params EventLogEntry[] entries) =>
        _client.Logs.ListEventLogAsync(Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(entries);

    [Theory]
    [InlineData("severity")]
    [InlineData("type")]
    [InlineData("user")]
    [InlineData("time")]
    [InlineData("hostname")]
    [InlineData("sysName")]
    public async Task Same_identity_refresh_updates_metadata_and_exact_detail_facts_without_resetting_other_rows(string field)
    {
        Returns(Event(2), Event(1));
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();
        var old = vm.Entries[0];
        var retained = vm.Entries[1];
        var changed = Event(2);
        switch (field)
        {
            case "severity": changed.Severity = 4; break;
            case "type": changed.Type = "reboot"; break;
            case "user": changed.Username = "operator"; break;
            case "time": changed.Timestamp = Noon.AddMinutes(-98).AddSeconds(12); break;
            case "hostname": changed.Hostname = "device-01.example.net"; break;
            case "sysName": changed.SysName = "device-01"; break;
        }
        Returns(changed, Event(1));
        await vm.AutoRefreshAsync();

        var current = vm.Entries[0];
        Assert.NotSame(old, current);
        Assert.Same(retained, vm.Entries[1]);
        Assert.Equal(old.Id, current.Id);
        var expected = LogEntryItem.ForEvent(changed, current.DeviceName, false, Noon);
        Assert.Equal(expected.StatusText, current.StatusText);
        Assert.Equal(expected.MetaText, current.MetaText);
        Assert.Equal(expected.TimeText, current.TimeText);
        Assert.Equal(expected.Fields, current.Fields);
        Assert.Equal(30, vm.AutoRefresh.Seconds);
        Assert.False(vm.AutoRefresh.IsOn);

        changed.Message = "<literal>\nsecond line";
        Returns(changed, Event(1));
        await vm.AutoRefreshAsync();
        Assert.Equal("<literal>\nsecond line", vm.Entries[0].FullText);
        Assert.Same(retained, vm.Entries[1]);
    }

    [Fact]
    public async Task Alert_refresh_replaces_changed_state_and_seconds_for_the_same_id()
    {
        _client.Logs.ListAlertLogAsync(null, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            [new AlertLogEntry { Id = 7, RuleId = 4, DeviceId = 1, StateValue = 0, TimeLogged = Noon }]);
        var vm = NewViewModel();
        vm.ShowAlertLogCommand.Execute(null);
        await vm.EnsureLoadedAsync();
        var old = Assert.Single(vm.Entries);
        _client.Logs.ListAlertLogAsync(null, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            [new AlertLogEntry { Id = 7, RuleId = 4, DeviceId = 1, StateValue = 1, TimeLogged = Noon.AddSeconds(12) }]);

        await vm.AutoRefreshAsync();

        var current = Assert.Single(vm.Entries);
        Assert.NotSame(old, current);
        Assert.Equal(old.Id, current.Id);
        Assert.NotEqual(old.StatusText, current.StatusText);
        Assert.Equal(Noon.AddSeconds(12), current.LocalTime);
        Assert.Equal(4, current.RuleId);
    }

    [Fact]
    public async Task New_entries_come_in_at_the_top_and_the_rows_below_stay()
    {
        Returns(Event(2), Event(1));
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();
        var kept = vm.Entries[0];
        var changes = 0;
        vm.Entries.CollectionChanged += (_, e) =>
        {
            changes++;
            Assert.NotEqual(System.Collections.Specialized.NotifyCollectionChangedAction.Reset, e.Action);
        };

        Returns(Event(3), Event(2), Event(1));
        await vm.AutoRefreshAsync();

        Assert.Equal(["Event 3", "Event 2", "Event 1"], vm.Entries.Select(e => e.Message));
        Assert.Same(kept, vm.Entries[1]);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task A_failed_refresh_keeps_the_rows_and_filters_and_says_so_quietly()
    {
        Returns(Event(3), Event(2, "reboot"), Event(1));
        var vm = NewViewModel();
        vm.EventType = "interface";
        await vm.EnsureLoadedAsync();
        _client.Logs.ListEventLogAsync(Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<EventLogEntry>>(_ => throw new LibreNmsApiException("Server error"));

        await vm.AutoRefreshAsync();

        Assert.Equal(["Event 3", "Event 1"], vm.Entries.Select(e => e.Message));
        Assert.Equal("interface", vm.EventType);
        Assert.False(vm.HasError);
        Assert.Equal("Couldn't refresh at 12:00", vm.RefreshProblem);

        Returns(Event(4), Event(3), Event(1));
        await vm.AutoRefreshAsync();
        Assert.False(vm.HasRefreshProblem);
    }

    [Fact]
    public async Task Nothing_is_fetched_before_the_first_load()
    {
        var vm = NewViewModel();

        await vm.AutoRefreshAsync();

        await _client.Logs.DidNotReceiveWithAnyArgs().ListEventLogAsync(default, default, default);
    }
}

public sealed class GraylogAutoRefreshTests
{
    private readonly IGraylogApi _api = Substitute.For<IGraylogApi>();
    private readonly ILibreNmsClient _client = Fakes.Client(devices: [Fakes.Device(1, "core-sw", ip: "192.0.2.1")]);
    private readonly AppSettings _appSettings = new();

    public GraylogAutoRefreshTests()
    {
        _api.IsConfigured.Returns(true);
        _api.GetStreamsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<GraylogStream>());
    }

    private static GraylogSearchResult Result(params string[] ids) => new()
    {
        TotalResults = ids.Length,
        Messages = ids.Select(id => new GraylogMessageEnvelope
        {
            Message = new GraylogMessage(new Dictionary<string, JsonElement>
            {
                ["_id"] = JsonSerializer.SerializeToElement(id),
                ["source"] = JsonSerializer.SerializeToElement("192.0.2.1"),
                ["message"] = JsonSerializer.SerializeToElement("Message " + id),
                ["timestamp"] = JsonSerializer.SerializeToElement("2026-01-01T12:00:00.000Z"),
            }),
        }).ToList(),
    };

    private void Returns(GraylogSearchResult result) =>
        _api.SearchAsync(default!, default, default, default, default, default, default).ReturnsForAnyArgs(result);

    private GraylogViewModel NewViewModel()
    {
        var settings = Fakes.Settings(_appSettings);
        var setup = new GraylogSetup(_api, Substitute.For<IGraylogPasswordProtector>(), settings, Fakes.Secrets(), NullLogger<GraylogSetup>.Instance);
        var vm = new GraylogViewModel(setup, _api, _client, settings, new RecordingNavigation(), Substitute.For<IDialogService>())
        {
            ResolveHostname = (_, _) => Task.FromResult<string?>(null),
        };
        vm.Initialise(null, null);
        return vm;
    }

    [Fact]
    public async Task Newer_messages_come_in_above_those_shown_with_the_same_search()
    {
        Returns(Result("b", "a"));
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();
        var kept = vm.Messages[0];
        _api.ClearReceivedCalls();

        Returns(Result("c", "b", "a"));
        await vm.AutoRefreshAsync();

        Assert.Equal(["c", "b", "a"], vm.Messages.Select(m => m.Id));
        Assert.Same(kept, vm.Messages[1]);
        await _api.Received(1).SearchAsync(Arg.Is("*"), Arg.Any<int>(), Arg.Is(GraylogViewModel.PageSize), Arg.Is(0), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failed_refresh_keeps_the_messages_and_says_so_quietly()
    {
        Returns(Result("b", "a"));
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();
        _api.SearchAsync(default!, default, default, default, default, default, default)
            .ReturnsForAnyArgs<GraylogSearchResult>(_ => throw new GraylogApiException("Unreachable"));

        await vm.AutoRefreshAsync();

        Assert.Equal(["b", "a"], vm.Messages.Select(m => m.Id));
        Assert.False(vm.HasError);
        Assert.True(vm.HasRefreshProblem);
    }
}
