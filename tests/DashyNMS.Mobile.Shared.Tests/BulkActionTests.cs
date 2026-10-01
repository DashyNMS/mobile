using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class BulkRunTests
{
    [Fact]
    public async Task Carries_on_past_a_failure_and_says_which()
    {
        var progress = new List<int>();

        var result = await BulkRun.RunAsync(
            [1, 2, 3, 4],
            n => $"item {n}",
            n => n == 3 ? Task.FromException(new LibreNmsApiException("Server error", System.Net.HttpStatusCode.InternalServerError)) : Task.CompletedTask,
            progress.Add);

        Assert.Equal([1, 2, 4], result.Succeeded);
        Assert.Equal([1, 2, 3, 4], progress);
        Assert.Equal("Acknowledged 3 of 4 alerts. 1 failed: item 3 (HTTP 500).", result.Describe("Acknowledged", "alert"));
    }

    [Fact]
    public void All_done_reads_simply_and_one_is_singular()
    {
        Assert.Equal("Pinned 5 devices.", new BulkResult<int>([1, 2, 3, 4, 5], []).Describe("Pinned", "device"));
        Assert.Equal("Pinned 1 device.", new BulkResult<int>([1], []).Describe("Pinned", "device"));
    }

    [Fact]
    public void Lists_three_failures_then_counts_the_rest()
    {
        var failures = Enumerable.Range(1, 5).Select(i => new BulkFailure($"sw{i}", "timed out")).ToList();

        var text = new BulkResult<int>([], failures).Describe("Rediscovery requested for", "device");

        Assert.Equal("Rediscovery requested for 0 of 5 devices. 5 failed: sw1 (timed out), sw2 (timed out), sw3 (timed out) and 2 more.", text);
    }
}

public sealed class BulkAlertTests
{
    private readonly ILibreNmsClient _client = Fakes.Client(alerts:
    [
        Fakes.Alert(1, 1, "warning"),
        Fakes.Alert(2, 1, "critical", acknowledged: true),
        Fakes.Alert(3, 2, "critical"),
    ]);

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly ISelfActionTracker _selfActions = Substitute.For<ISelfActionTracker>();
    private readonly RecordingNavigation _navigation = new();

    private async Task<AlertsViewModel> Loaded()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var vm = new AlertsViewModel(_client, Fakes.Settings(), _dialogs, _navigation, _selfActions, Substitute.For<IShareService>(), new RecordingBadge(), time);
        await vm.RefreshCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Selecting_turns_a_tap_into_a_tick()
    {
        var vm = await Loaded();

        vm.StartSelectingCommand.Execute(null);
        await vm.OpenAlertCommand.ExecuteAsync(vm.Alerts.Single(a => a.Id == 3));

        Assert.Empty(_navigation.Visits);
        Assert.True(vm.Alerts.Single(a => a.Id == 3).IsTicked);
        Assert.Equal("1 selected", vm.Selection.Summary);

        vm.StopSelectingCommand.Execute(null);
        Assert.DoesNotContain(vm.Alerts, a => a.IsTicked);
        Assert.False(vm.Selection.IsSelecting);
    }

    [Fact]
    public async Task Press_and_hold_starts_selecting_with_that_row_ticked()
    {
        var vm = await Loaded();

        vm.HoldCommand.Execute(vm.Alerts.Single(a => a.Id == 1));

        Assert.True(vm.Selection.IsSelecting);
        Assert.Equal([1], vm.Alerts.Where(a => a.IsTicked).Select(a => a.Id));
        Assert.Empty(_navigation.Visits);

        // Already selecting: holding another ticks it, as a tap would.
        vm.HoldCommand.Execute(vm.Alerts.Single(a => a.Id == 3));
        Assert.Equal([1, 3], vm.Alerts.Where(a => a.IsTicked).Select(a => a.Id).Order());
    }

    [Fact]
    public async Task Select_all_ticks_what_the_filters_show_then_none()
    {
        var vm = await Loaded();
        vm.ShowAcknowledged = false;
        vm.StartSelectingCommand.Execute(null);

        vm.SelectAllCommand.Execute(null);
        Assert.Equal([1, 3], vm.Alerts.Where(a => a.IsTicked).Select(a => a.Id).Order());
        Assert.Equal("Select none", vm.SelectAllText);

        vm.SelectAllCommand.Execute(null);
        Assert.Equal(0, vm.Selection.Count);
        Assert.Equal("Select all", vm.SelectAllText);
    }

    [Fact]
    public async Task Acknowledges_the_active_ones_ticked_with_one_note_in_place()
    {
        _dialogs.PromptAsync(default!, default!, default!, default!).ReturnsForAnyArgs(" change window ");
        var vm = await Loaded();
        vm.StartSelectingCommand.Execute(null);
        vm.SelectAllCommand.Execute(null); // 1, 3 active; 2 already acknowledged

        await vm.AcknowledgeSelectedCommand.ExecuteAsync(null);

        await _dialogs.Received(1).PromptAsync("Acknowledge alerts", "2 alerts. Add a note (optional):", "Acknowledge", "Note");
        await _client.Alerts.Received(1).AcknowledgeAsync(1, "change window", Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await _client.Alerts.Received(1).AcknowledgeAsync(3, "change window", Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await _client.Alerts.DidNotReceive().AcknowledgeAsync(2, Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        _selfActions.Received(1).Record(1, AlertChangeKind.Acknowledged);
        _selfActions.Received(1).Record(3, AlertChangeKind.Acknowledged);

        Assert.All(vm.Alerts, a => Assert.True(a.IsAcknowledged));
        Assert.Equal("change window", vm.Alerts.Single(a => a.Id == 1).Note);
        Assert.Equal("Acknowledged 2 alerts.", vm.Selection.ResultText);
        Assert.False(vm.Selection.IsSelecting); // all done
        await _client.Alerts.Received(1).ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()); // no reload (#93)
    }

    [Fact]
    public async Task What_failed_stays_ticked_to_try_again()
    {
        _dialogs.PromptAsync(default!, default!, default!, default!).ReturnsForAnyArgs(string.Empty);
        _client.Alerts.AcknowledgeAsync(3, Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new LibreNmsApiException("Not found", System.Net.HttpStatusCode.NotFound));
        var vm = await Loaded();
        vm.StartSelectingCommand.Execute(null);
        vm.SelectAllCommand.Execute(null);

        await vm.AcknowledgeSelectedCommand.ExecuteAsync(null);

        Assert.Equal("Acknowledged 1 of 2 alerts. 1 failed: Rule 3 on host2 (HTTP 404).", vm.Selection.ResultText);
        Assert.True(vm.Selection.IsSelecting);
        Assert.Equal([3], vm.Alerts.Where(a => a.IsTicked).Select(a => a.Id));
        Assert.False(vm.Alerts.Single(a => a.Id == 3).IsAcknowledged);
    }

    [Fact]
    public async Task Cancelling_the_note_does_nothing()
    {
        _dialogs.PromptAsync(default!, default!, default!, default!).ReturnsForAnyArgs((string?)null);
        var vm = await Loaded();
        vm.StartSelectingCommand.Execute(null);
        vm.SelectAllCommand.Execute(null);

        await vm.AcknowledgeSelectedCommand.ExecuteAsync(null);

        await _client.Alerts.DidNotReceiveWithAnyArgs().AcknowledgeAsync(default, default!, default, default);
        Assert.Equal(3, vm.Selection.Count);
    }

    [Fact]
    public async Task Unacknowledges_the_acknowledged_ones_ticked_after_asking_once()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var vm = await Loaded();
        vm.StartSelectingCommand.Execute(null);
        vm.SelectAllCommand.Execute(null);

        await vm.UnacknowledgeSelectedCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ConfirmAsync("Unacknowledge alerts", "Put 1 alert back to active?", "Unacknowledge", "Cancel");
        await _client.Alerts.Received(1).UnmuteAsync(2, Arg.Any<string?>(), Arg.Any<CancellationToken>());
        Assert.False(vm.Alerts.Single(a => a.Id == 2).IsAcknowledged);
        Assert.Equal("Unacknowledged 1 alert.", vm.Selection.ResultText);
    }

    [Fact]
    public async Task Nothing_to_acknowledge_says_so()
    {
        var vm = await Loaded();
        vm.StartSelectingCommand.Execute(null);
        await vm.OpenAlertCommand.ExecuteAsync(vm.Alerts.Single(a => a.Id == 2)); // already acknowledged

        await vm.AcknowledgeSelectedCommand.ExecuteAsync(null);

        await _dialogs.DidNotReceiveWithAnyArgs().PromptAsync(default!, default!, default!, default!);
        Assert.Contains("nothing to acknowledge", vm.Selection.ResultText);
    }
}

public sealed class BulkDeviceTests
{
    private readonly ILibreNmsClient _client = Fakes.Client(devices:
    [
        Fakes.Device(1, "core-sw"),
        Fakes.Device(2, "access-sw"),
        Fakes.Device(3, "edge-rtr"),
    ]);

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly RecordingNavigation _navigation = new();
    private readonly DeviceBookmarks _bookmarks;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly ISettingsStore _settings = Fakes.Settings();

    public BulkDeviceTests() => _bookmarks = new DeviceBookmarks(_settings, _time);

    private async Task<DevicesViewModel> Loaded()
    {
        var vm = new DevicesViewModel(_client, _navigation, _settings, _bookmarks, _time, dialogs: _dialogs);
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;
        vm.StartSelectingCommand.Execute(null);
        return vm;
    }

    private static async Task Tick(DevicesViewModel vm, params int[] ids)
    {
        foreach (var id in ids)
        {
            await vm.OpenDeviceCommand.ExecuteAsync(vm.Devices.Single(d => d.DeviceId == id));
        }
    }

    [Fact]
    public async Task Pins_the_ticked_devices_together()
    {
        var vm = await Loaded();
        await Tick(vm, 1, 3);

        vm.PinSelectedCommand.Execute(null);

        Assert.Equal(new HashSet<int> { 1, 3 }, _bookmarks.PinnedIds);
        Assert.Equal("Pinned 2 devices.", vm.Selection.ResultText);
        Assert.False(vm.Selection.IsSelecting);
        Assert.Empty(_navigation.Visits); // ticking never opened them
    }

    [Fact]
    public async Task Rediscovers_each_ticked_device_after_asking_once()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var vm = await Loaded();
        await Tick(vm, 2, 3);

        await vm.RediscoverSelectedCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ConfirmAsync("Rediscover devices", "Ask LibreNMS to rediscover 2 devices?", "Rediscover", "Cancel");
        await _client.Devices.Received(1).DiscoverAsync(2, Arg.Any<CancellationToken>());
        await _client.Devices.Received(1).DiscoverAsync(3, Arg.Any<CancellationToken>());
        Assert.Equal("Rediscovery requested for 2 devices.", vm.Selection.ResultText);
    }

    [Fact]
    public async Task Maintenance_opens_one_form_for_them_all()
    {
        var vm = await Loaded();
        await Tick(vm, 1, 2);

        await vm.ScheduleMaintenanceSelectedCommand.ExecuteAsync(null);

        var visit = Assert.Single(_navigation.Visits);
        Assert.Equal(Routes.Maintenance, visit.Route);
        var devices = Assert.IsAssignableFrom<IReadOnlyList<(int Id, string Name)>>(visit.Parameters![Routes.DevicesParameter]);
        Assert.Equal([1, 2], devices.Select(d => d.Id).Order());
    }
}

public sealed class BulkMaintenanceTests
{
    private readonly ILibreNmsClient _client = Fakes.Client();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly RecordingNavigation _navigation = new();

    [Fact]
    public async Task One_window_goes_to_every_device_with_one_summary()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _client.Devices.ScheduleMaintenanceAsync(2, Arg.Any<DeviceMaintenanceRequest>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new LibreNmsApiException("Forbidden", System.Net.HttpStatusCode.Forbidden));
        var vm = new MaintenanceViewModel(_client, _dialogs, _navigation, time);
        vm.Initialize([(1, "core-sw"), (2, "access-sw"), (3, "edge-rtr")]);

        Assert.Equal("3 devices", vm.DeviceName);

        await vm.SaveCommand.ExecuteAsync(null);

        await _client.Devices.Received(1).ScheduleMaintenanceAsync(1, Arg.Any<DeviceMaintenanceRequest>(), Arg.Any<CancellationToken>());
        await _client.Devices.Received(1).ScheduleMaintenanceAsync(3, Arg.Any<DeviceMaintenanceRequest>(), Arg.Any<CancellationToken>());
        await _dialogs.Received(1).AlertAsync("Maintenance scheduled", "Scheduled maintenance for 2 of 3 devices. 1 failed: access-sw (HTTP 403).");
        Assert.Equal(Routes.Back, _navigation.Visits.Single().Route);
    }
}
