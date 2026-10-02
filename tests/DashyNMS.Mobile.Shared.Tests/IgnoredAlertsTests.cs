using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Rules;
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

/// <summary>Ignoring chosen alerts in notifications only (#102).</summary>
public sealed class IgnoredAlertsTests
{
    private const string Server = "https://nms.example.com/";

    private readonly InMemoryPreferences _preferences = new();
    private readonly IgnoredAlerts _ignored;

    public IgnoredAlertsTests() => _ignored = new IgnoredAlerts(_preferences);

    // Fakes.Alert gives rule id * 10: alert 1 is rule 10, alert 2 rule 20.
    private static Alert On(int alertId, int deviceId) => Fakes.Alert(alertId, deviceId, "critical");

    [Fact]
    public void A_rule_can_be_ignored_on_one_device_or_every_device()
    {
        _ignored.Ignore(10, "Rule 1", 7, "core-sw");

        Assert.True(_ignored.IsIgnored(On(1, 7)));
        Assert.False(_ignored.IsIgnored(On(1, 9)));
        Assert.False(_ignored.IsIgnored(On(2, 7)));
        Assert.False(_ignored.IsRuleIgnored(10));
        Assert.Equal("Rule 1 on core-sw", _ignored.All.Single().Description);

        // Every device takes in the single one.
        _ignored.Ignore(10, "Rule 1");
        Assert.Equal("Rule 1, on every device", _ignored.All.Single().Description);
        Assert.True(_ignored.IsIgnored(On(1, 9)));

        // And then a single device adds nothing.
        _ignored.Ignore(10, "Rule 1", 9, "edge-rtr");
        Assert.Single(_ignored.All);
    }

    [Fact]
    public void Kept_between_launches_and_unreadable_is_nothing_ignored()
    {
        _ignored.Ignore(20, "Rule 2", 7, "core-sw");

        Assert.True(new IgnoredAlerts(_preferences).IsIgnored(On(2, 7)));

        _preferences.Set(IgnoredAlerts.Key, "{not json");
        Assert.Empty(new IgnoredAlerts(_preferences).All);
    }

    [Fact]
    public void Notifying_again_clears_whatever_covered_the_alert()
    {
        _ignored.Ignore(10, "Rule 1", 7, "core-sw");
        _ignored.Ignore(20, "Rule 2");
        var changes = 0;
        _ignored.Changed += (_, _) => changes++;

        _ignored.NotifyAgain(On(1, 7));
        _ignored.NotifyAgain(20);

        Assert.Empty(_ignored.All);
        Assert.Equal(2, changes);
        Assert.Null(_preferences.Get(IgnoredAlerts.Key));
    }

    [Fact]
    public async Task An_ignored_alert_doesnt_notify_but_still_counts_on_the_badge()
    {
        var client = Fakes.Client();
        var session = Substitute.For<ISessionService>();
        session.IsConnected.Returns(true);
        session.Connection.Returns(new LibreNmsConnection(new Uri(Server), "token"));
        var store = new InMemoryWatchStore();
        var notifier = new RecordingNotifier();
        var badge = new RecordingBadge();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        var watcher = new AlertWatcher(
            client, session, Fakes.Secrets(), Fakes.Settings(new AppSettings()), store, new SelfActionTracker(), notifier, badge,
            new RecordingWidgets { IsInUse = false }, time, NullLogger<AlertWatcher>.Instance, ignored: _ignored);

        store.State = new AlertWatchState(Server, AlertChangeDetector.Snapshot([]));
        client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()).Returns([On(1, 7), On(2, 7)]);
        _ignored.Ignore(10, "Rule 1", 7, "core-sw");

        var result = await watcher.CheckAsync();

        Assert.Equal("alert-2", Assert.Single(notifier.Shown).Tag);
        Assert.Equal(2, result.Changes);
        Assert.Equal(2, badge.Count);
    }

    [Fact]
    public async Task An_alert_offers_to_stop_notifications_here_or_everywhere_and_to_start_them_again()
    {
        var client = Fakes.Client(devices: [Fakes.Device(7, "core-sw")]);
        client.Alerts.GetAsync(1, Arg.Any<CancellationToken>()).Returns(On(1, 7));
        client.Devices.GetAsync("7", Arg.Any<CancellationToken>()).Returns(Fakes.Device(7, "core-sw"));
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ChooseAsync("Stop notifications for", Arg.Any<IReadOnlyList<string>>()).Returns("Rule 1 on core-sw");
        dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var vm = new AlertDetailViewModel(client, Fakes.Settings(), dialogs, new RecordingNavigation(), new SelfActionTracker(), _ignored);
        await vm.LoadAsync(1);
        Assert.Equal("Notifications on", vm.NotificationsText);

        await vm.ChangeNotificationsCommand.ExecuteAsync(null);

        Assert.True(_ignored.IsIgnored(On(1, 7)));
        Assert.False(_ignored.IsIgnored(On(1, 9)));
        Assert.Equal("No notifications: Rule 1 on core-sw", vm.NotificationsText);

        await vm.ChangeNotificationsCommand.ExecuteAsync(null);
        Assert.Empty(_ignored.All);
    }

    [Fact]
    public void A_rules_page_switches_its_notifications_off_everywhere_and_shows_single_devices()
    {
        var vm = new AlertRuleViewModel(Fakes.Client(), Fakes.Settings(), new RecordingNavigation(), _ignored);
        _ignored.Ignore(10, "Rule 1", 7, "core-sw");
        vm.Show(new AlertRule { Id = 10, Name = "Rule 1" }, new Dictionary<int, Device>(), [], [], [], new AppSettings());

        Assert.True(vm.Notifies);
        Assert.Equal("Not on core-sw", vm.NotificationsNote);

        vm.Notifies = false;
        Assert.True(_ignored.IsRuleIgnored(10));
        Assert.Null(vm.NotificationsNote);

        vm.Notifies = true;
        Assert.Empty(_ignored.All); // the single device too
    }

    [Fact]
    public void Settings_lists_whats_ignored_and_can_notify_again()
    {
        _ignored.Ignore(20, "Rule 2");
        var vm = new SettingsViewModel(
            Substitute.For<ISessionService>(), Fakes.Settings(), Substitute.For<IDialogService>(), new RecordingNavigation(),
            new RecordingNotifier(), null!, new NoAppBadge(), new InMemoryAppearance(), new NoHomeWidgets(), ignored: _ignored);

        Assert.True(vm.HasIgnoredAlerts);
        _ignored.Ignore(10, "Rule 1", 7, "core-sw"); // from an alert, while Settings is open
        Assert.Equal(["Rule 1 on core-sw", "Rule 2, on every device"], vm.IgnoredAlerts.Select(i => i.Description));

        vm.NotifyAgainCommand.Execute(vm.IgnoredAlerts[0]);
        Assert.Equal(["Rule 2, on every device"], vm.IgnoredAlerts.Select(i => i.Description));
    }
}
