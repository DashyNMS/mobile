using System.Text.Json;
using System.Text.Json.Serialization;
using DashyNMS.Agent;
using DashyNMS.Agent.Alerts;
using DesktopNMS.Core.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed partial class IntelligenceFactsTests
{
    /// <summary>
    /// Current MCP projections over the pinned shared operation, without loading MCP into
    /// mobile. RuleTools projects target names, while alert_details serialises Data directly.
    /// Existing HTTP tests independently assert triggers, history, source identities and sorts.
    /// </summary>
    [Theory]
    [InlineData("alert_details", "{\"alert_id\":10}")]
    [InlineData("alert_rules", "{\"name\":\"Port\",\"verbose\":true}")]
    [InlineData("alert_rules", "{\"name\":\"Old test\",\"include_disabled\":true,\"verbose\":true}")]
    public async Task Alert_fields_in_MCPs_current_projection_are_shown(string tool, string arguments)
    {
        var routes = Routes(tool);
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path));
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(server.Root, FixtureServer.Token));
        using var host = HostAlertRules.Create(client, new FakeTimeProvider(Now), new AgentOptions());
        using var args = JsonDocument.Parse(arguments);
        object data;
        if (tool == "alert_details")
            data = (await host.ReadDetailsAsync(new(10))).Result!.Data;
        else
        {
            var result = (await host.ReadRulesAsync(new(args.RootElement.GetProperty("name").GetString(),
                IncludeDisabled: args.RootElement.TryGetProperty("include_disabled", out var include) && include.GetBoolean(),
                Limit: 20, Verbose: true))).Result!;
            data = new
            {
                rules = result.Data.Rules.Select(r => new
                {
                    r.RuleId, r.Name, r.Severity, r.Enabled, r.Condition,
                    applies_to = r.AppliesTo.AllDevices ? (object)"all devices" : new
                    {
                        r.AppliesTo.Targets!.Except,
                        devices = r.AppliesTo.Targets.Devices?.Select(t => t.Name).ToList(),
                        groups = r.AppliesTo.Targets.Groups?.Select(t => t.Name).ToList(),
                        locations = r.AppliesTo.Targets.Locations?.Select(t => t.Name).ToList(),
                    },
                    r.Template, r.NotifyPerEntity, r.MaxEntities, r.MaxEntitiesInherited, r.Acknowledging,
                    r.RemovedColumns, r.OpenAlerts, raised_last_7_days = r.RaisedLast7Days, r.Notes, r.Procedure,
                }),
                result.Data.Page, result.Data.Unavailable,
            };
        }
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        };
        var json = JsonSerializer.SerializeToElement(new { data }, options);
        var text = await Run(tool, arguments);
        var missing = Fields(json).Where(field => !field.Value.GroupBy(v => v.Scope)
                .All(record => ShownFor(field.Key, record.Select(v => v.Value).ToList(), record.Key, text)))
            .Select(f => f.Key).ToList();
        Assert.True(missing.Count == 0, $"{tool}: missing fields {string.Join(", ", missing)}\n{text}");
    }
}
