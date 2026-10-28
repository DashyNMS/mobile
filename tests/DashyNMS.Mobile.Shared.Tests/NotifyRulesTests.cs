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

/// <summary>Which alerts notify (#167): Core's rules, in desktop's settings, in notifications only.</summary>
public sealed class NotifyRulesTests
{
    private const string Server = "https://nms.example.com/";

    private readonly InMemoryPreferences _preferences = new();
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly NotifyRules _rules;

    public NotifyRulesTests()
    {
        _settings = Fakes.Settings(_appSettings);
        _rules = new NotifyRules(_settings, _preferences);
    }

    // Fakes.Alert gives rule id * 10: alert 1 is rule 10, alert 2 rule 20.
    private static Alert On(int alertId, int deviceId) => Fakes.Alert(alertId, deviceId, "critical");

    [Fact]
    public void A_phones_old_ignored_list_moves_into_desktops_settings_once()
    {
        var preferences = new InMemoryPreferences();
        preferences.Set(NotifyRules.OldKey, """[{"RuleId":10,"RuleName":"Rule 1","DeviceId":7,"DeviceName":"core-sw"},{"RuleId":20,"RuleName":"Rule 2","DeviceId":null,"DeviceName":null}]""");
        var settings = new AppSettings();

        var rules = new NotifyRules(Fakes.Settings(settings), preferences);

        Assert.Equal(NotificationRuleMode.AllExcept, rules.Mode);
        Assert.Equal(["Rule 1 on core-sw", "Rule 2, on every device"], settings.Notifications.ExceptRules.Select(e => e.Description));
        Assert.Null(preferences.Get(NotifyRules.OldKey));
        Assert.False(rules.Allows(On(1, 7)));
        Assert.True(rules.Allows(On(1, 9)));
    }

    [Fact]
    public void An_unreadable_old_list_is_dropped()
    {
        var preferences = new InMemoryPreferences();
        preferences.Set(NotifyRules.OldKey, "{not json");

        var rules = new NotifyRules(_settings, preferences);

        Assert.Equal(NotificationRuleMode.All, rules.Mode);
        Assert.Null(preferences.Get(NotifyRules.OldKey));
    }

    [Fact]
    public void Each_mode_keeps_its_own_list()
    {
        _rules.Mode = NotificationRuleMode.AllExcept;
        _rules.AddEverywhere(10, "Rule 1");
        _rules.Mode = NotificationRuleMode.Only;
        _rules.AddEverywhere(20, "Rule 2");

        _rules.Mode = NotificationRuleMode.AllExcept;

        Assert.Equal(["Rule 1, on every device"], _rules.Listed.Select(e => e.Description));
        Assert.Equal(["Rule 2, on every device"], _appSettings.Notifications.OnlyRules.Select(e => e.Description));
    }

    [Fact]
    public void Choices_on_an_alert_keep_the_mode_and_read_as_desktops()
    {
        var choices = _rules.ChoicesFor(10, 7).Select(c => NotificationRuleChoices.Describe(c)).ToList();

        Assert.Equal(["Don't notify me about this rule on this device", "Don't notify me about this rule on any device"], choices);

        _rules.Mode = NotificationRuleMode.Only;
        Assert.Equal(["Notify me about this rule on this device", "Notify me about this rule on every device"],
            _rules.ChoicesFor(10, 7).Select(c => NotificationRuleChoices.Describe(c)));
    }

    [Fact]
    public void Undo_puts_the_mode_and_lists_back()
    {
        var before = _rules.Apply(NotificationRuleAction.LeaveOutOnDevice, 10, "Rule 1", 7, "core-sw");
        Assert.Equal(NotificationRuleMode.AllExcept, _rules.Mode);
        Assert.Equal("Not notified: Rule 1 on core-sw", _rules.StatusFor(On(1, 7)));

        _rules.Restore(before);

        Assert.Equal(NotificationRuleMode.All, _rules.Mode);
        Assert.Empty(_appSettings.Notifications.ExceptRules);
        Assert.Equal("Notifies you", _rules.StatusFor(On(1, 7)));
    }

    [Theory]
    [InlineData(NotificationRuleMode.All, "Notifies you")]
    [InlineData(NotificationRuleMode.AllExcept, "Notifies you, except on core-sw")]
    [InlineData(NotificationRuleMode.Only, "Notifies you on core-sw only")]
    public void A_rules_line_says_where_it_notifies(NotificationRuleMode mode, string expected)
    {
        NotificationRuleList.Add(_appSettings.Notifications.ExceptRules, 10, "Rule 1", 7, "core-sw");
        NotificationRuleList.Add(_appSettings.Notifications.OnlyRules, 10, "Rule 1", 7, "core-sw");
        _rules.Mode = mode;

        Assert.Equal(expected, _rules.StatusForRule(10));
    }

    [Fact]
    public async Task A_left_out_alert_doesnt_notify_but_still_counts_on_the_badge()
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
            client, session, Fakes.Secrets(), _settings, store, new SelfActionTracker(), notifier, badge,
            new RecordingWidgets { IsInUse = false }, time, NullLogger<AlertWatcher>.Instance, rules: _rules);

        store.State = new AlertWatchState(Server, AlertChangeDetector.Snapshot([]));
        client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()).Returns([On(1, 7), On(2, 7)]);
        _rules.Apply(NotificationRuleAction.LeaveOutOnDevice, 10, "Rule 1", 7, "core-sw");

        var result = await watcher.CheckAsync();

        Assert.Equal("alert-2", Assert.Single(notifier.Shown).Tag);
        Assert.Equal(2, result.Changes);
        Assert.Equal(1, result.Ignored);
        Assert.Equal(2, badge.Count);
    }

    [Fact]
    public async Task Only_the_chosen_rules_notify_in_Only_mode()
    {
        var client = Fakes.Client();
        var session = Substitute.For<ISessionService>();
        session.IsConnected.Returns(true);
        session.Connection.Returns(new LibreNmsConnection(new Uri(Server), "token"));
        var store = new InMemoryWatchStore();
        var notifier = new RecordingNotifier();
        var watcher = new AlertWatcher(
            client, session, Fakes.Secrets(), _settings, store, new SelfActionTracker(), notifier, new RecordingBadge(),
            new RecordingWidgets { IsInUse = false }, new FakeTimeProvider(), NullLogger<AlertWatcher>.Instance, rules: _rules);

        store.State = new AlertWatchState(Server, AlertChangeDetector.Snapshot([]));
        client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()).Returns([On(1, 7), On(2, 7)]);
        _rules.Mode = NotificationRuleMode.Only;
        _rules.AddEverywhere(20, "Rule 2");

        await watcher.CheckAsync();

        Assert.Equal("alert-2", Assert.Single(notifier.Shown).Tag);
    }

    [Fact]
    public async Task An_alert_changes_its_notifications_with_Undo()
    {
        var client = Fakes.Client(devices: [Fakes.Device(7, "core-sw")]);
        client.Alerts.GetAsync(1, Arg.Any<CancellationToken>()).Returns(On(1, 7));
        client.Devices.GetAsync("7", Arg.Any<CancellationToken>()).Returns(Fakes.Device(7, "core-sw", sysName: "core-sw"));
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns("Don't notify me about this rule on this device");
        var vm = new AlertDetailViewModel(client, _settings, dialogs, new RecordingNavigation(), new SelfActionTracker(), _rules);
        await vm.LoadAsync(1);
        Assert.Equal("Notifies you", vm.NotificationsText);

        await vm.ChangeNotificationsCommand.ExecuteAsync(null);

        await dialogs.Received(1).ChooseAsync("Notifications for Rule 1 on core-sw", Arg.Any<IReadOnlyList<string>>());
        Assert.False(_rules.Allows(On(1, 7)));
        Assert.True(_rules.Allows(On(1, 9)));
        Assert.Equal("Not notified: Rule 1 on core-sw", vm.NotificationsText);
        Assert.Equal("Not notified: Rule 1 on core-sw", vm.Toast.Message);

        vm.Toast.UndoCommand.Execute(null);

        Assert.True(_rules.Allows(On(1, 7)));
        Assert.Equal("Notifies you", vm.NotificationsText);
        Assert.False(vm.Toast.IsShowing);
    }

    [Fact]
    public async Task A_rules_page_offers_the_rule_wide_choices()
    {
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns("Don't notify me about this rule");
        var vm = new AlertRuleViewModel(Fakes.Client(), _settings, new RecordingNavigation(), _rules, dialogs);
        vm.Show(new AlertRule { Id = 10, Name = "Rule 1" }, new Dictionary<int, Device>(), [], [], [], new AppSettings());

        await vm.ChangeNotificationsCommand.ExecuteAsync(null);

        await dialogs.Received(1).ChooseAsync("Notifications for Rule 1", Arg.Is<IReadOnlyList<string>>(l => l.Count == 1 && l[0] == "Don't notify me about this rule"));
        Assert.Equal("Not notified on any device", vm.NotificationsText);
    }

    [Fact]
    public void Settings_switches_modes_and_removes_rules_with_Undo()
    {
        var vm = new SettingsViewModel(
            Substitute.For<ISessionService>(), _settings, Substitute.For<IDialogService>(), new RecordingNavigation(),
            new RecordingNotifier(), null!, new NoAppBadge(), new InMemoryAppearance(), new NoHomeWidgets(), notifyRules: _rules);
        Assert.True(vm.IsEveryAlert);
        Assert.False(vm.HasNotifyRuleList);

        vm.SetNotifyModeCommand.Execute(NotificationRuleMode.Only);
        Assert.True(vm.IsOnlyEmpty);
        Assert.Equal("None chosen yet", vm.OnlyRulesDetail);

        vm.SetNotifyModeCommand.Execute(NotificationRuleMode.AllExcept);
        _rules.Apply(NotificationRuleAction.LeaveOutOnDevice, 10, "Rule 1", 7, "core-sw"); // from an alert, while Settings is open
        Assert.Equal("NOT NOTIFIED (1)", vm.NotifyListTitle);
        Assert.Equal("1 rule left out", vm.AllExceptDetail);

        vm.RemoveNotifyRuleCommand.Execute(vm.NotifyRuleList[0]);
        Assert.Empty(vm.NotifyRuleList);
        Assert.Equal("Rule 1 on core-sw removed", vm.Toast.Message);

        vm.Toast.UndoCommand.Execute(null);
        Assert.Equal(["Rule 1 on core-sw"], vm.NotifyRuleList.Select(e => e.Description));
    }

    [Fact]
    public async Task Add_a_rule_adds_it_on_every_device_and_marks_whats_there()
    {
        var client = Fakes.Client();
        client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new AlertRule { Id = 10, Name = "Rule 1", SeverityText = "critical" },
            new AlertRule { Id = 20, Name = "Rule 2", SeverityText = "warning" },
            new AlertRule { Id = 30, Name = "Rule 3", SeverityText = "warning" },
        ]);
        _rules.Apply(NotificationRuleAction.LeaveOutEverywhere, 20, "Rule 2");
        _rules.Apply(NotificationRuleAction.LeaveOutOnDevice, 30, "Rule 3", 7, "core-sw");
        var navigation = new RecordingNavigation();
        var vm = new NotifyRulePickerViewModel(client, _rules, navigation);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["Critical", "Already on the list, on every device", "On the list for core-sw · adding covers every device"], vm.Rules.Select(r => r.Detail));
        Assert.False(vm.Rules[1].CanAdd);

        await vm.ChooseCommand.ExecuteAsync(vm.Rules[2]);

        Assert.Equal(["Rule 2, on every device", "Rule 3, on every device"], _rules.Listed.Select(e => e.Description));
        Assert.Equal(Routes.Back, Assert.Single(navigation.Visits).Route);
    }
}
