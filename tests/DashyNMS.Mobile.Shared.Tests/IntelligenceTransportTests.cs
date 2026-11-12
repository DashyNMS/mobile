using System.Net;
using System.Text.Json;
using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Api;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Tests;

/// <summary>Exercises Core's resource requests and JSON models beneath every compact lookup.</summary>
public sealed class IntelligenceTransportTests
{
    [Theory]
    [InlineData("network_overview", "", "fault:7", "faults")]
    [InlineData("find_device", "switch", "switch.example.net [[device:1]]", "devices")]
    [InlineData("device_summary", "1", "switch.example.net [[device:1]]", "devices/1")]
    [InlineData("active_alerts", "1", "(fault:7) on switch.example.net [[device:1]]", "faults")]
    [InlineData("device_sensors", "1", "Inlet [[sensor:9]]", "resources/sensors")]
    public async Task Each_lookup_uses_Core_resource_APIs_over_read_only_transport(
        string name, string query, string expected, string endpoint)
    {
        var transport = new RecordedTransport();
        var text = await Lookup(transport, name).Run(query, default);

        Assert.Contains(expected, text);
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
        if (name == "device_sensors") Assert.Contains("reading 20.5", text);
        if (name == "device_summary") Assert.Contains("Example switch", text);
        Assert.Contains(transport.Requests, r => r.Path.Split('?')[0] == endpoint);
        Assert.All(transport.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
    }

    [Theory]
    [InlineData(false, "alert:7", "alerts")]
    [InlineData(true, "fault:7", "faults")]
    public async Task Alert_probe_preserves_the_identity_of_the_returning_resource(bool faults, string key, string endpoint)
    {
        var transport = new RecordedTransport(faults);
        var text = await Lookup(transport, "active_alerts").Run("", default);

        Assert.Contains(key, text);
        Assert.Contains(transport.Requests, r => r.Path == "faults?state=-1");
        Assert.Contains(transport.Requests, r => r.Path.Split('?')[0] == endpoint);
    }

    private static IntelligenceLookup Lookup(RecordedTransport transport, string name)
    {
        var apis = new LibreNmsResourceApis(transport);
        var client = Substitute.For<ILibreNmsClient>();
        client.Devices.Returns(apis.Devices);
        client.Alerts.Returns(apis.Alerts);
        client.Sensors.Returns(apis.Sensors);
        var session = Substitute.For<ISessionService>();
        session.Connection.Returns(transport.Connection);
        return new CompactNetworkLookups(client, Fakes.Settings(), session, deviceTransport: (_, _) => transport).Create().Single(l => l.Name == name);
    }

    /// <summary>Answers only the expected GET routes, so writes and unexpected resource paths fail the test.</summary>
    private sealed class RecordedTransport(bool faults = true) : ILibreNmsTransport
    {
        public LibreNmsConnection? Connection { get; } = new(new Uri("https://nms.example.net/"), "test-token");
        public List<(HttpMethod Method, string Path)> Requests { get; } = [];

        public Task<JsonDocument> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add((method, relativeUrl));
            Assert.Equal(HttpMethod.Get, method);
            Assert.Null(body);
            if (relativeUrl == "faults?state=-1")
            {
                if (!faults) throw new LibreNmsApiException("Route missing", HttpStatusCode.NotFound);
                return Task.FromResult(JsonDocument.Parse("""{"alerts": []}"""));
            }
            var json = relativeUrl.Split('?')[0] switch
            {
                "devices" or "devices/1" => """{"devices":[{"device_id":1,"hostname":"switch.example.net","ip":"192.0.2.1","status":1,"os":"example-os","hardware":"Example switch"}]}""",
                "faults" when faults => """{"alerts":[{"id":7,"device_id":1,"hostname":"switch.example.net","name":"Example rule","severity":"critical","state":1,"open":1}]}""",
                "alerts" when !faults => """{"alerts":[{"id":7,"device_id":1,"hostname":"switch.example.net","name":"Example rule","severity":"critical","state":1,"open":1}]}""",
                "resources/sensors" => """{"sensors":[{"sensor_id":9,"device_id":1,"sensor_descr":"Inlet","sensor_class":"temperature","sensor_current":"20.5"}]}""",
                _ => throw new InvalidOperationException("Unexpected resource path"),
            };
            return Task.FromResult(JsonDocument.Parse(json));
        }

        /// <summary>Parses the supplied envelope into Core's models rather than substituting model instances.</summary>
        public async Task<IReadOnlyList<T>> GetCollectionAsync<T>(string relativeUrl, string collectionProperty, CancellationToken cancellationToken = default)
        {
            using var document = await SendAsync(HttpMethod.Get, relativeUrl, cancellationToken: cancellationToken);
            return JsonSerializer.Deserialize<List<T>>(document.RootElement.GetProperty(collectionProperty).GetRawText(), DesktopNMS.Core.Json.LibreNmsJson.Options)!;
        }

        public Task<string> SendRawAsync(string relativeUrl, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Raw requests are not expected");
    }
}
