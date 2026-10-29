using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class CorePollingTests
{
    [Fact]
    public async Task Widget_and_page_share_Core_device_cache()
    {
        var client = Fakes.Client([Fakes.Device(7, "example.net")]);
        var session = Substitute.For<ISessionService>();
        session.IsConnected.Returns(true);
        using var polling = new CorePolling(client, session, Fakes.Settings());
        Assert.Single(await polling.DevicesAsync());
        Assert.Single((await polling.CachedDevicesAsync())!);
        await client.Devices.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Switching_session_drops_the_previous_device_cache()
    {
        var client = Fakes.Client([Fakes.Device(7, "first.example.net")]);
        var session = Substitute.For<ISessionService>();
        session.IsConnected.Returns(true);
        using var polling = new CorePolling(client, session, Fakes.Settings());
        Assert.Single((await polling.CachedDevicesAsync())!);
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Device>());
        session.StateChanged += Raise.Event();
        Assert.Empty((await polling.CachedDevicesAsync())!);
    }

    [Fact]
    public async Task Rule_details_share_one_bulk_lookup_and_clear_on_session_change()
    {
        var client = Fakes.Client();
        client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { new AlertRule { Id = 7, Name = "Example rule" } });
        var session = Substitute.For<ISessionService>();
        using var polling = new CorePolling(client, session, Fakes.Settings());
        Assert.NotNull(await polling.Rules.GetRuleAsync(7));
        Assert.NotNull(await polling.Rules.GetRuleAsync(7));
        await client.Rules.Received(1).ListAsync(Arg.Any<CancellationToken>());
        session.StateChanged += Raise.Event();
        await polling.Rules.GetRuleAsync(7);
        await client.Rules.Received(2).ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Signed_out_requests_fail_without_starting_a_monitor()
    {
        using var polling = new CorePolling(Fakes.Client(), Substitute.For<ISessionService>(), Fakes.Settings());
        await Assert.ThrowsAsync<InvalidOperationException>(() => polling.DevicesAsync());
    }

    private static (ILibreNmsClient Client, CorePolling Polling) SensorPolling()
    {
        var client = Fakes.Client([Fakes.Device(1, "example.net")]);
        var session = Substitute.For<ISessionService>();
        session.IsConnected.Returns(true);
        return (client, new CorePolling(client, session, Fakes.Settings()));
    }

    private static Sensor Reading(double current) =>
        new() { SensorId = 1, DeviceId = 1, SensorClass = "temperature", Description = "Inlet", Current = current };

    [Fact]
    public async Task Two_quick_sensor_refreshes_send_one_request()
    {
        var (client, polling) = SensorPolling();
        using var _ = polling;
        var slow = new TaskCompletionSource<IReadOnlyList<Sensor>>();
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(slow.Task);

        var first = polling.SensorsAsync();
        var second = polling.SensorsAsync();
        slow.SetResult([Reading(30)]);

        Assert.Same(await first, await second);
        await client.Sensors.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_slow_sensor_list_doesnt_hold_up_the_device_list()
    {
        var (client, polling) = SensorPolling();
        using var _ = polling;
        var slow = new TaskCompletionSource<IReadOnlyList<Sensor>>();
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(slow.Task);

        var sensors = polling.SensorsAsync();
        Assert.Single(await polling.DevicesAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.False(sensors.IsCompleted);
        slow.SetResult([]);
        await sensors;
    }

    [Fact]
    public async Task A_caller_giving_up_doesnt_cancel_the_shared_sensor_request()
    {
        var (client, polling) = SensorPolling();
        using var _ = polling;
        var slow = new TaskCompletionSource<IReadOnlyList<Sensor>>();
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(slow.Task);
        using var leaving = new CancellationTokenSource();

        var gone = polling.SensorsAsync(leaving.Token);
        var staying = polling.SensorsAsync();
        leaving.Cancel();
        slow.SetResult([Reading(30)]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gone);
        Assert.True((await staying).Succeeded);
    }

    [Fact]
    public async Task A_sensor_list_timeout_keeps_the_last_readings_and_their_time()
    {
        var (client, polling) = SensorPolling();
        using var _ = polling;
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns([Reading(30)]);
        var good = await polling.SensorsAsync();
        client.Sensors.ListAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Sensor>>(_ => throw new LibreNmsApiException("Timed out") { IsTimeout = true });

        var result = await polling.SensorsAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(30, Assert.Single(result.Sensors).Current);
        Assert.Same(result, polling.LastSensors);
        Assert.Equal(
            $"LibreNMS took too long to send the sensor list - showing readings from {good.ObservedAt!.Value.LocalDateTime:HH:mm}.",
            SensorRefreshStatus.Describe(result));
    }
}
