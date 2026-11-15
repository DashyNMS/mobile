using System.Text.Json;
using System.Text.Json.Serialization;
using DashyNMS.Agent.Events;
using DesktopNMS.Core.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed partial class IntelligenceFactsTests
{
    private const string HistorySince = "2026-10-16T10:00:00Z";
    private const string HistoryUntil = "2026-10-17T10:00:00Z";
    [Theory]
    [InlineData("device_events")]
    [InlineData("network_events")]
    [InlineData("active_alerts")]
    [InlineData("what_changed")]
    public async Task History_fields_in_MCPs_direct_projection_are_shown_or_documented(string tool)
    {
        var routes = Routes(tool == "active_alerts" ? "active_alerts-history" : tool);
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(server.Root, FixtureServer.Token));
        using var unimus = new UnimusApi(NullLogger<UnimusApi>.Instance);
        using var graylog = new GraylogApi(NullLogger<GraylogApi>.Instance);
        using var host = HostEventLookups.Create(client, unimus, graylog, new FakeTimeProvider(Now), new());
        object data = tool switch
        {
            "device_events" => (await host.ReadDeviceAsync(new(1, Since: HistorySince, Until: HistoryUntil, Limit: 1))).Result!.Data,
            "network_events" => (await host.ReadNetworkAsync(new(Log: "both", Since: HistorySince, Until: HistoryUntil, Limit: 1))).Result!.Data,
            "active_alerts" => (await host.ReadRaisedAlertsAsync(new(Since: HistorySince, Until: HistoryUntil, Limit: 1))).Result!.Data,
            _ => (await host.ReadChangesAsync(new(1, Since: HistorySince, Until: HistoryUntil))).Result!.Data,
        };
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        };
        var fields = Fields(JsonSerializer.SerializeToElement(new { data }, options));
        var arguments = new Dictionary<string, object?>
        {
            ["since"] = HistorySince, ["until"] = HistoryUntil,
            ["device"] = tool is "device_events" or "what_changed" ? "1" : null,
        };
        if (tool != "what_changed") arguments["limit"] = 1;
        if (tool == "network_events") arguments["log"] = "both";
        var text = await Run(tool, JsonSerializer.Serialize(arguments));
        var excluded = Table("History facts left out").Where(r => r.Tool == tool).SelectMany(r => r.Fields).ToHashSet();
        Assert.All(excluded, path => Assert.Contains(path, fields.Keys));
        var missing = fields.Where(field => !excluded.Contains(field.Key))
            .Where(field => !field.Value.GroupBy(v => v.Scope).All(record => ShownFor(field.Key, record.Select(v => v.Value).ToList(), record.Key, text)))
            .Select(f => f.Key).ToList();
        Assert.True(missing.Count == 0, $"{tool}: missing fields {string.Join(", ", missing)}\n{text}");
    }
}
