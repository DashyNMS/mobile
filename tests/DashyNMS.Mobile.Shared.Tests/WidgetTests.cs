using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Widgets;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class WidgetSnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private static Alert WithState(Alert alert, int state)
    {
        alert.StateValue = state;
        return alert;
    }

    [Fact]
    public void Counts_active_alerts_by_severity_and_lists_the_worst_first()
    {
        var snapshot = WidgetSnapshot.Build(
        [
            Fakes.Alert(1, 1, "warning", at: new DateTime(2026, 9, 28, 11, 0, 0)),
            Fakes.Alert(2, 2, "critical", at: new DateTime(2026, 9, 28, 9, 0, 0)),
            Fakes.Alert(3, 3, "critical", at: new DateTime(2026, 9, 28, 10, 0, 0)),
            Fakes.Alert(4, 4, "critical", acknowledged: true),
            WithState(Fakes.Alert(5, 5, "critical"), 0), // recovered
            Fakes.Alert(6, 6, "warning", at: new DateTime(2026, 9, 28, 8, 0, 0)),
        ], devicesDown: 2, Now);

        Assert.True(snapshot.SignedIn);
        Assert.Equal((2, 2, 1, 2), (snapshot.Critical, snapshot.Warning, snapshot.Acknowledged, snapshot.DevicesDown));
        Assert.Equal([3, 2, 1], snapshot.Alerts.Select(a => a.AlertId)); // critical newest first, then warnings; three at most
        Assert.Equal(new WidgetAlert(3, 3, "critical", "Rule 3", "host3"), snapshot.Alerts[0]);
        Assert.Equal(Now.ToUnixTimeSeconds(), snapshot.CheckedAt);
    }

    [Fact]
    public void Round_trips_through_the_json_the_ios_widget_reads()
    {
        var snapshot = WidgetSnapshot.Build([Fakes.Alert(1, 7, "critical")], devicesDown: null, Now);

        var json = snapshot.ToJson();

        // The names Snapshot.swift decodes.
        foreach (var key in new[] { "\"signedIn\"", "\"critical\"", "\"warning\"", "\"acknowledged\"", "\"devicesDown\"", "\"alerts\"", "\"checkedAt\"", "\"alertId\"", "\"deviceId\"", "\"severity\"", "\"rule\"", "\"device\"" })
        {
            Assert.Contains(key, json);
        }

        var back = WidgetSnapshot.FromJson(json);
        Assert.Equal(snapshot with { Alerts = [] }, back with { Alerts = [] });
        Assert.Equal(snapshot.Alerts, back.Alerts);
    }

    [Fact]
    public void Nothing_saved_or_something_unreadable_reads_as_signed_out()
    {
        Assert.False(WidgetSnapshot.FromJson(null).SignedIn);
        Assert.False(WidgetSnapshot.FromJson("{ not json").SignedIn);
    }
}

public sealed class WidgetUpdateTests
{
    private readonly ILibreNmsClient _client = Fakes.Client(
        devices: [Fakes.Device(1, "a"), Fakes.Device(2, "b", up: false), Fakes.Device(3, "c", up: false, disabled: true)],
        alerts: [Fakes.Alert(1, 2, "critical")]);

    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly AppSettings _settings = new();
    private readonly RecordingWidgets _widgets = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly AlertWatcher _watcher;

    public WidgetUpdateTests()
    {
        _session.IsConnected.Returns(true);
        _session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.com/"), "token"));
        _watcher = new AlertWatcher(
            _client, _session, Fakes.Secrets(), Fakes.Settings(_settings), new InMemoryWatchStore(), new SelfActionTracker(),
            new RecordingNotifier(), new NoAppBadge(), _widgets, _time, NullLogger<AlertWatcher>.Instance);
    }

    [Fact]
    public async Task Each_check_updates_the_widget_reading_devices_every_few_minutes()
    {
        await _watcher.CheckAsync();

        var first = Assert.Single(_widgets.Updates);
        Assert.Equal((1, 1), (first.Critical, first.DevicesDown));

        _time.Advance(TimeSpan.FromMinutes(1));
        await _watcher.CheckAsync();
        _time.Advance(AlertWatcher.DevicesDownMaxAge);
        await _watcher.CheckAsync();

        Assert.Equal(3, _widgets.Updates.Count);
        await _client.Devices.Received(2).ListAsync(Arg.Any<CancellationToken>()); // not on the middle check
    }

    [Fact]
    public async Task No_widget_no_device_list()
    {
        _widgets.IsInUse = false;

        await _watcher.CheckAsync();

        Assert.Empty(_widgets.Updates);
        await _client.Devices.DidNotReceiveWithAnyArgs().ListAsync(default);
    }

    [Fact]
    public async Task A_failed_device_list_still_updates_the_alerts()
    {
        _client.Devices.ListAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Device>>(_ => throw new LibreNmsApiException("busy", System.Net.HttpStatusCode.ServiceUnavailable));

        var result = await _watcher.CheckAsync();

        Assert.True(result.Succeeded);
        var snapshot = Assert.Single(_widgets.Updates);
        Assert.Equal(1, snapshot.Critical);
        Assert.Null(snapshot.DevicesDown);
    }
}

public sealed class WidgetCoordinatorTests
{
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly AppSettings _settings = new() { ShowAlertTabBadge = false };
    private readonly IBackgroundAlertScheduler _scheduler = Substitute.For<IBackgroundAlertScheduler>();
    private readonly RecordingWidgets _widgets = new();
    private readonly AlertWatchCoordinator _coordinator;

    public WidgetCoordinatorTests()
    {
        _settings.Notifications.Enabled = false;
        var store = new InMemoryWatchStore();
        var watcher = new AlertWatcher(
            Fakes.Client(), _session, Fakes.Secrets(), Fakes.Settings(_settings), store, new SelfActionTracker(),
            new RecordingNotifier(), new NoAppBadge(), _widgets, TimeProvider.System, NullLogger<AlertWatcher>.Instance);
        _coordinator = new AlertWatchCoordinator(
            _session, Fakes.Settings(_settings), watcher, _scheduler, store, new NoAppBadge(), _widgets, TimeProvider.System,
            NullLogger<AlertWatchCoordinator>.Instance);
        _coordinator.Start();
    }

    private void SessionBecomes(bool connected)
    {
        _session.IsConnected.Returns(connected);
        _session.StateChanged += Raise.Event();
    }

    [Fact]
    public void A_widget_alone_keeps_checks_running()
    {
        SessionBecomes(connected: true);

        Assert.True(_coordinator.ChecksNeeded);
        _scheduler.Received(1).Schedule();
    }

    [Fact]
    public void Signing_out_blanks_the_widget()
    {
        SessionBecomes(connected: true);
        SessionBecomes(connected: false);

        Assert.False(Assert.Single(_widgets.Updates).SignedIn);
    }
}

public sealed class WidgetLinkTests
{
    [Theory]
    [InlineData("dashynms://alerts", null, null)]
    [InlineData("dashynms://alert/5?device=3", 3, 5)]
    [InlineData("DASHYNMS://alert/5", null, 5)]
    [InlineData("dashynms://alert/5?device=nope", null, 5)]
    public void Opens_the_places_a_notification_can(string link, int? deviceId, int? alertId)
    {
        Assert.Equal(new DashyNMS.Mobile.Alerts.NotificationTarget(deviceId, alertId), WidgetLink.TryParse(new Uri(link)));
    }

    [Theory]
    [InlineData("https://evil.example/alert/5")]
    [InlineData("dashynms://alert/")]
    [InlineData("dashynms://alert/-1")]
    [InlineData("dashynms://alert/5x")]
    [InlineData("dashynms://settings/signout")]
    public void Ignores_anything_else(string link)
    {
        Assert.Null(WidgetLink.TryParse(new Uri(link)));
    }
}
