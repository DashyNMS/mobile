using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task Counts_devices_and_active_alerts()
    {
        var client = Fakes.Client(
            devices: [Fakes.Device(1, "a"), Fakes.Device(2, "b", up: false), Fakes.Device(3, "c", ignore: true)],
            alerts:
            [
                Fakes.Alert(1, 1, "critical"),
                Fakes.Alert(2, 1, "warning"),
                Fakes.Alert(3, 2, "critical", acknowledged: true),
            ]);
        var settings = Fakes.Settings();
        var vm = new DashboardViewModel(client, settings, new RecordingNavigation(), new DeviceBookmarks(settings, TimeProvider.System));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal((1, 1, 1), (vm.DevicesUp, vm.DevicesDown, vm.DevicesInactive));
        Assert.Equal((1, 1, 1), (vm.CriticalAlerts, vm.WarningAlerts, vm.AcknowledgedAlerts));
        Assert.Equal([1, 2], vm.TopAlerts.Select(a => a.Id));
        Assert.False(vm.HasNoAlerts);
    }

    [Fact]
    public async Task Top_alerts_are_most_severe_then_newest_and_capped()
    {
        var day = new DateTime(2026, 1, 1);
        var alerts = Enumerable.Range(1, 8)
            .Select(i => Fakes.Alert(i, 1, i % 2 == 0 ? "critical" : "warning", at: day.AddHours(i)))
            .ToList();
        var settings = Fakes.Settings();
        var vm = new DashboardViewModel(Fakes.Client(alerts: alerts), settings, new RecordingNavigation(), new DeviceBookmarks(settings, TimeProvider.System));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal([8, 6, 4, 2, 7], vm.TopAlerts.Select(a => a.Id));
    }
}

public sealed class AlertsViewModelTests
{
    private readonly ILibreNmsClient _client = Fakes.Client(alerts:
    [
        Fakes.Alert(1, 1, "warning"),
        Fakes.Alert(2, 1, "critical", acknowledged: true),
        Fakes.Alert(3, 2, "critical"),
    ]);

    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly ISelfActionTracker _selfActions = Substitute.For<ISelfActionTracker>();
    private readonly IShareService _share = Substitute.For<IShareService>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 14, 30, 5, TimeSpan.Zero));

    private readonly RecordingBadge _badge = new();

    public AlertsViewModelTests()
    {
        _settings = Fakes.Settings(_appSettings);
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
    }

    private AlertsViewModel NewViewModel() =>
        new(_client, _settings, _dialogs, new RecordingNavigation(), _selfActions, _share, _badge, _time);

    [Fact]
    public async Task Alerts_name_their_device_by_the_Device_names_setting()
    {
        _appSettings.DeviceNameStyle = DeviceNameStyle.SysName;
        _client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns([Fakes.Device(1, "10.0.0.1", sysName: "core-sw-01")]);
        var vm = NewViewModel();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("core-sw-01", vm.Alerts.Single(a => a.Id == 1).Device);

        // A device LibreNMS didn't list keeps the alert's own hostname.
        Assert.Equal("host2", vm.Alerts.Single(a => a.Id == 3).Device);
    }

    private async Task<AlertsViewModel> LoadedViewModel()
    {
        var vm = NewViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Unacknowledged_first_then_by_severity()
    {
        var vm = await LoadedViewModel();

        Assert.Equal([3, 1, 2], vm.Alerts.Select(a => a.Id));
        Assert.Equal("3 alerts", vm.CountText);
        Assert.False(vm.HasActiveFilters);
    }

    [Fact]
    public async Task Loading_the_list_updates_the_app_icons_count_straight_away()
    {
        _appSettings.AlertTabBadgeIncludesAcknowledged = false;

        await LoadedViewModel();

        Assert.Equal(2, _badge.Count); // the two active alerts
    }

    [Fact]
    public async Task Severity_and_state_filters_narrow_the_list()
    {
        var vm = await LoadedViewModel();

        vm.ShowAcknowledged = false;
        Assert.Equal([3, 1], vm.Alerts.Select(a => a.Id));

        vm.ToggleCriticalCommand.Execute(null);
        Assert.Equal([1], vm.Alerts.Select(a => a.Id));
        Assert.Equal("1 of 3 alerts", vm.CountText);
        Assert.True(vm.HasActiveFilters);
    }

    [Fact]
    public async Task Alerts_with_no_severity_always_show_as_on_desktop()
    {
        _client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>())
            .Returns([Fakes.Alert(9, 1, "")]);
        var vm = await LoadedViewModel();

        vm.ShowCritical = false;
        vm.ShowWarning = false;

        Assert.Equal([9], vm.Alerts.Select(a => a.Id));
    }

    [Fact]
    public async Task Chip_counts_ignore_the_filters()
    {
        var vm = await LoadedViewModel();
        vm.ShowCritical = false;
        vm.SearchText = "nothing matches this";

        Assert.Equal((1, 1, 1), (vm.CriticalCount, vm.WarningCount, vm.AcknowledgedCount));
        Assert.Equal("No alerts match these filters.", vm.EmptyText);
    }

    [Theory]
    [InlineData("host2", new[] { 3 })]      // device
    [InlineData("RULE 1", new[] { 1 })]     // rule, any case
    [InlineData("warning", new[] { 1 })]    // severity
    [InlineData("acknowledged", new[] { 2 })] // state
    [InlineData("3", new[] { 3 })]          // alert id
    public async Task Search_looks_where_desktop_does(string term, int[] expected)
    {
        var vm = await LoadedViewModel();

        vm.SearchText = term;

        Assert.Equal(expected, vm.Alerts.Select(a => a.Id));
    }

    [Fact]
    public async Task Search_finds_notes()
    {
        var withNote = Fakes.Alert(5, 1, "warning");
        withNote.Note = "Waiting on the ISP";
        _client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()).Returns([withNote, Fakes.Alert(6, 1, "warning")]);
        var vm = await LoadedViewModel();

        vm.SearchText = "isp";

        Assert.Equal([5], vm.Alerts.Select(a => a.Id));
    }

    [Fact]
    public async Task Filters_are_remembered_in_desktops_settings()
    {
        var vm = await LoadedViewModel();

        vm.ShowWarning = false;
        vm.SearchText = "core";

        Assert.False(_appSettings.Filter.ShowWarning);
        Assert.Equal("core", _appSettings.Filter.SearchText);
        _settings.Received().Save();

        var next = NewViewModel();
        Assert.False(next.ShowWarning);
        Assert.Equal("core", next.SearchText);
    }

    [Fact]
    public async Task A_dashboard_shortcut_doesnt_replace_the_users_own_filter()
    {
        var vm = await LoadedViewModel();
        vm.ToggleAcknowledgedCommand.Execute(null); // the user turns Acknowledged off

        vm.ShowOnly("all"); // then the dashboard's See all
        Assert.True(vm.ShowAcknowledged);
        Assert.False(_appSettings.Filter.ShowAcknowledged); // still theirs (#81)

        vm.ShowSavedFilter(); // the tab opened normally again
        Assert.False(vm.ShowAcknowledged);
        Assert.False(NewViewModel().ShowAcknowledged);

        // A chip tapped on a shortcut makes that the user's filter.
        vm.ShowOnly("critical");
        vm.ToggleWarningCommand.Execute(null);
        Assert.True(_appSettings.Filter.ShowCritical);
        Assert.True(_appSettings.Filter.ShowWarning);
        Assert.False(_appSettings.Filter.ShowUnknownSeverity);
    }

    [Fact]
    public async Task Clear_brings_everything_back()
    {
        var vm = await LoadedViewModel();
        vm.ShowCritical = false;
        vm.ShowAcknowledged = false;
        vm.SearchText = "host1";

        vm.ClearFiltersCommand.Execute(null);

        Assert.Equal([3, 1, 2], vm.Alerts.Select(a => a.Id));
        Assert.False(vm.HasActiveFilters);
        Assert.Null(_appSettings.Filter.SearchText);
        Assert.True(_appSettings.Filter.ShowCritical);
    }

    [Fact]
    public async Task Export_shares_the_filtered_list_as_csv()
    {
        var formula = Fakes.Alert(7, 1, "warning");
        formula.Note = "=HYPERLINK(\"http://x\")";
        _client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()).Returns([formula, Fakes.Alert(8, 2, "critical")]);
        var vm = await LoadedViewModel();
        vm.ShowCritical = false;
        string? csv = null;
        await _share.ShareTextFileAsync(Arg.Any<string>(), Arg.Do<string>(c => csv = c), Arg.Any<string>(), Arg.Any<string>());

        await vm.ExportCsvCommand.ExecuteAsync(null);

        await _share.Received(1).ShareTextFileAsync("alerts-2026-09-28-143005.csv", Arg.Any<string>(), "text/csv", Arg.Any<string>());
        var lines = csv!.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Severity,Device,Alert,State,Age,Note", lines[0]);
        Assert.Equal(2, lines.Length); // the critical one is filtered out
        // Desktop's CsvWriter defuses spreadsheet formulas from server-supplied text.
        Assert.StartsWith("Warning,host1,Rule 7,Active,", lines[1]);
        Assert.EndsWith("\"'=HYPERLINK(\"\"http://x\"\")\"", lines[1]);
    }

    [Fact]
    public async Task Exporting_nothing_says_so_instead()
    {
        var vm = await LoadedViewModel();
        vm.SearchText = "nothing matches this";

        await vm.ExportCsvCommand.ExecuteAsync(null);

        await _share.DidNotReceiveWithAnyArgs().ShareTextFileAsync(default!, default!, default!, default!);
        await _dialogs.Received(1).AlertAsync("Nothing to export", Arg.Any<string>());
    }

    [Fact]
    public async Task Acknowledge_sends_the_trimmed_note_and_updates_its_row_in_place()
    {
        _dialogs.PromptAsync(default!, default!, default!, default!).ReturnsForAnyArgs("  on it  ");
        var vm = await LoadedViewModel();
        var before = vm.Alerts.Select(a => a.Id).ToList();
        var resets = 0;
        vm.Alerts.CollectionChanged += (_, e) => resets += e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset ? 1 : 0;

        await vm.AcknowledgeCommand.ExecuteAsync(vm.Alerts.Single(a => a.Id == 3));

        await _client.Alerts.Received(1).AcknowledgeAsync(3, "on it", true, Arg.Any<CancellationToken>());
        _selfActions.Received(1).Record(3, AlertChangeKind.Acknowledged);

        // No reload - that reset the list to the top (#93): the row changes where it is.
        await _client.Alerts.Received(1).ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>());
        Assert.Equal(0, resets);
        Assert.Equal(before, vm.Alerts.Select(a => a.Id));
        var row = vm.Alerts.Single(a => a.Id == 3);
        Assert.True(row.IsAcknowledged);
        Assert.Equal("on it", row.Note);
    }

    [Fact]
    public async Task Acknowledging_with_acknowledged_hidden_takes_just_that_row_out()
    {
        _dialogs.PromptAsync(default!, default!, default!, default!).ReturnsForAnyArgs(string.Empty);
        var vm = await LoadedViewModel();
        vm.ShowAcknowledged = false;
        var others = vm.Alerts.Where(a => a.Id != 3).Select(a => a.Id).ToList();
        var acknowledged = vm.AcknowledgedCount;

        await vm.AcknowledgeCommand.ExecuteAsync(vm.Alerts.Single(a => a.Id == 3));

        Assert.Equal(others, vm.Alerts.Select(a => a.Id));
        Assert.Equal(acknowledged + 1, vm.AcknowledgedCount); // the chip's count follows, without a reload
    }

    [Fact]
    public async Task Cancelling_the_note_prompt_acknowledges_nothing()
    {
        _dialogs.PromptAsync(default!, default!, default!, default!).ReturnsForAnyArgs((string?)null);
        var vm = await LoadedViewModel();

        await vm.AcknowledgeCommand.ExecuteAsync(vm.Alerts[0]);

        await _client.Alerts.DidNotReceiveWithAnyArgs().AcknowledgeAsync(default, default, default, default);
        _selfActions.DidNotReceiveWithAnyArgs().Record(default, default);
    }

    [Fact]
    public async Task Unacknowledge_needs_confirmation()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var vm = await LoadedViewModel();

        await vm.UnacknowledgeCommand.ExecuteAsync(vm.Alerts.Single(a => a.Id == 2));

        await _client.Alerts.Received(1).UnmuteAsync(2, null, Arg.Any<CancellationToken>());
        _selfActions.Received(1).Record(2, AlertChangeKind.Unacknowledged);
    }
}

public sealed class DeviceDetailViewModelTests
{
    [Fact]
    public async Task Shows_the_device_and_only_its_own_alerts()
    {
        var client = Fakes.Client(alerts: [Fakes.Alert(1, 7, "warning"), Fakes.Alert(2, 8, "critical"), Fakes.Alert(3, 7, "critical")]);
        client.Devices.GetAsync("7", Arg.Any<CancellationToken>()).Returns(Fakes.Device(7, "edge-rtr", ip: "192.0.2.1"));
        var settings = Fakes.Settings();
        var vm = new DeviceDetailViewModel(client, settings, Substitute.For<ILauncherService>(), new DeviceBookmarks(settings, TimeProvider.System), Substitute.For<IDialogService>(), new RecordingNavigation());

        await vm.LoadAsync(7);

        Assert.Equal("edge-rtr", vm.Title);
        Assert.Equal([3, 1], vm.Alerts.Select(a => a.Id));
        Assert.Contains(vm.Properties, p => p is { Key: "IP address", Value: "192.0.2.1" });
    }

    [Fact]
    public async Task A_deleted_device_says_so()
    {
        var client = Fakes.Client();
        client.Devices.GetAsync("7", Arg.Any<CancellationToken>()).Returns((Device?)null);
        var settings = Fakes.Settings();
        var vm = new DeviceDetailViewModel(client, settings, Substitute.For<ILauncherService>(), new DeviceBookmarks(settings, TimeProvider.System), Substitute.For<IDialogService>(), new RecordingNavigation());

        await vm.LoadAsync(7);

        Assert.True(vm.HasError);
        Assert.False(vm.TogglePinCommand.CanExecute(null));
    }
}

public sealed class SignInViewModelTests
{
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly RecordingNavigation _navigation = new();
    private readonly RecordingNotifier _notifier = new();

    private SignInViewModel NewViewModel() => new(
        _session, Fakes.Settings(), _navigation, Fakes.Secrets(), _notifier, new NotificationRouter(_session, _navigation));

    [Fact]
    public async Task Successful_sign_in_goes_to_main_and_clears_the_token_box()
    {
        _session.SignInAsync(default!, default!, default, default, default, default)
            .ReturnsForAnyArgs(ConnectionTestResult.Success(new SystemInfo()));
        var vm = NewViewModel();
        vm.ServerUrl = "nms.example.com";
        vm.ApiToken = "secret";

        await vm.SignInCommand.ExecuteAsync(null);

        Assert.Equal(Routes.Main, _navigation.Visits.Single().Route);
        Assert.Empty(vm.ApiToken);
        Assert.Equal(1, _notifier.PermissionRequests);
        await _session.Received(1).SignInAsync("nms.example.com", "secret", false, true, Arg.Any<CancellationToken>(), string.Empty);
    }

    [Fact]
    public async Task Failed_sign_in_shows_why_and_stays_put()
    {
        _session.SignInAsync(default!, default!, default, default, default, default)
            .ReturnsForAnyArgs(ConnectionTestResult.Failure("The API token was rejected.", isAuthenticationFailure: true));
        var vm = NewViewModel();
        vm.ApiToken = "bad";

        await vm.SignInCommand.ExecuteAsync(null);

        Assert.Equal("The API token was rejected.", vm.ErrorMessage);
        Assert.Empty(_navigation.Visits);
        Assert.Equal(0, _notifier.PermissionRequests);
        Assert.Equal("bad", vm.ApiToken);
    }

    [Fact]
    public async Task Restores_a_saved_session_once()
    {
        _session.TryRestoreAsync(default).ReturnsForAnyArgs(ConnectionTestResult.Success(new SystemInfo()));
        var vm = NewViewModel();

        await vm.AppearingCommand.ExecuteAsync(null);
        await vm.AppearingCommand.ExecuteAsync(null);

        Assert.Equal(Routes.Main, _navigation.Visits.Single().Route);
    }

    [Fact]
    public async Task Nothing_saved_means_no_error_and_no_navigation()
    {
        _session.TryRestoreAsync(default).ReturnsForAnyArgs((ConnectionTestResult?)null);
        var vm = NewViewModel();

        await vm.AppearingCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.Empty(_navigation.Visits);
    }

    [Fact]
    public async Task A_remembered_server_shows_signing_in_rather_than_the_form_until_restore_fails()
    {
        _session.TryRestoreAsync(default).ReturnsForAnyArgs(ConnectionTestResult.Failure("The server didn't answer."));
        var vm = new SignInViewModel(
            _session, Fakes.Settings(new AppSettings { ServerUrl = "https://nms.example.com", RememberToken = true }),
            _navigation, Fakes.Secrets(), _notifier, new NotificationRouter(_session, _navigation));

        Assert.True(vm.IsRestoring);
        Assert.False(vm.ShowForm);

        await vm.AppearingCommand.ExecuteAsync(null);

        Assert.False(vm.IsRestoring);
        Assert.True(vm.ShowForm);
        Assert.Equal("The server didn't answer.", vm.ErrorMessage);
    }

    [Fact]
    public void A_first_launch_goes_straight_to_the_form()
    {
        var vm = NewViewModel();

        Assert.False(vm.IsRestoring);
        Assert.True(vm.ShowForm);
    }
}

public sealed class FormattingTests
{
    [Theory]
    [InlineData(0, "—")]
    [InlineData(30, "1m")]
    [InlineData(3 * 3600 + 20 * 60, "3h 20m")]
    [InlineData(12 * 86400 + 4 * 3600, "12d 4h")]
    public void Uptime(long seconds, string expected) => Assert.Equal(expected, Formatting.Uptime(seconds));

    [Theory]
    [InlineData(20, "just now")]
    [InlineData(5 * 60, "5m ago")]
    [InlineData(3 * 3600, "3h ago")]
    [InlineData(2 * 86400, "2d ago")]
    public void Age(int seconds, string expected) => Assert.Equal(expected, Formatting.Age(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void An_alert_row_shows_its_age_short_at_the_right_and_its_id_and_state_below()
    {
        var item = new AlertItem(Fakes.Alert(4821, 1, "critical", at: DateTime.Now.AddMinutes(-5).AddSeconds(-10)), serverTimestampsAreUtc: false);

        Assert.Equal("5m", item.ShortAge);
        Assert.Equal("#4821 · Active", item.MetaText);
        Assert.Equal("now", new AlertItem(Fakes.Alert(1, 1, "critical", at: DateTime.Now), false).ShortAge);
    }
}
