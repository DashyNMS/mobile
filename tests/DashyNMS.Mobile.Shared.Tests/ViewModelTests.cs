using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class DevicesViewModelTests
{
    private readonly DevicesViewModel _vm = new(
        Fakes.Client(devices:
        [
            Fakes.Device(1, "core-sw", ip: "10.0.0.1"),
            Fakes.Device(2, "access-sw", up: false),
            Fakes.Device(3, "backup-rtr", disabled: true),
            Fakes.Device(4, "Branch-fw", ip: "10.9.0.1"),
        ]),
        new RecordingNavigation());

    [Fact]
    public async Task Down_devices_come_first_then_by_name()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["access-sw", "backup-rtr", "Branch-fw", "core-sw"], _vm.Devices.Select(d => d.Name));
        Assert.Equal("4 devices", _vm.CountText);
    }

    [Fact]
    public async Task Search_matches_name_or_ip_ignoring_case()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);

        _vm.SearchText = "BRANCH";
        Assert.Equal(["Branch-fw"], _vm.Devices.Select(d => d.Name));

        _vm.SearchText = "10.0.";
        Assert.Equal(["core-sw"], _vm.Devices.Select(d => d.Name));
        Assert.Equal("1 of 4 devices", _vm.CountText);
    }

    [Fact]
    public async Task Filter_by_state()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);

        _vm.Filter = DeviceFilter.Down;
        Assert.Equal(["access-sw"], _vm.Devices.Select(d => d.Name));

        _vm.Filter = DeviceFilter.Up;
        Assert.Equal(["Branch-fw", "core-sw"], _vm.Devices.Select(d => d.Name));
    }

    [Fact]
    public async Task A_failed_load_becomes_an_error_message()
    {
        var client = Fakes.Client();
        client.Devices.ListAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Device>>(_ => throw new HttpRequestException("no route"));
        var vm = new DevicesViewModel(client, new RecordingNavigation());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.IsBusy);
    }
}

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
        var vm = new DashboardViewModel(client, Fakes.Settings(), new RecordingNavigation());

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
        var vm = new DashboardViewModel(Fakes.Client(alerts: alerts), Fakes.Settings(), new RecordingNavigation());

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

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private readonly ISelfActionTracker _selfActions = Substitute.For<ISelfActionTracker>();

    private AlertsViewModel NewViewModel() => new(_client, Fakes.Settings(), _dialogs, new RecordingNavigation(), _selfActions);

    [Fact]
    public async Task Unacknowledged_first_then_by_severity()
    {
        var vm = NewViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal([3, 1, 2], vm.Alerts.Select(a => a.Id));

        vm.HideAcknowledged = true;
        Assert.Equal([3, 1], vm.Alerts.Select(a => a.Id));
    }

    [Fact]
    public async Task Acknowledge_sends_the_trimmed_note_and_reloads()
    {
        _dialogs.PromptAsync(default!, default!, default!, default!).ReturnsForAnyArgs("  on it  ");
        var vm = NewViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.AcknowledgeCommand.ExecuteAsync(vm.Alerts.Single(a => a.Id == 3));

        await _client.Alerts.Received(1).AcknowledgeAsync(3, "on it", true, Arg.Any<CancellationToken>());
        _selfActions.Received(1).Record(3, AlertChangeKind.Acknowledged);
        await _client.Alerts.Received(2).ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancelling_the_note_prompt_acknowledges_nothing()
    {
        _dialogs.PromptAsync(default!, default!, default!, default!).ReturnsForAnyArgs((string?)null);
        var vm = NewViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.AcknowledgeCommand.ExecuteAsync(vm.Alerts[0]);

        await _client.Alerts.DidNotReceiveWithAnyArgs().AcknowledgeAsync(default, default, default, default);
        _selfActions.DidNotReceiveWithAnyArgs().Record(default, default);
    }

    [Fact]
    public async Task Unacknowledge_needs_confirmation()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        var vm = NewViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);

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
        var vm = new DeviceDetailViewModel(client, Fakes.Settings(), Substitute.For<ILauncherService>());

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
        var vm = new DeviceDetailViewModel(client, Fakes.Settings(), Substitute.For<ILauncherService>());

        await vm.LoadAsync(7);

        Assert.True(vm.HasError);
        Assert.False(vm.OpenInBrowserCommand.CanExecute(null));
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
}
