using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
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
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly AlertWatcher _watcher;

    public AlertWatcherTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _session.IsConnected.Returns(true);
        _session.Connection.Returns(new LibreNmsConnection(new Uri(Server), "token"));
        _watcher = new AlertWatcher(
            _client, _session, Fakes.Secrets(), Fakes.Settings(_settings), _store, _selfActions, _notifier, _time, NullLogger<AlertWatcher>.Instance);
    }

    private void ServerReturns(params Alert[] alerts) =>
        _client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()).Returns(alerts);

    private void BaselineOf(params Alert[] alerts) =>
        _store.State = new AlertWatchState(Server, AlertChangeDetector.Snapshot(alerts));

    [Fact]
    public async Task First_check_records_a_baseline_quietly()
    {
        ServerReturns(Fakes.Alert(1, 7, "critical"));

        var result = await _watcher.CheckAsync();

        Assert.Equal(AlertCheckOutcome.Baseline, result.Outcome);
        Assert.Empty(_notifier.Shown);
        Assert.Equal(1, _store.State!.States[1]);
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
        Assert.Equal("alert-2", shown.Tag);
        Assert.Equal("Critical: host9", shown.Title);
        Assert.Equal("Rule 2", shown.Body);
        Assert.Equal(9, shown.DeviceId);
        Assert.Equal(2, shown.AlertId); // tapping it opens the alert's own page
        Assert.True(shown.IsProblem);
    }

    [Fact]
    public async Task A_baseline_from_another_server_starts_again()
    {
        _store.State = new AlertWatchState("https://other.example.com/", new Dictionary<int, int>());
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

        Assert.Equal(["alert-2"], _notifier.Shown.Select(n => n.Tag));
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

        Assert.Equal(["alert-1"], _notifier.Removed);
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

public sealed class AlertWatchCoordinatorTests
{
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly AppSettings _settings = new();
    private readonly IBackgroundAlertScheduler _scheduler = Substitute.For<IBackgroundAlertScheduler>();
    private readonly InMemoryWatchStore _store = new();
    private readonly AlertWatchCoordinator _coordinator;

    public AlertWatchCoordinatorTests()
    {
        var client = Fakes.Client();
        var watcher = new AlertWatcher(
            client, _session, Fakes.Secrets(), Fakes.Settings(_settings), _store, new SelfActionTracker(),
            new RecordingNotifier(), TimeProvider.System, NullLogger<AlertWatcher>.Instance);
        _coordinator = new AlertWatchCoordinator(
            _session, Fakes.Settings(_settings), watcher, _scheduler, _store, TimeProvider.System, NullLogger<AlertWatchCoordinator>.Instance);
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
    public void Signing_in_with_notifications_off_schedules_nothing()
    {
        _settings.Notifications.Enabled = false;

        SessionBecomes(connected: true);

        _scheduler.DidNotReceive().Schedule();
        _scheduler.Received(1).Cancel();
    }

    [Fact]
    public void Signing_out_cancels_and_forgets_the_baseline()
    {
        _store.State = new AlertWatchState("https://nms.example.com/", new Dictionary<int, int>());

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
    public void Turning_notifications_off_stops_everything()
    {
        SessionBecomes(connected: true);
        _coordinator.SetForeground(true);

        _settings.Notifications.Enabled = false;
        _coordinator.SettingsChanged();

        _scheduler.Received(1).Cancel();
        Assert.False(_coordinator.IsForegroundLoopRunning);
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

        _store.Save(new AlertWatchState("https://nms.example.com/", new Dictionary<int, int> { [4] = 2 }));
        var loaded = _store.Load();

        Assert.Equal("https://nms.example.com/", loaded!.Server);
        Assert.Equal(2, loaded.States[4]);

        _store.Clear();
        Assert.Null(_store.Load());
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
            Fakes.Client(), session, Fakes.Secrets(), settings, store, new SelfActionTracker(), notifier, TimeProvider.System, NullLogger<AlertWatcher>.Instance);
        var coordinator = new AlertWatchCoordinator(session, settings, watcher, scheduler, store, TimeProvider.System, NullLogger<AlertWatchCoordinator>.Instance);
        var vm = new SettingsViewModel(session, settings, Substitute.For<IDialogService>(), new RecordingNavigation(), notifier, coordinator);

        vm.NotificationsEnabled = false;

        Assert.False(appSettings.Notifications.Enabled);
        settings.Received(1).Save();
        scheduler.Received(1).Cancel();

        vm.QuietHoursStart = 40;
        Assert.Equal(23, appSettings.Notifications.QuietHoursStartHour);
    }
}
