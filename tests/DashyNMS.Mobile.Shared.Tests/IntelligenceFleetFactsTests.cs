using System.Text.Json;
using System.Text.Json.Serialization;
using DashyNMS.Agent.Health;
using DashyNMS.Agent.Rankings;
using DesktopNMS.Core.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed partial class IntelligenceFactsTests
{
    [Theory]
    [InlineData("top_interfaces", "total")]
    [InlineData("top_errors", "in")]
    [InlineData("top_devices", "traffic")]
    [InlineData("top_devices", "problems")]
    [InlineData("health_overview", null)]
    public async Task Fleet_fields_in_MCPs_direct_projection_are_shown_or_documented(string tool, string? by)
    {
        var routes = Routes(tool);
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(server.Root, FixtureServer.Token));
        using var ranker = HostFleetRankings.Create(client, new FakeTimeProvider(Now));
        using var health = HostHealthOverview.Create(client, transport, new FakeTimeProvider(Now), new());
        object data = tool switch
        {
            "top_interfaces" => (await ranker.ReadPortsAsync(new(By: by, Limit: 1))).Result!.Data,
            "top_errors" => (await ranker.ReadPortsAsync(new(Errors: true, By: by, Limit: 1))).Result!.Data,
            "top_devices" => (await ranker.ReadDevicesAsync(new(by, Limit: 1))).Result!.Data,
            _ => (await health.ReadAsync(new(Limit: 1))).Result!.Data,
        };
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        };
        var fields = Fields(JsonSerializer.SerializeToElement(new { data }, options));
        var arguments = tool == "health_overview" ? "{\"limit\":1}" : JsonSerializer.Serialize(new { by, limit = 1 });
        var text = await Run(tool, arguments);
        var excluded = Table("Fleet facts left out").Where(r => r.Tool == tool).SelectMany(r => r.Fields).ToHashSet();
        Assert.All(excluded, path => Assert.Contains(path, fields.Keys));
        var missing = fields.Where(field => !excluded.Contains(field.Key))
            .Where(field => !field.Value.GroupBy(v => v.Scope).All(record => ShownFor(field.Key, record.Select(v => v.Value).ToList(), record.Key, text)))
            .Select(f => f.Key).ToList();
        Assert.True(missing.Count == 0, $"{tool} {by}: missing fields {string.Join(", ", missing)}\n{text}");
    }
}
