using AlertWatchState = DashyNMS.Mobile.Alerts.AlertWatchState;
using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Widgets;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

internal sealed class InMemoryWatchStore : IAlertWatchStore
{
    public AlertWatchState? State { get; set; }

    public int Clears { get; private set; }

    public AlertWatchState? Load() => State;

    public void Save(AlertWatchState state) => State = state;

    public void Clear()
    {
        Clears++;
        State = null;
    }
}

public sealed class AlertWatcherTests
{
    private const string Server = "https://nms.example.com/";

    private readonly ILibreNmsClient _client = Fakes.Client();
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly AppSettings _settings = new();
    private readonly InMemoryWatchStore _store = new();
    private readonly SelfActionTracker _selfActions = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly RecordingBadge _badge = new();
    private readonly RecordingWidgets _widgets = new() { IsInUse = false };
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly AlertWatcher _watcher;

    public AlertWatcherTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _session.IsConnected.Returns(true);
        _session.Connection.Returns(new LibreNmsConnection(new Uri(Server), "token"));
        _watcher = new AlertWatcher(
            _client, _session, Fakes.Secrets(), Fakes.Settings(_settings), _store, _selfActions, _notifier, _badge, _widgets, _time, NullLogger<AlertWatcher>.Instance);
    }

    private void ServerReturns(params Alert[] alerts) =>
        _client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()).Returns(alerts);

    private void BaselineOf(params Alert[] alerts) =>
        _store.State = new AlertWatchState(Server, Fakes.Watch(alerts));

    private static Alert Fault(int id, bool acknowledged = false, bool recovered = false)
    {
        var alert = Fakes.Alert(id, 7, "critical", acknowledged);
        alert.Source = AlertSource.Fault;
        alert.RuleId = 10;
        alert.RuleName = "Processor load";
        alert.EntityType = "processor";
        alert.EntityId = id;
        alert.EntityKey = $"processor:{id}";
        if (recovered) alert.StateValue = 0;
        return alert;
    }

    [Fact]
    public async Task Eight_new_faults_notify_once_and_partial_recovery_updates_silently()
    {
        _client.Alerts.GetSourceAsync(Arg.Any<CancellationToken>()).Returns(AlertSource.Fault);
        _store.State = new AlertWatchState(Server, new DesktopNMS.Core.Alerting.AlertWatchState(AlertSource.Fault, new Dictionary<AlertKey, AlertWatchEntry>()));
        ServerReturns(Enumerable.Range(1, 8).Select(id => Fault(id)).ToArray());
        await _watcher.CheckAsync();
        Assert.Single(_notifier.Shown);
        Assert.Equal("device:7/rule:10", _notifier.Shown[0].Tag);
        Assert.Contains("more", _notifier.Shown[0].Detail);

        ServerReturns(Enumerable.Range(2, 7).Select(id => Fault(id)).ToArray());
        _client.Alerts.GetAsync(1, Arg.Any<CancellationToken>()).Returns(Fault(1, recovered: true));
        await _watcher.CheckAsync();
        Assert.Equal(2, _notifier.Shown.Count);
        Assert.True(_notifier.Shown[1].IsUpdate);
        Assert.Contains("7 open", _notifier.Shown[1].Detail);
        Assert.Equal(_notifier.Shown[0].Tag, _notifier.Shown[1].Tag);
    }

    [Fact]
    public async Task A_denied_missing_fault_prevents_false_group_recovery_then_recovery_fires_once()
    {
        _client.Alerts.GetSourceAsync(Arg.Any<CancellationToken>()).Returns(AlertSource.Fault);
        BaselineOf(Fault(1), Fault(2));
        ServerReturns(Fault(1, recovered: true));
        _client.Alerts.GetAsync(2, Arg.Any<CancellationToken>()).Returns<Alert?>(_ => throw new LibreNmsApiException("Denied", System.Net.HttpStatusCode.Forbidden));
        await _watcher.CheckAsync();
        Assert.DoesNotContain(_notifier.Shown, n => n.Title.StartsWith("Recovered"));
        Assert.True(_store.State!.Snapshot.Entries[new AlertKey(AlertSource.Fault, 2)].IsOutstanding);
        _client.Alerts.GetAsync(2, Arg.Any<CancellationToken>()).Returns(Fault(2, recovered: true));
        await _watcher.CheckAsync();
        await _watcher.CheckAsync();
        Assert.Single(_notifier.Shown, n => n.Title.StartsWith("Recovered"));
    }

    [Fact]
    public async Task Recovery_lookups_run_even_when_display_and_recovery_notifications_exclude_recovered()
    {
        _settings.Notifications.NotifyOnRecovery = false;
        _settings.IncludeRecoveredAlerts = false;
        _client.Alerts.GetSourceAsync(Arg.Any<CancellationToken>()).Returns(AlertSource.Fault);
        BaselineOf(Fault(1));
        ServerReturns();
        _client.Alerts.GetAsync(1, Arg.Any<CancellationToken>()).Returns(Fault(1, recovered: true));
        await _watcher.CheckAsync();
        await _client.Alerts.Received(1).GetAsync(1, Arg.Any<CancellationToken>());
        Assert.False(_store.State!.Snapshot.Entries[new AlertKey(AlertSource.Fault, 1)].IsOutstanding);
        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public async Task New_fault_in_an_acknowledged_group_notifies()
    {
        _client.Alerts.GetSourceAsync(Arg.Any<CancellationToken>()).Returns(AlertSource.Fault);
        BaselineOf(Fault(1, acknowledged: true));
        ServerReturns(Fault(1, acknowledged: true), Fault(2));
        await _watcher.CheckAsync();
        Assert.Single(_notifier.Shown);
        Assert.True(_notifier.Shown[0].IsProblem);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Old_state_or_changed_source_forces_a_quiet_baseline_even_with_suppression_off(bool outdated)
    {
        _settings.Notifications.SuppressOnFirstPoll = false;
        _store.State = new AlertWatchState(Server, outdated ? DesktopNMS.Core.Alerting.AlertWatchState.Outdated : Fakes.Watch(Fakes.Alert(1, 99, "critical")));
        _client.Alerts.GetSourceAsync(Arg.Any<CancellationToken>()).Returns(AlertSource.Fault);
        ServerReturns(Fault(1));
        var result = await _watcher.CheckAsync();
        Assert.Equal(AlertCheckOutcome.Baseline, result.Outcome);
        Assert.Empty(_notifier.Shown);
        Assert.Equal(1, _notifier.RemoveAllCalls);
        Assert.Equal(AlertSource.Fault, _store.State!.Snapshot.Source);
        await _watcher.CheckAsync();
        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public async Task Every_check_updates_the_app_icons_count_even_the_first()
    {
        ServerReturns(Fakes.Alert(1, 7, "critical"), Fakes.Alert(2, 7, "warning", acknowledged: true));

        var result = await _watcher.CheckAsync();

        Assert.Equal(AlertCheckOutcome.Baseline, result.Outcome);
        Assert.Equal(2, _badge.Count);
    }

    [Fact]
    public async Task First_check_records_a_baseline_quietly()
    {
        ServerReturns(Fakes.Alert(1, 7, "critical"));

        var result = await _watcher.CheckAsync();

        Assert.Equal(AlertCheckOutcome.Baseline, result.Outcome);
        Assert.Empty(_notifier.Shown);
        Assert.Equal(1, _store.State!.Snapshot.Entries[new AlertKey(AlertSource.Alert, 1)].State);
    }

    [Fact]
    public async Task First_check_announces_everything_when_suppression_is_off()
    {
        _settings.Notifications.SuppressOnFirstPoll = false;
        ServerReturns(Fakes.Alert(1, 7, "critical"));

        var result = await _watcher.CheckAsync();

        Assert.Equal(AlertCheckOutcome.Checked, result.Outcome);
        Assert.Single(_notifier.Shown);
    }

    [Fact]
    public async Task A_new_alert_after_the_baseline_is_notified()
    {
        BaselineOf(Fakes.Alert(1, 7, "warning"));
        ServerReturns(Fakes.Alert(1, 7, "warning"), Fakes.Alert(2, 9, "critical"));

        var result = await _watcher.CheckAsync();

        Assert.Equal((AlertCheckOutcome.Checked, 1, 1), (result.Outcome, result.Changes, result.Notified));
        var shown = Assert.Single(_notifier.Shown);
        Assert.Equal("device:9/rule:20", shown.Tag);
        Assert.Equal("Critical: host9", shown.Title);
        Assert.Equal("Rule 2", shown.Body);
        Assert.Equal(9, shown.DeviceId);
        Assert.Equal(2, shown.AlertId); // tapping it opens the alert's own page
        Assert.True(shown.IsProblem);
    }

    [Fact]
    public async Task Notifications_name_devices_as_the_user_chose()
    {
        _settings.DeviceNameStyle = DeviceNameStyle.SysName;
        var device = Fakes.Device(9, "10.0.9.1");
        device.SysName = "edge-rtr-09";
        _client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns([device]);
        BaselineOf(Fakes.Alert(1, 7, "warning"));
        ServerReturns(Fakes.Alert(1, 7, "warning"), Fakes.Alert(2, 9, "critical"));

        using var polling = new CorePolling(_client, _session, Fakes.Settings(_settings));
        await polling.CachedDevicesAsync();
        var watcher = new AlertWatcher(_client, _session, Fakes.Secrets(), Fakes.Settings(_settings), _store, _selfActions, _notifier, _badge, _widgets, _time, NullLogger<AlertWatcher>.Instance, polling: polling);
        await watcher.CheckAsync();

        // Not the alert's own hostname (#80).
        Assert.Equal("Critical: edge-rtr-09", Assert.Single(_notifier.Shown).Title);
    }

    [Fact]
    public async Task A_baseline_from_another_server_starts_again()
    {
        _store.State = new AlertWatchState("https://other.example.com/", Fakes.Watch());
        ServerReturns(Fakes.Alert(1, 7, "critical"));

        var result = await _watcher.CheckAsync();

        Assert.Equal(AlertCheckOutcome.Baseline, result.Outcome);
        Assert.Equal(Server, _store.State!.Server);
    }

    [Fact]
    public async Task Disabled_severities_are_not_notified()
    {
        _settings.Notifications.Warning.Enabled = false;
        BaselineOf();
        ServerReturns(Fakes.Alert(1, 7, "warning"));

        await _watcher.CheckAsync();

        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public async Task Quiet_hours_hold_back_warnings_but_not_critical()
    {
        _settings.Notifications.QuietHoursEnabled = true; // 22:00-07:00, critical allowed by default
        _time.SetUtcNow(new DateTimeOffset(2026, 9, 28, 23, 30, 0, TimeSpan.Zero));
        BaselineOf();
        ServerReturns(Fakes.Alert(1, 7, "warning"), Fakes.Alert(2, 7, "critical"));

        await _watcher.CheckAsync();

        Assert.Equal(["device:7/rule:20"], _notifier.Shown.Select(n => n.Tag));
    }

    [Fact]
    public async Task Many_changes_become_one_summary()
    {
        _settings.Notifications.MaxToastsPerPoll = 2;
        BaselineOf();
        ServerReturns(Fakes.Alert(1, 7, "critical"), Fakes.Alert(2, 8, "critical"), Fakes.Alert(3, 9, "critical"));

        await _watcher.CheckAsync();

        var summary = Assert.Single(_notifier.Shown);
        Assert.Equal(AlertNotification.SummaryTag, summary.Tag);
        Assert.Equal("3 new critical alerts", summary.Title);
        Assert.Equal("+2 more", summary.Detail);
        Assert.Null(summary.DeviceId);
        Assert.Null(summary.AlertId);
    }

    [Fact]
    public async Task Acknowledging_takes_the_problem_notification_away()
    {
        BaselineOf(Fakes.Alert(1, 7, "critical"));
        ServerReturns(Fakes.Alert(1, 7, "critical", acknowledged: true));

        await _watcher.CheckAsync();

        Assert.Equal(["device:7/rule:10"], _notifier.Removed);
        Assert.Empty(_notifier.Shown); // "notify on acknowledge" is off by default
    }

    [Fact]
    public async Task Your_own_acknowledgement_is_not_announced()
    {
        _settings.Notifications.NotifyOnAcknowledge = true;
        _selfActions.Record(1, AlertChangeKind.Acknowledged);
        BaselineOf(Fakes.Alert(1, 7, "critical"), Fakes.Alert(2, 8, "warning"));
        ServerReturns(Fakes.Alert(1, 7, "critical", acknowledged: true), Fakes.Alert(2, 8, "warning", acknowledged: true));

        await _watcher.CheckAsync();

        Assert.Equal(["Acknowledged: host8"], _notifier.Shown.Select(n => n.Title));
    }

    [Fact]
    public async Task Recoveries_need_recovered_alerts_fetched()
    {
        var recovered = Fakes.Alert(1, 7, "critical");
        recovered.StateValue = 0;
        BaselineOf(Fakes.Alert(1, 7, "critical"));
        ServerReturns(recovered);

        await _watcher.CheckAsync();

        await _client.Alerts.Received(1).ListAsync(AlertQuery.All, Arg.Any<CancellationToken>());
        var shown = Assert.Single(_notifier.Shown);
        Assert.Equal("Recovered: host7", shown.Title);
        Assert.False(shown.IsProblem);
    }

    [Fact]
    public async Task Without_recovery_notifications_only_open_alerts_are_fetched()
    {
        _settings.Notifications.NotifyOnRecovery = false;
        BaselineOf();
        ServerReturns();

        await _watcher.CheckAsync();

        await _client.Alerts.Received(1).ListAsync(AlertQuery.Open, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_background_wake_restores_the_saved_session_first()
    {
        _session.IsConnected.Returns(false);
        _session.TryRestoreAsync(default).ReturnsForAnyArgs((ConnectionTestResult?)null);

        var result = await _watcher.CheckAsync();

        Assert.Equal(AlertCheckOutcome.NotSignedIn, result.Outcome);
        await _session.ReceivedWithAnyArgs(1).TryRestoreAsync(default);
        await _client.Alerts.DidNotReceiveWithAnyArgs().ListAsync(default, default);
    }

    [Fact]
    public async Task A_failed_fetch_keeps_the_old_baseline()
    {
        BaselineOf(Fakes.Alert(1, 7, "critical"));
        var before = _store.State;
        _client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Alert>>(_ => throw new HttpRequestException("no route"));

        var result = await _watcher.CheckAsync();

        Assert.Equal(AlertCheckOutcome.Failed, result.Outcome);
        Assert.Same(before, _store.State);
    }

    [Fact]
    public async Task A_sensor_request_that_never_answers_cannot_hold_notifications_or_state()
    {
        BaselineOf();
        ServerReturns(Fakes.Alert(2, 9, "critical"));
        _widgets.IsInUse = true;
        DashyNMS.Mobile.Dashboard.DashboardLayout.Add(_settings, DashyNMS.Mobile.Dashboard.DashboardLayout.Sensors)
            .Sensors.Add(new PinnedSensor { SensorId = 1, DeviceId = 9 });
        var never = new TaskCompletionSource<IReadOnlyList<Sensor>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Assert.Single(_notifier.Shown);
            Assert.Equal(1, _store.State!.Snapshot.Entries[new AlertKey(AlertSource.Alert, 2)].State);
            return never.Task;
        });
        var watcher = new AlertWatcher(_client, _session, Fakes.Secrets(), Fakes.Settings(_settings), _store, _selfActions, _notifier, _badge, _widgets, _time, NullLogger<AlertWatcher>.Instance)
            { WidgetRefreshBudget = TimeSpan.FromSeconds(1) };
        var result = await watcher.CheckAsync(background: true).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(AlertCheckOutcome.Checked, result.Outcome);
        Assert.Single(_notifier.Shown);
        Assert.Equal(1, _store.State!.Snapshot.Entries[new AlertKey(AlertSource.Alert, 2)].State);
        Assert.Single(_widgets.Updates);
        Assert.Equal(1, _widgets.Updates[0].Critical);
        _ = _client.Sensors.Received(1).ListAsync(Arg.Any<CancellationToken>());
        never.TrySetResult([]);
    }

    [Fact]
    public async Task Failed_platform_delivery_does_not_commit_watch_state()
    {
        BaselineOf();
        var previous = _store.State;
        ServerReturns(Fakes.Alert(2, 9, "critical"));
        var notifier = Substitute.For<IAlertNotifier>();
        notifier.ShowAsync(Arg.Any<AlertNotification>()).Returns(Task.FromException(new IOException("delivery failed")));
        var watcher = new AlertWatcher(_client, _session, Fakes.Secrets(), Fakes.Settings(_settings), _store, _selfActions, notifier, _badge, _widgets, _time, NullLogger<AlertWatcher>.Instance);
        Assert.Equal(AlertCheckOutcome.Failed, (await watcher.CheckAsync()).Outcome);
        Assert.Same(previous, _store.State);
    }

    [Fact]
    public void Alert_only_snapshot_keeps_previous_sensor_readings_and_timestamp()
    {
        var previous = new WidgetSnapshot { SignedIn = true, SensorsReadAt = 123, Sensors = [new WidgetSensor(1, 9, "Temperature", "example", "20 °C", "ok", null)] };
        var current = new WidgetSnapshot { SignedIn = true, Critical = 2, PreserveEnrichment = true };
        var combined = current.RetainEnrichment(previous);
        Assert.Equal(2, combined.Critical);
        Assert.Equal(previous.Sensors, combined.Sensors);
        Assert.Equal(123, combined.SensorsReadAt);
        Assert.Empty(WidgetSnapshot.SignedOut.RetainEnrichment(previous).Sensors);
    }

    [Fact]
    public async Task Cancellation_after_listing_leaves_changes_for_the_next_check()
    {
        BaselineOf();
        var previous = _store.State;
        using var cancellation = new CancellationTokenSource();
        _client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            cancellation.Cancel();
            return new[] { Fakes.Alert(2, 9, "critical") };
        });
        Assert.Equal(AlertCheckOutcome.Failed, (await _watcher.CheckAsync(cancellation.Token)).Outcome);
        Assert.Same(previous, _store.State);
        Assert.Empty(_notifier.Shown);
        ServerReturns(Fakes.Alert(2, 9, "critical"));
        await _watcher.CheckAsync();
        await _watcher.CheckAsync();
        Assert.Single(_notifier.Shown);
    }

    [Fact]
    public async Task Overlapping_checks_run_once()
    {
        var release = new TaskCompletionSource<IReadOnlyList<Alert>>();
        _client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()).Returns(release.Task);

        var first = _watcher.CheckAsync();
        var second = await _watcher.CheckAsync();
        release.SetResult([]);
        await first;

        Assert.Equal(AlertCheckOutcome.Skipped, second.Outcome);
        await _client.Alerts.ReceivedWithAnyArgs(1).ListAsync(default, default);
    }
}

public sealed class AlertBadgeTests
{
    private static Alert WithState(int id, int state)
    {
        var alert = Fakes.Alert(id, 1, "critical");
        alert.StateValue = state;
        return alert;
    }

    private static readonly Alert[] Alerts =
    [
        WithState(1, 1), WithState(2, 1), // active
        WithState(3, 2),                  // acknowledged
        WithState(4, 0),                  // recovered
    ];

    [Fact]
    public void Counts_as_desktops_alerts_tab_badge()
    {
        Assert.Equal(3, AlertBadge.Count(Alerts, new AppSettings())); // desktop's default counts acknowledged too
        Assert.Equal(2, AlertBadge.Count(Alerts, new AppSettings { AlertTabBadgeIncludesAcknowledged = false }));
        Assert.Equal(0, AlertBadge.Count(Alerts, new AppSettings { ShowAlertTabBadge = false }));
    }
}

public sealed class AlertWatchCoordinatorTests
{
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly AppSettings _settings = new();
    private readonly IBackgroundAlertScheduler _scheduler = Substitute.For<IBackgroundAlertScheduler>();
    private readonly InMemoryWatchStore _store = new();
    private readonly RecordingBadge _badge = new();
    private readonly RecordingWidgets _widgets = new() { IsInUse = false };
    private readonly AlertWatchCoordinator _coordinator;

    public AlertWatchCoordinatorTests()
    {
        var client = Fakes.Client();
        var watcher = new AlertWatcher(
            client, _session, Fakes.Secrets(), Fakes.Settings(_settings), _store, new SelfActionTracker(),
            new RecordingNotifier(), _badge, _widgets, TimeProvider.System, NullLogger<AlertWatcher>.Instance);
        _coordinator = new AlertWatchCoordinator(
            _session, Fakes.Settings(_settings), watcher, _scheduler, _store, _badge, _widgets, TimeProvider.System, NullLogger<AlertWatchCoordinator>.Instance);
        _coordinator.Start();
    }

    private void SessionBecomes(bool connected)
    {
        _session.IsConnected.Returns(connected);
        _session.StateChanged += Raise.Event();
    }

    [Fact]
    public void Starting_changes_nothing_until_the_session_does()
    {
        _scheduler.DidNotReceiveWithAnyArgs().Schedule();
        _scheduler.DidNotReceiveWithAnyArgs().Cancel();
    }

    [Fact]
    public void Signing_in_schedules_background_checks()
    {
        SessionBecomes(connected: true);

        _scheduler.Received(1).Schedule();
    }

    [Fact]
    public void With_notifications_off_the_badge_still_needs_checks()
    {
        _settings.Notifications.Enabled = false;

        SessionBecomes(connected: true);

        _scheduler.Received(1).Schedule();
    }

    [Fact]
    public void Where_the_phone_cant_show_a_count_the_badge_alone_needs_no_checks()
    {
        _settings.Notifications.Enabled = false;
        _badge.IsSupported = false;

        SessionBecomes(connected: true);

        _scheduler.DidNotReceive().Schedule();
    }

    [Fact]
    public void Signing_out_clears_the_badge()
    {
        SessionBecomes(connected: true);
        SessionBecomes(connected: false);

        Assert.Equal(0, _badge.Count);
    }

    [Fact]
    public void Signing_in_with_notifications_and_the_badge_off_schedules_nothing()
    {
        _settings.Notifications.Enabled = false;
        _settings.ShowAlertTabBadge = false;

        SessionBecomes(connected: true);

        _scheduler.DidNotReceive().Schedule();
        _scheduler.Received(1).Cancel();
    }

    [Fact]
    public void Signing_out_cancels_and_forgets_the_baseline()
    {
        _store.State = new AlertWatchState("https://nms.example.com/", Fakes.Watch());

        SessionBecomes(connected: false);

        _scheduler.Received(1).Cancel();
        Assert.Null(_store.State);
    }

    [Fact]
    public void The_in_app_timer_runs_only_while_in_the_foreground()
    {
        SessionBecomes(connected: true);

        _coordinator.SetForeground(true);
        Assert.True(_coordinator.IsForegroundLoopRunning);

        _coordinator.SetForeground(false);
        Assert.False(_coordinator.IsForegroundLoopRunning);
    }

    [Fact]
    public void Turning_notifications_and_the_badge_off_stops_everything()
    {
        SessionBecomes(connected: true);
        _coordinator.SetForeground(true);

        _settings.Notifications.Enabled = false;
        _settings.ShowAlertTabBadge = false;
        _coordinator.SettingsChanged();

        _scheduler.Received(1).Cancel();
        Assert.False(_coordinator.IsForegroundLoopRunning);
        Assert.Equal(0, _badge.Count); // and the count comes off the icon
    }
}

public sealed class NotificationRouterTests
{
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly RecordingNavigation _navigation = new();
    private readonly NotificationRouter _router;

    public NotificationRouterTests()
    {
        _session.IsConnected.Returns(true);
        _router = new NotificationRouter(_session, _navigation);
    }

    [Fact]
    public async Task A_tap_that_launched_the_app_waits_for_sign_in()
    {
        await _router.OpenAsync(new NotificationTarget(DeviceId: 7));
        Assert.Empty(_navigation.Visits);

        await _router.MainShownAsync();

        var visit = Assert.Single(_navigation.Visits);
        Assert.Equal(Routes.DeviceDetail, visit.Route);
        Assert.Equal(7, visit.Parameters![Routes.DeviceIdParameter]);
    }

    [Fact]
    public async Task Once_signed_in_a_tap_goes_straight_there()
    {
        await _router.MainShownAsync();

        await _router.OpenAsync(new NotificationTarget(DeviceId: null));

        Assert.Equal(Routes.Alerts, Assert.Single(_navigation.Visits).Route);
    }

    [Fact]
    public async Task After_signing_out_a_tap_waits_again()
    {
        await _router.MainShownAsync();
        _session.IsConnected.Returns(false);
        _session.StateChanged += Raise.Event();

        await _router.OpenAsync(new NotificationTarget(DeviceId: 7));

        Assert.Empty(_navigation.Visits);
    }
}

public sealed class AlertWatchStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"alert-watch-{Guid.NewGuid():N}.json");
    private readonly AlertWatchStore _store;

    public AlertWatchStoreTests() => _store = new AlertWatchStore(_path, NullLogger<AlertWatchStore>.Instance);

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void Round_trips_and_clears()
    {
        Assert.Null(_store.Load());

        _store.Save(new AlertWatchState("https://nms.example.com/", Fakes.Watch(Fakes.Alert(4, 1, "critical", acknowledged: true))));
        var loaded = _store.Load();

        Assert.Equal("https://nms.example.com/", loaded!.Server);
        Assert.Equal(2, loaded.Snapshot.Entries[new AlertKey(AlertSource.Alert, 4)].State);

        _store.Clear();
        Assert.Null(_store.Load());
    }

    [Fact]
    public void Old_numeric_state_is_read_as_needing_a_baseline()
    {
        File.WriteAllText(_path, """{"server":"https://nms.example.com/","alerts":{"12":1}}""");
        Assert.True(_store.Load()!.Snapshot.NeedsBaseline);
    }

    [Fact]
    public void Fault_keys_and_recovery_memory_round_trip()
    {
        var cleared = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
        _store.Save(new AlertWatchState("https://nms.example.com/", new DesktopNMS.Core.Alerting.AlertWatchState(AlertSource.Fault,
            new Dictionary<AlertKey, AlertWatchEntry> { [new(AlertSource.Fault, 12)] = new(7, 10, 0, cleared) })));
        var state = _store.Load()!.Snapshot;
        Assert.Equal(AlertSource.Fault, state.Source);
        Assert.Equal(cleared, state.Entries[new AlertKey(AlertSource.Fault, 12)].ClearedAtUtc);
        Assert.False(state.NeedsBaseline);
    }

    [Fact]
    public void A_corrupt_file_means_a_fresh_baseline()
    {
        File.WriteAllText(_path, "{ not json");

        Assert.Null(_store.Load());
    }
}

public sealed class NotificationSettingsViewModelTests
{
    [Fact]
    public void Toggling_a_setting_saves_it_and_reapplies_the_schedule()
    {
        var session = Substitute.For<ISessionService>();
        session.IsConnected.Returns(true);
        var appSettings = new AppSettings();
        var settings = Fakes.Settings(appSettings);
        var scheduler = Substitute.For<IBackgroundAlertScheduler>();
        var store = new InMemoryWatchStore();
        var notifier = new RecordingNotifier();
        var watcher = new AlertWatcher(
            Fakes.Client(), session, Fakes.Secrets(), settings, store, new SelfActionTracker(), notifier, new NoAppBadge(), new NoHomeWidgets(), TimeProvider.System, NullLogger<AlertWatcher>.Instance);
        var coordinator = new AlertWatchCoordinator(session, settings, watcher, scheduler, store, new NoAppBadge(), new NoHomeWidgets(), TimeProvider.System, NullLogger<AlertWatchCoordinator>.Instance);
        var vm = new SettingsViewModel(session, settings, Substitute.For<IDialogService>(), new RecordingNavigation(), notifier, coordinator, new NoAppBadge(), new InMemoryAppearance(), new DashyNMS.Mobile.Widgets.NoHomeWidgets());

        vm.NotificationsEnabled = false;

        Assert.False(appSettings.Notifications.Enabled);
        settings.Received(1).Save();
        scheduler.Received(1).Cancel();

        vm.QuietHoursStart = 40;
        Assert.Equal(23, appSettings.Notifications.QuietHoursStartHour);
    }
}
