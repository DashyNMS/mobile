using System.Text.Json;
using System.Text.Json.Serialization;
using DashyNMS.Agent.Devices;
using DesktopNMS.Core.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed partial class IntelligenceFactsTests
{
    [Theory]
    [InlineData("inventory", 20)]
    [InlineData("inventory", 1)]
    [InlineData("vlans", 20)]
    [InlineData("fdb", 20)]
    [InlineData("arp", 20)]
    [InlineData("poe", 20)]
    [InlineData("wireless", 20)]
    [InlineData("routing", 20)]
    [InlineData("availability", 20)]
    [InlineData("addresses", 20)]
    public async Task Device_detail_fields_in_MCPs_direct_projection_are_shown(string section, int limit)
    {
        var routes = Routes("device_details");
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(server.Root, FixtureServer.Token));
        using var host = HostDeviceDetails.Create(client, transport, new FakeTimeProvider(Now));
        var data = (await host.ReadAsync(new(1, [section], Limit: limit))).Result!.Data;
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        };
        var fields = Fields(JsonSerializer.SerializeToElement(new { data }, options));
        var text = await Run("device_details", JsonSerializer.Serialize(new { device = "1", sections = new[] { section }, limit }));
        var missing = fields.Where(field => !field.Value.GroupBy(v => v.Scope)
                .All(record => ShownFor(field.Key, record.Select(v => v.Value).ToList(), record.Key, text)))
            .Select(f => f.Key).ToList();
        Assert.True(missing.Count == 0, $"device_details {section}: missing fields {string.Join(", ", missing)}\n{text}");
    }
}
