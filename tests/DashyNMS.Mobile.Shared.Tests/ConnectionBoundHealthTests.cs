using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class ConnectionBoundHealthTests
{
    private static readonly LibreNmsConnection Captured = new(new Uri("https://nms.example.net/"), "synthetic-token");

    [Fact]
    public async Task Health_reads_only_its_device_resources_on_the_captured_transport()
    {
        var transport = new Recording();
        using var client = new ConnectionBoundClient(new SessionScope(0, Captured), _ => transport, health: true);
        await client.Devices.ListAsync();
        await client.Devices.GetAsync("1");
        await client.Devices.IsUnderMaintenanceAsync(1);
        await client.Health.ListProcessorsAsync(1);
        await client.Health.ListMempoolsAsync(1);
        await client.Health.ListStorageAsync(1);
        await client.Ports.ListForDeviceAsync(1);
        Assert.Equal(new[] { "devices", "devices/1", "devices/1/maintenance", "devices/1/health/processor",
            "devices/1/health/mempool", "devices/1/health/storage", "devices/1/ports" }, transport.Requests.Select(p => p.Split('?')[0]));
        Assert.Same(Captured, client.BoundConnection);
        client.Dispose();
        await Assert.ThrowsAsync<ConnectionBoundClient.RetiredException>(() => client.Health.ListStorageAsync(1));
    }

    [Fact]
    public async Task Health_rejects_other_profiles_invalid_ids_and_writes_before_transport()
    {
        var transport = new Recording();
        using var client = new ConnectionBoundClient(new SessionScope(0, Captured), _ => transport, health: true);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Arp);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.DeviceGroups);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.System);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Sensors);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.SensorTransport);
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.GetAsync("switch.example.net"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.DiscoverAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Alerts.AcknowledgeAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Alerts.GetAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Ports.ListAllStatusAsync());
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Ports.ListIpAddressesAsync(1));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Health.ListStorageAsync(0));
        Assert.Throws<ArgumentException>(() => new ConnectionBoundClient(new SessionScope(0, Captured), _ => transport, overview: true, health: true));
        Assert.Empty(transport.Requests);
    }

    private sealed class Recording : ILibreNmsTransport
    {
        internal List<string> Requests { get; } = [];
        public LibreNmsConnection? Connection => Captured;
        public Task<IReadOnlyList<T>> GetCollectionAsync<T>(string relativeUrl, string collectionProperty, CancellationToken cancellationToken = default)
        {
            Requests.Add(relativeUrl);
            return Task.FromResult<IReadOnlyList<T>>([]);
        }
        public Task<System.Text.Json.JsonDocument> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken cancellationToken = default)
        {
            Assert.Equal(HttpMethod.Get, method);
            Assert.Equal("devices/1/maintenance", relativeUrl);
            Assert.Null(body);
            Requests.Add(relativeUrl);
            return Task.FromResult(System.Text.Json.JsonDocument.Parse("""{"is_under_maintenance":false}"""));
        }
        public Task<string> SendRawAsync(string relativeUrl, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No raw health reads");
    }
}
