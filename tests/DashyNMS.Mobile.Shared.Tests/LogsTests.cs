using System.Text.Json;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Json;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class NetworkLogsTests
{
    [Fact]
    public async Task Asks_librenms_for_every_devices_log_newest_first()
    {
        var transport = Substitute.For<ILibreNmsTransport>();
        var logs = new NetworkLogs(transport);

        await logs.EventLogAsync(100);
        await logs.AlertLogAsync(0);

        await transport.Received(1).GetCollectionAsync<EventLogEntry>("logs/eventlog?limit=100&sortorder=DESC", "logs", Arg.Any<CancellationToken>());
        await transport.Received(1).GetCollectionAsync<AlertLogEntry>("logs/alertlog?limit=1&sortorder=DESC", "logs", Arg.Any<CancellationToken>());
    }
}

public sealed class LogsViewModelTests
{
    private readonly INetworkLogs _logs = Substitute.For<INetworkLogs>();
    private readonly ILibreNmsClient _client = Fakes.Client(devices: [Fakes.Device(1, "core-sw"), Fakes.Device(2, "edge-rtr")]);
    private readonly RecordingNavigation _navigation = new();

    private LogsViewModel NewViewModel() => new(_logs, _client, Fakes.Settings(), _navigation);

    private static IReadOnlyList<EventLogEntry> Events(int count) => Enumerable.Range(1, count)
        .Select(i => new EventLogEntry { Id = i, DeviceId = 1 + i % 2, Message = $"Event {i}", Type = "interface", Severity = i == 1 ? 5 : 2 })
        .ToList();

    [Fact]
    public async Task Shows_every_devices_events_with_the_device_named()
    {
        _logs.EventLogAsync(LogsViewModel.PageSize, Arg.Any<CancellationToken>()).Returns(Events(3));
        var vm = NewViewModel();

        await vm.EnsureLoadedAsync();

        Assert.Equal(["Event 1", "Event 2", "Event 3"], vm.Entries.Select(e => e.Title));
        Assert.Equal("edge-rtr · interface", vm.Entries[0].Subtitle);
        Assert.Equal(RowStatus.Critical, vm.Entries[0].Status);
        Assert.False(vm.CanLoadMore); // fewer than a page: that's all there is
    }

    [Fact]
    public async Task Load_more_asks_for_a_bigger_page()
    {
        _logs.EventLogAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ci => Events((int)ci[0]));
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();
        Assert.True(vm.CanLoadMore);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(2 * LogsViewModel.PageSize, vm.Entries.Count);
        await _logs.Received(1).EventLogAsync(2 * LogsViewModel.PageSize, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_alert_log_names_rules_and_shows_each_change()
    {
        _logs.EventLogAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        _logs.AlertLogAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
        [
            JsonSerializer.Deserialize<AlertLogEntry>("""{ "id": 1, "rule_id": 4, "device_id": 2, "state": 1 }""", LibreNmsJson.Options)!,
            JsonSerializer.Deserialize<AlertLogEntry>("""{ "id": 2, "rule_id": 9, "device_id": 1, "state": 0 }""", LibreNmsJson.Options)!,
        ]);
        _client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns([new AlertRule { Id = 4, Name = "Port down" }]);
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();

        vm.ShowAlertLogCommand.Execute(null);
        await vm.EnsureLoadedAsync();

        Assert.Equal(["Port down", "Rule 9"], vm.Entries.Select(e => e.Title));
        Assert.Equal(("edge-rtr", RowStatus.Critical), (vm.Entries[0].Subtitle, vm.Entries[0].Status));
        Assert.Equal(RowStatus.Ok, vm.Entries[1].Status);
    }

    [Fact]
    public async Task Search_covers_whats_loaded_and_a_tap_opens_the_device()
    {
        _logs.EventLogAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Events(3));
        var vm = NewViewModel();
        await vm.EnsureLoadedAsync();

        vm.SearchText = "core-sw";
        Assert.Equal(["Event 2"], vm.Entries.Select(e => e.Title));

        await vm.OpenCommand.ExecuteAsync(vm.Entries[0]);
        Assert.Equal(1, Assert.Single(_navigation.Visits).Parameters![Routes.DeviceIdParameter]);
    }
}
