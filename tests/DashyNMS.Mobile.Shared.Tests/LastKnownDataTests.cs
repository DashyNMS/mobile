using System.Net;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;

namespace DashyNMS.Mobile.Tests;

/// <summary>The last known data when the server can't be reached (#130).</summary>
public sealed class LastKnownDataTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"dashynms-{Guid.NewGuid():N}");
    private readonly AppSettings _appSettings = new() { ServerUrl = "https://nms.example.net/" };
    private readonly LastKnownData _lastKnown;

    public LastKnownDataTests() =>
        _lastKnown = new LastKnownData(_folder, Fakes.Settings(_appSettings), NullLogger<LastKnownData>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static Task<IReadOnlyList<Device>> Unreachable() =>
        Task.FromException<IReadOnlyList<Device>>(new HttpRequestException("No route to host"));

    [Fact]
    public async Task With_the_server_out_of_reach_the_last_good_list_is_shown_and_said_to_be()
    {
        IReadOnlyList<Device> fresh = [Fakes.Device(1, "core-sw-01"), Fakes.Device(2, "edge-rtr-01")];
        await _lastKnown.DevicesAsync(() => Task.FromResult(fresh));
        Assert.False(_lastKnown.IsShowingSaved);

        var shown = await _lastKnown.DevicesAsync(Unreachable);

        Assert.Equal(["core-sw-01", "edge-rtr-01"], shown.Select(d => d.Hostname));
        Assert.True(_lastKnown.IsShowingSaved);
        Assert.StartsWith("Offline: last updated ", _lastKnown.OfflineText);

        // Back as soon as the server answers.
        await _lastKnown.DevicesAsync(() => Task.FromResult(fresh));
        Assert.False(_lastKnown.IsShowingSaved);
        Assert.Null(_lastKnown.OfflineText);
    }

    [Fact]
    public async Task Nothing_saved_or_another_server_is_the_error_as_it_was()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => _lastKnown.DevicesAsync(Unreachable));

        await _lastKnown.DevicesAsync(() => Task.FromResult<IReadOnlyList<Device>>([Fakes.Device(1, "core-sw-01")]));
        _appSettings.ServerUrl = "https://other.example.net/";
        await Assert.ThrowsAsync<HttpRequestException>(() => _lastKnown.DevicesAsync(Unreachable));
    }

    [Fact]
    public async Task A_refusal_is_never_covered_up_with_the_saved_copy()
    {
        await _lastKnown.DevicesAsync(() => Task.FromResult<IReadOnlyList<Device>>([Fakes.Device(1, "core-sw-01")]));

        await Assert.ThrowsAsync<LibreNmsApiException>(() => _lastKnown.DevicesAsync(() =>
            Task.FromException<IReadOnlyList<Device>>(new LibreNmsApiException("LibreNMS rejected the API token.", HttpStatusCode.Unauthorized))));
        Assert.False(_lastKnown.IsShowingSaved);
    }

    [Fact]
    public async Task Saved_alerts_keep_whether_they_were_faults()
    {
        var fault = Fakes.Alert(5, 3, "critical");
        fault.Source = AlertSource.Fault;
        await _lastKnown.AlertsAsync(() => Task.FromResult<IReadOnlyList<Alert>>([fault]));

        var shown = await _lastKnown.AlertsAsync(() => Task.FromException<IReadOnlyList<Alert>>(new TimeoutException()));

        Assert.Equal(new AlertKey(AlertSource.Fault, 5), shown.Single().Identity);
        Assert.Equal(AlertSeverity.Critical, shown.Single().Severity);
    }

    [Fact]
    public async Task Signing_out_wipes_it_and_no_token_is_ever_saved()
    {
        await _lastKnown.DevicesAsync(() => Task.FromResult<IReadOnlyList<Device>>([Fakes.Device(1, "core-sw-01")]));
        var written = string.Concat(Directory.GetFiles(_folder, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain("token", written, StringComparison.OrdinalIgnoreCase);

        var session = new SessionService(Substitute.For<ILibreNmsClient>(), Fakes.Settings(_appSettings), Substitute.For<ITokenProtector>(),
            NullLogger<SessionService>.Instance, _lastKnown);
        session.SignOut(forgetToken: false);

        Assert.False(_lastKnown.HasSaved());
        Assert.False(Directory.Exists(Path.Combine(_folder, "last-known")));
    }

    [Fact]
    public async Task Out_of_reach_at_launch_with_a_saved_copy_opens_on_it()
    {
        await _lastKnown.DevicesAsync(() => Task.FromResult<IReadOnlyList<Device>>([Fakes.Device(1, "core-sw-01")]));
        var client = Substitute.For<ILibreNmsClient>();
        client.TestAsync(Arg.Any<LibreNmsConnection>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionTestResult.Failure("Couldn't reach nms.example.net."));
        var tokens = Substitute.For<ITokenProtector>();
        tokens.Load().Returns("0123456789abcdef0123456789ABCDEF");
        var session = new SessionService(client, Fakes.Settings(_appSettings), tokens, NullLogger<SessionService>.Instance, _lastKnown);

        var result = await session.TryRestoreAsync();

        Assert.True(result!.Succeeded);
        client.Received(1).Connect(Arg.Is<LibreNmsConnection>(c => c.WebRoot == new Uri("https://nms.example.net/")), false);

        // A rejected token still asks to sign in again, saved copy or not.
        client.TestAsync(Arg.Any<LibreNmsConnection>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionTestResult.Failure("LibreNMS rejected the API token.", isAuthenticationFailure: true));
        Assert.False((await session.TryRestoreAsync())!.Succeeded);
    }

    [Fact]
    public async Task The_dashboard_opens_offline_on_the_saved_lists()
    {
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw-01", up: false)], alerts: [Fakes.Alert(1, 1, "critical")]);
        var settings = Fakes.Settings(_appSettings);
        var vm = new ViewModels.DashboardViewModel(client, settings, new RecordingNavigation(), new DeviceBookmarks(settings, TimeProvider.System), lastKnown: _lastKnown);
        Dashboard.DashboardLayout.Add(_appSettings, Dashboard.DashboardLayout.DeviceStatus);
        await vm.RefreshCommand.ExecuteAsync(null);

        client.Devices.ListAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("No route to host"));
        client.Alerts.ListAsync(Arg.Any<AlertQuery>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("No route to host"));
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.Equal(1, vm.DevicesDown);
        Assert.Equal(1, vm.CriticalAlerts);
        Assert.True(vm.Offline!.IsShowingSaved);
    }
}
