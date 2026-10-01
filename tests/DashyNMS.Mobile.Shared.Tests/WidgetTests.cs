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
        ], Now, new AppSettings());

        Assert.True(snapshot.SignedIn);
        Assert.Equal((2, 2, 1), (snapshot.Critical, snapshot.Warning, snapshot.Acknowledged));
        Assert.Null(snapshot.DevicesDown);
        Assert.Null(snapshot.Devices);

        // Critical newest first, then warnings, then acknowledged at the end.
        Assert.Equal([3, 2, 1, 6, 4], snapshot.Alerts.Select(a => a.AlertId));
        Assert.Equal(
            new WidgetAlert(3, 3, "critical", "Rule 3", "host3", false, new DateTimeOffset(new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Local)).ToUnixTimeSeconds()),
            snapshot.Alerts[0]);
        Assert.True(snapshot.Alerts[^1].Acknowledged);
        Assert.Equal(Now.ToUnixTimeSeconds(), snapshot.CheckedAt);
    }

    [Fact]
    public void Round_trips_through_the_json_the_ios_widget_reads()
    {
        var snapshot = WidgetSnapshot.Build(
            [Fakes.Alert(1, 7, "critical")], Now, PinningAndSensors(),
            [Fakes.Device(7, "core", up: false, location: "Comms room")],
            [Sensor(1, 7, "temperature", 71)]);

        var json = snapshot.ToJson();

        // The names Snapshot.swift decodes.
        foreach (var key in new[] { "\"signedIn\"", "\"critical\"", "\"warning\"", "\"acknowledged\"", "\"devicesDown\"", "\"alerts\"", "\"checkedAt\"", "\"alertId\"", "\"deviceId\"", "\"severity\"", "\"rule\"", "\"device\"" })
        {
            Assert.Contains(key, json);
        }

        foreach (var key in new[] { "\"acknowledged\"", "\"raisedAt\"", "\"devices\"", "\"total\"", "\"ok\"", "\"pinned\"", "\"state\"", "\"status\"", "\"since\"", "\"location\"", "\"sensors\"", "\"sensorId\"", "\"value\"", "\"position\"", "\"sensorsReadAt\"", "\"hideLockScreenDetails\"" })
        {
            Assert.Contains(key, json);
        }

        var back = WidgetSnapshot.FromJson(json);
        Assert.Equal(snapshot with { Alerts = [], Pinned = [], Sensors = [] }, back with { Alerts = [], Pinned = [], Sensors = [] });
        Assert.Equal(snapshot.Alerts, back.Alerts);
        Assert.Equal(snapshot.Pinned, back.Pinned);
        Assert.Equal(snapshot.Sensors, back.Sensors);
    }

    [Fact]
    public void A_snapshot_saved_before_the_new_widgets_still_reads()
    {
        var back = WidgetSnapshot.FromJson("""{"signedIn":true,"critical":1,"warning":0,"acknowledged":0,"devicesDown":null,"alerts":[{"alertId":1,"deviceId":2,"severity":"critical","rule":"Down","device":"a"}],"checkedAt":5}""");

        Assert.True(back.SignedIn);
        Assert.False(Assert.Single(back.Alerts).Acknowledged);
        Assert.Empty(back.Pinned);
        Assert.Null(back.Devices);
    }

    private static AppSettings PinningAndSensors()
    {
        var settings = new AppSettings();
        settings.PinnedDevices.Add(new PinnedDevice { DeviceId = 7 });
        DashyNMS.Mobile.Dashboard.DashboardLayout.Add(settings, DashyNMS.Mobile.Dashboard.DashboardLayout.Sensors)
            .Sensors.Add(new PinnedSensor { SensorId = 1, DeviceId = 7, Description = "Chassis" });
        return settings;
    }

    private static Sensor Sensor(int id, int deviceId, string sensorClass, double current, double? low = null, double? high = null) =>
        new() { SensorId = id, DeviceId = deviceId, SensorClass = sensorClass, Description = $"Sensor {id}", Current = current, LimitLow = low, LimitHigh = high };

    [Fact]
    public void Counts_each_device_once_by_its_worst_alert()
    {
        var snapshot = WidgetSnapshot.Build(
        [
            Fakes.Alert(1, 1, "critical"),
            Fakes.Alert(2, 1, "warning"),
            Fakes.Alert(3, 2, "warning"),
            Fakes.Alert(4, 3, "critical", acknowledged: true),
            Fakes.Alert(5, 9, "critical"), // on a disabled device
        ], Now, new AppSettings(),
        [
            Fakes.Device(1, "a", up: false), Fakes.Device(2, "b"), Fakes.Device(3, "c"), Fakes.Device(4, "d"), Fakes.Device(5, "e"),
            Fakes.Device(9, "off", disabled: true),
        ]);

        Assert.Equal(new WidgetDeviceCounts(Total: 6, Up: 4, Down: 1, Disabled: 1, Critical: 1, Warning: 1, Acknowledged: 1, Ok: 2), snapshot.Devices);
        Assert.Equal(1, snapshot.DevicesDown);
    }

    [Fact]
    public void Pinned_devices_say_what_is_wrong_or_how_long_they_have_been_up()
    {
        var settings = new AppSettings();
        foreach (var id in new[] { 1, 2, 3, 4, 99 })
        {
            settings.PinnedDevices.Add(new PinnedDevice { DeviceId = id });
        }

        var snapshot = WidgetSnapshot.Build(
            [Fakes.Alert(1, 1, "critical", at: new DateTime(2026, 9, 28, 11, 48, 0, DateTimeKind.Utc)), Fakes.Alert(2, 2, "warning")],
            Now, settings,
            [
                Fakes.Device(1, "core", up: false, location: "Comms room"),
                Fakes.Device(2, "dist"),
                Fakes.Device(3, "fw", uptime: 41 * 86400),
                Fakes.Device(4, "old", disabled: true),
            ]);

        // Pinned order; a pin whose device has gone doesn't show.
        Assert.Equal(["core", "dist", "fw", "old"], snapshot.Pinned.Select(p => p.Name));
        Assert.Equal(("down", "Down", "Comms room"), (snapshot.Pinned[0].State, snapshot.Pinned[0].Status, snapshot.Pinned[0].Location));
        Assert.Equal("Down for 12 min", WidgetFormat.DeviceStatus(snapshot.Pinned[0], Now));
        Assert.Equal(("warning", "Rule 2"), (snapshot.Pinned[1].State, snapshot.Pinned[1].Status));
        Assert.Equal(("ok", "Up 41d 0h"), (snapshot.Pinned[2].State, snapshot.Pinned[2].Status));
        Assert.Equal("disabled", snapshot.Pinned[3].State);

        settings.EnablePinnedDevices = false;
        Assert.Empty(WidgetSnapshot.Build([], Now, settings, [Fakes.Device(1, "core")]).Pinned);
    }

    [Fact]
    public void Sensors_are_the_dashboard_cards_read_against_the_thresholds()
    {
        var settings = new AppSettings();
        var card = DashyNMS.Mobile.Dashboard.DashboardLayout.Add(settings, DashyNMS.Mobile.Dashboard.DashboardLayout.Sensors);
        card.Sensors.Add(new PinnedSensor { SensorId = 2, DeviceId = 1, Description = "Fan" });
        card.Sensors.Add(new PinnedSensor { SensorId = 1, DeviceId = 1, Description = "Chassis" });
        card.Sensors.Add(new PinnedSensor { SensorId = 3, DeviceId = 1, DeviceName = "saved", Description = "Gone" });

        var snapshot = WidgetSnapshot.Build([], Now, settings, [Fakes.Device(1, "core")],
        [
            Sensor(1, 1, "temperature", 20),
            Sensor(2, 1, "load", 90, low: 0, high: 100),
        ]);

        Assert.Equal([2, 1, 3], snapshot.Sensors.Select(s => s.SensorId));
        Assert.Equal(0.9, snapshot.Sensors[0].Position);
        Assert.Null(snapshot.Sensors[1].Position); // no limits to sit between
        Assert.Equal(("ok", "core"), (snapshot.Sensors[1].Status, snapshot.Sensors[1].Device));
        Assert.Equal(("Gone", "unknown", "–"), (snapshot.Sensors[2].Name, snapshot.Sensors[2].Status, snapshot.Sensors[2].Value));
        Assert.Equal(Now.ToUnixTimeSeconds(), snapshot.SensorsReadAt);
    }

    [Fact]
    public void Ages_and_pie_slices_are_worked_out_as_the_widget_draws()
    {
        var at = Now.ToUnixTimeSeconds();
        Assert.Equal("just now", WidgetFormat.Age(at, Now));
        Assert.Equal("12 min", WidgetFormat.Age(at - 12 * 60, Now));
        Assert.Equal("3 hr", WidgetFormat.Age(at - 3 * 3600, Now));
        Assert.Equal("1 day", WidgetFormat.Age(at - 86400, Now));
        Assert.Equal("4 days", WidgetFormat.Age(at - 4 * 86400, Now));
        Assert.Equal(string.Empty, WidgetFormat.Age(0, Now));
        Assert.Equal("Checked 2 min ago", WidgetFormat.Checked(at - 120, Now));

        var slices = WidgetFormat.PieSlices(new WidgetDeviceCounts(1000, 1000, 0, 0, 1, 0, 0, 999));
        Assert.Equal(["critical", "ok"], slices.Select(s => s.State));
        Assert.True(slices[0].Fraction > 0.01); // one among a thousand still shows
        Assert.Equal(1, slices.Sum(s => s.Fraction), 6);
        Assert.Empty(WidgetFormat.PieSlices(new WidgetDeviceCounts(0, 0, 0, 0, 0, 0, 0, 0)));
        Assert.Equal("42/48", WidgetFormat.OkShare(new WidgetDeviceCounts(50, 47, 1, 2, 2, 3, 1, 42)));
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
        Assert.Equal(2, first.Devices!.Total - first.Devices.Disabled);

        _time.Advance(TimeSpan.FromMinutes(1));
        await _watcher.CheckAsync();
        _time.Advance(AlertWatcher.DevicesMaxAge);
        await _watcher.CheckAsync();

        Assert.Equal(3, _widgets.Updates.Count);
        await _client.Devices.Received(2).ListAsync(Arg.Any<CancellationToken>()); // not on the middle check
    }

    [Fact]
    public async Task Sensors_are_read_only_when_picked_and_at_most_every_quarter_hour()
    {
        await _watcher.CheckAsync();
        await _client.Sensors.DidNotReceiveWithAnyArgs().ListAsync(default);

        DashyNMS.Mobile.Dashboard.DashboardLayout.Add(_settings, DashyNMS.Mobile.Dashboard.DashboardLayout.Sensors)
            .Sensors.Add(new PinnedSensor { SensorId = 1, DeviceId = 1 });
        await _watcher.CheckAsync();
        _time.Advance(TimeSpan.FromMinutes(10));
        await _watcher.CheckAsync();
        _time.Advance(AlertWatcher.SensorsMaxAge);
        await _watcher.CheckAsync();

        await _client.Sensors.Received(2).ListAsync(Arg.Any<CancellationToken>());
        Assert.Single(_widgets.Updates[^1].Sensors);
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
    [InlineData("dashynms://device/7", 7, null)]
    public void Opens_the_places_a_notification_can(string link, int? deviceId, int? alertId)
    {
        Assert.Equal(new DashyNMS.Mobile.Alerts.NotificationTarget(deviceId, alertId), WidgetLink.TryParse(new Uri(link)));
    }

    [Theory]
    [InlineData("https://evil.example/alert/5")]
    [InlineData("dashynms://alert/")]
    [InlineData("dashynms://alert/-1")]
    [InlineData("dashynms://alert/5x")]
    [InlineData("dashynms://device/")]
    [InlineData("dashynms://device/0")]
    [InlineData("dashynms://settings/signout")]
    public void Ignores_anything_else(string link)
    {
        Assert.Null(WidgetLink.TryParse(new Uri(link)));
    }
}
