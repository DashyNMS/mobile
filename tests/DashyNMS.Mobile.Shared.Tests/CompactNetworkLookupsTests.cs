using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class CompactNetworkLookupsTests
{
    private static ISessionService Session()
    {
        var session = Substitute.For<ISessionService>();
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "test-token"));
        return session;
    }

    [Fact]
    public async Task Each_lookup_returns_identified_data_within_the_budget()
    {
        var devices = Enumerable.Range(1, 5000).Select(i => Fakes.Device(i, $"switch-{i}.example.net")).ToList();
        var alerts = Enumerable.Range(1, 3000).Select(i => Fakes.Alert(i, 1, "critical")).ToList();
        var client = Fakes.Client(devices, alerts);
        client.Devices.GetAsync("1", Arg.Any<CancellationToken>()).Returns(devices[0]);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(Enumerable.Range(1, 2000)
            .Select(i => new Sensor { SensorId = i, DeviceId = 1, Description = new string('x', 500), Current = 20, SensorClass = "temperature" }).ToList());
        var lookups = new CompactNetworkLookups(client, Fakes.Settings(), Session()).Create();

        Assert.Equal(5, lookups.Count);
        foreach (var lookup in lookups)
        {
            var query = lookup.Name == "find_device" ? "switch" : lookup.Name == "network_overview" ? "" : "1";
            var text = await lookup.Run(query, default);
            Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
            Assert.NotEqual(CompactNetworkLookups.Failed, text);
            Assert.Contains(lookup.Name == "device_sensors" ? "sensor 1" : lookup.Name is "active_alerts" or "network_overview" ? "alert 1" : "device 1", text);
        }
    }

    [Fact]
    public async Task Unacknowledged_warning_precedes_acknowledged_critical()
    {
        var client = Fakes.Client(alerts: [Fakes.Alert(1, 1, "critical", acknowledged: true), Fakes.Alert(2, 1, "warning")]);
        var lookups = new CompactNetworkLookups(client, Fakes.Settings(), Session()).Create();
        foreach (var lookup in lookups.Where(l => l.Name is "network_overview" or "active_alerts"))
        {
            var text = await lookup.Run("", default);
            Assert.True(text.IndexOf("alert 2", StringComparison.Ordinal) < text.IndexOf("alert 1", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Failures_do_not_expose_exception_details()
    {
        var client = Fakes.Client();
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Device>>(new HttpRequestException("private-server secret-token")));
        var lookup = new CompactNetworkLookups(client, Fakes.Settings(), Session()).Create().Single(l => l.Name == "find_device");
        Assert.Equal(CompactNetworkLookups.Failed, await lookup.Run("switch", default));
    }

    [Fact]
    public async Task A_timeout_bounds_a_transport_which_ignores_cancellation()
    {
        var time = new FakeTimeProvider();
        var client = Fakes.Client();
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<IReadOnlyList<Device>>().Task);
        var lookup = new CompactNetworkLookups(client, Fakes.Settings(), Session(), time).Create().Single(l => l.Name == "find_device");
        var pending = lookup.Run("switch", default);
        time.Advance(CompactNetworkLookups.Timeout);
        Assert.Equal(CompactNetworkLookups.Failed, await pending);
    }

    [Fact]
    public async Task Cancellation_is_not_reported_as_a_lookup_failure()
    {
        var client = Fakes.Client();
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<IReadOnlyList<Device>>().Task);
        var lookup = new CompactNetworkLookups(client, Fakes.Settings(), Session()).Create().Single(l => l.Name == "find_device");
        using var stopping = new CancellationTokenSource();
        var pending = lookup.Run("switch", stopping.Token);
        stopping.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task A_connection_switch_discards_old_data_and_requires_a_fresh_lookup_set()
    {
        var client = Fakes.Client();
        var response = new TaskCompletionSource<IReadOnlyList<Device>>();
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(response.Task);
        var session = Session();
        var provider = new CompactNetworkLookups(client, Fakes.Settings(), session);
        var lookup = provider.Create().Single(l => l.Name == "find_device");
        var pending = lookup.Run("switch", default);
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://new.example.net/"), "new-test-token"));
        response.SetResult([Fakes.Device(1, "old-switch")]);
        Assert.Equal(CompactNetworkLookups.Changed, await pending);
        Assert.Equal(CompactNetworkLookups.Changed, await lookup.Run("switch", default));
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { Fakes.Device(2, "new-switch") });
        var fresh = provider.Create().Single(l => l.Name == "find_device");
        Assert.Contains("new-switch", await fresh.Run("switch", default));
    }

    [Theory]
    [InlineData("device_summary")]
    [InlineData("device_sensors")]
    [InlineData("active_alerts")]
    public async Task Invalid_device_ids_do_not_make_requests(string name)
    {
        var client = Fakes.Client();
        var lookup = new CompactNetworkLookups(client, Fakes.Settings(), Session()).Create().Single(l => l.Name == name);
        Assert.Contains("numeric device id", await lookup.Run("-1", default));
        await client.Devices.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
        await client.Alerts.DidNotReceive().ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>());
        await client.Sensors.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }
}
