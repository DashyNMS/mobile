using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DashyNMS.Agent.Graphs;
using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed partial class IntelligenceFactsTests
{
    private static string? GraphRecordMarker(string path, JsonElement record) => path switch
    {
        "graph" => "[[graphmeta]]",
        "port" => "[[graphport:" + record.GetProperty("port_id").GetInt32() + "]]",
        "sensor" => "[[graphsensor:" + record.GetProperty("sensor_id").GetInt32() + "]]",
        "available_types" => "[[graphtype:" + record.GetProperty("name").GetString() + "]]",
        _ => null,
    };
    private static string? GraphFactBlock(string text, string marker)
    {
        var lines = text.Split('\n');
        if (marker == "[[graphmeta]]") return lines.FirstOrDefault(l => l.StartsWith("Graph type ", StringComparison.Ordinal));
        var parts = marker[2..^2].Split(':', 2);
        var prefix = parts[0] switch { "graphport" => "Port ", "graphsensor" => "Sensor ", _ => "Available graph type " };
        return lines.FirstOrDefault(l => l.StartsWith(prefix + CompactNetworkLookups.Clean(parts[1]) + ";", StringComparison.Ordinal));
    }
    private static bool? GraphFactShown(string path, JsonElement value, string text)
    {
        if (path is "graph.from" or "graph.to")
            return text.Contains(IntelligenceTime.At(DateTime.Parse(value.GetString()!, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), new FakeTimeProvider()), StringComparison.Ordinal);
        // Graph type identifiers retain underscores so the model can use them as catalogue arguments.
        if (path is "graph.type" or "graph.description" or "available_types.name" or "available_types.description"
            or "sensor.description" or "sensor.class" or "port.if_name" or "port.alias" or "other_types")
            return text.Contains(CompactNetworkLookups.Clean(value.GetString()), StringComparison.Ordinal);
        return null;
    }
    [Fact]
    public void Graph_accounting_does_not_accept_a_description_from_a_different_type()
    {
        using var json = JsonDocument.Parse("""{"data":{"available_types":[{"name":"device_bits","description":"Traffic"}]}}""");
        var fact = Fields(json.RootElement)["available_types.description"].Single();
        Assert.False(ShownFor("available_types.description", [fact.Value], fact.Scope,
            "Available graph type device_bits; description CPU\nAvailable graph type device_processor; description Traffic"));
    }
    [Theory]
    [InlineData("device")]
    [InlineData("cpu")]
    [InlineData("port")]
    [InlineData("sensor")]
    [InlineData("custom")]
    [InlineData("partial")]
    [InlineData("negative")]
    [InlineData("cap")]
    public async Task Graph_fields_in_fixed_MCP_fixtures_and_current_projection_are_shown_in_their_record_or_counted_as_omitted(string scenario)
    {
        Answer? Change(string path)
        {
            if (scenario == "negative" && path == "devices/1/graphs") return new(403, "{}");
            if (scenario == "cap" && path == "devices/1/graphs") return new(200, JsonSerializer.Serialize(new
            {
                graphs = new[] { new { name = "device_bits", desc = "Traffic" } }.Concat(Enumerable.Range(0, 90).Select(i => new { name = "device_synthetic_" + i, desc = "Synthetic graph " + i })),
            }));
            return null;
        }
        using var server = SharedGraphLookupHttpTests.Server(partial: scenario == "partial", change: Change);
        using var mobile = new SharedGraphLookupHttpTests.Mobile(server.Root);
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(server.Root, FixtureServer.Token));
        using var host = HostGraphLookup.Create(client, transport, new FakeTimeProvider(SharedGraphLookupHttpTests.Now));
        var request = new GraphLookupRequest(1, Type: scenario == "cpu" ? "cpu" : scenario == "port" ? "errors" : scenario == "sensor" ? "temperature" : null,
            Port: scenario == "port" ? "1/1/7" : null, Sensor: scenario == "sensor" ? "1" : null,
            Since: scenario == "custom" ? "2026-10-16T10:00:00Z" : null, Until: scenario == "custom" ? "2026-10-17T09:00:00Z" : null);
        var result = (await host.ReadAsync(request)).Result!;
        var text = await mobile.Graph.Run(JsonSerializer.Serialize(new { device = "1", type = request.Type, port = request.Port,
            sensor = request.Sensor, since = request.Since, until = request.Until }), default);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        };
        var data = JsonSerializer.SerializeToNode(result.Data, options)!.AsObject();
        if (scenario is "device" or "cpu" or "port" or "sensor")
        {
            using var fixedMcp = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "Graphs", "expected-mcp.json")));
            var baseline = fixedMcp.RootElement.GetProperty(scenario).GetProperty("data");
            Assert.True(JsonElement.DeepEquals(baseline, JsonSerializer.SerializeToElement(result.Data, options)));
        }
        // MCP appends this partial-source notice to Data when a listing fails.
        if (result.Unavailable.Count > 0) data["unavailable"] = JsonSerializer.SerializeToNode(result.Unavailable);
        var fields = Fields(JsonSerializer.SerializeToElement(new { data }));
        var missing = fields.Where(field => !field.Value.GroupBy(v => v.Scope)
            .All(record => ShownFor(field.Key, record.Select(v => v.Value).ToList(), record.Key, text)))
            .Select(f => f.Key).ToList();
        Assert.True(missing.Count == 0, $"graph {scenario}: missing fields {string.Join(", ", missing)}\n{text}");
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
        if (scenario == "negative")
        {
            Assert.Empty(mobile.Images);
            Assert.Contains("availability is unknown", text);
        }
        else Assert.Equal(result.Image!.Data, Assert.Single(mobile.Images).Data);
        if (scenario == "cap")
        {
            Assert.True(result.Omitted > 0);
            Assert.Contains("more results omitted", text);
        }
    }
}
