using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// The sensor reads the connection-bound client allows the shared sensor operation (#603): the
/// network-wide list and the device's sensor health routes, nothing else of the transport.
/// </summary>
public sealed class ConnectionBoundSensorTests
{
    private static readonly LibreNmsConnection Captured = new(new Uri("https://nms.example.net/"), "test-token");

    [Theory]
    [InlineData("devices/1/health")]
    [InlineData("devices/12/health/device_temperature")]
    [InlineData("devices/12/health/device_dbm/4501")]
    [InlineData("devices/12/health/device_power_consumed/7")]
    public void Sensor_health_routes_are_allowed(string path) => Assert.True(ConnectionBoundClient.IsSensorRoute(path));

    [Theory]
    [InlineData("devices/1/health/device_processor")]
    [InlineData("devices/1/health/device_processor/5")]
    [InlineData("devices/1/health/device_mempool/7")]
    [InlineData("devices/1/health/device_storage")]
    [InlineData("devices/1/health/processor")]
    [InlineData("devices/1")]
    [InlineData("devices/1/ports")]
    [InlineData("devices/0/health")]
    [InlineData("devices/-1/health")]
    [InlineData("devices/1/health?x=1")]
    [InlineData("devices/1/health/device_temperature/1/extra")]
    [InlineData("devices/1/health/../../system")]
    [InlineData("devices/1/health/device_temperature/")]
    [InlineData("resources/sensors")]
    [InlineData("alerts/1")]
    [InlineData("")]
    public void Every_other_path_is_refused(string path) => Assert.False(ConnectionBoundClient.IsSensorRoute(path));

    [Fact]
    public async Task The_sensor_transport_reads_only_sensor_routes_over_the_captured_connection_and_never_writes()
    {
        var transport = new Recording();
        using var bound = new ConnectionBoundClient(new SessionScope(0, Captured), _ => transport, sensors: true);
        var routes = bound.SensorTransport;

        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => routes.GetCollectionAsync<Sensor>("devices/1/health/device_processor/5", "graphs"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => routes.GetCollectionAsync<Sensor>("devices/1/ports", "ports"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => routes.SendAsync(HttpMethod.Get, "devices/1/health"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => routes.SendAsync(HttpMethod.Post, "devices/1/health", new { }));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => routes.SendRawAsync("devices/1/health"));
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => routes.Connection);
        // Refusals happen before anything is dialled.
        Assert.False(bound.IsOpen);
        Assert.Empty(transport.Requests);

        await routes.GetCollectionAsync<Sensor>("devices/1/health/device_temperature/9", "graphs");
        await bound.Sensors.ListAsync();
        Assert.Equal(["devices/1/health/device_temperature/9", "resources/sensors"], transport.Requests);

        bound.Dispose();
        Assert.Throws<ConnectionBoundClient.RetiredException>(() => bound.SensorTransport);
        Assert.Throws<ConnectionBoundClient.RetiredException>(() => bound.Sensors);
        await Assert.ThrowsAsync<ConnectionBoundClient.RetiredException>(() => routes.GetCollectionAsync<Sensor>("devices/1/health", "graphs"));
        Assert.Equal(2, transport.Requests.Count);
    }

    [Fact]
    public async Task A_transport_for_another_server_is_never_used()
    {
        var other = new Recording(new LibreNmsConnection(new Uri("https://other.example.net/"), "other-test-token"));
        using var bound = new ConnectionBoundClient(new SessionScope(0, Captured), _ => other, sensors: true);

        await Assert.ThrowsAsync<ConnectionBoundClient.RetiredException>(() => bound.SensorTransport.GetCollectionAsync<Sensor>("devices/1/health", "graphs"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RetiredException>(() => bound.Sensors.ListAsync());
        Assert.Empty(other.Requests);
    }

    [Fact]
    public void Each_profile_keeps_to_its_own_reads()
    {
        var transport = new Recording();
        using var search = new ConnectionBoundClient(new SessionScope(0, Captured), _ => transport);
        using var overview = new ConnectionBoundClient(new SessionScope(0, Captured), _ => transport, overview: true);
        using var sensors = new ConnectionBoundClient(new SessionScope(0, Captured), _ => transport, sensors: true);

        // Only the sensor profile reads sensors.
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => search.Sensors);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => search.SensorTransport);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => overview.Sensors);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => overview.SensorTransport);
        // And the sensor profile reads nothing of alerts, ARP, groups, ports or the system.
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => sensors.Alerts);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => sensors.Arp);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => sensors.DeviceGroups);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => sensors.Ports);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => sensors.System);
        Assert.Throws<ArgumentException>(() => new ConnectionBoundClient(new SessionScope(0, Captured), _ => transport, overview: true, sensors: true));
        Assert.Empty(transport.Requests);
    }

    private sealed class Recording(LibreNmsConnection? connection = null) : ILibreNmsTransport
    {
        public List<string> Requests { get; } = [];

        public LibreNmsConnection? Connection { get; } = connection ?? Captured;

        public Task<IReadOnlyList<T>> GetCollectionAsync<T>(string relativeUrl, string collectionProperty, CancellationToken cancellationToken = default)
        {
            Requests.Add(relativeUrl);
            return Task.FromResult<IReadOnlyList<T>>([]);
        }

        public Task<System.Text.Json.JsonDocument> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Not expected");

        public Task<string> SendRawAsync(string relativeUrl, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Not expected");
    }
}
