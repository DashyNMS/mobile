using System.Text.Json;
using System.Text.Json.Serialization;
using DashyNMS.Agent.Ports;
using DesktopNMS.Core.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed partial class IntelligenceFactsTests
{
    [Theory]
    [InlineData("port_status", "{\"device\":\"1\",\"port\":\"1/1/1\"}")]
    [InlineData("device_neighbours", "{\"device\":\"1\"}")]
    [InlineData("trace_endpoint", "{\"address\":\"aa:bb:cc:00:00:05\"}")]
    public async Task Topology_fields_in_MCPs_current_projection_are_shown_or_documented(string tool, string arguments)
    {
        var routes = Routes(tool);
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(server.Root, FixtureServer.Token));
        using var host = HostPortLookups.Create(client, transport, new FakeTimeProvider(Now), new());
        object data = tool switch
        {
            "port_status" => (await host.ReadStatusAsync(new(1, "1/1/1"))).Result!.Data,
            "device_neighbours" => (await host.ReadNeighboursAsync(new(1))).Result!.Data,
            _ => (await host.TraceAsync(new("aa:bb:cc:00:00:05"))).Result!.Data,
        };
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        };
        var fields = Fields(JsonSerializer.SerializeToElement(new { data }, options));
        var text = await Run(tool, arguments);
        var excluded = Table("Topology facts left out").Where(r => r.Tool == tool).SelectMany(r => r.Fields).ToHashSet();
        Assert.All(excluded, path => Assert.Contains(path, fields.Keys));
        var missing = fields.Where(field => !excluded.Contains(field.Key))
            .Where(field => !field.Value.GroupBy(v => v.Scope).All(record => ShownFor(field.Key, record.Select(v => v.Value).ToList(), record.Key, text)))
            .Select(f => f.Key).ToList();
        Assert.True(missing.Count == 0, $"{tool}: missing fields {string.Join(", ", missing)}\n{text}");
    }
}
