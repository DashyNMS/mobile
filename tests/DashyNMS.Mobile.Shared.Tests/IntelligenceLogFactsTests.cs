using System.Text.Json;
using System.Text.Json.Serialization;
using DashyNMS.Agent.Graylog;
using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed partial class IntelligenceFactsTests
{
    [Theory]
    [InlineData("lines.text", "Log 17 Oct 2026 09:00; source first.example.net; wrong text\nLog 17 Oct 2026 08:00; source second.example.net; expected text")]
    [InlineData("top.sources.count", "Top source first.example.net: 2.\nTop source second.example.net: 17.")]
    [InlineData("over_time.count", "From 17 Oct 2026 09:00: 2 messages.\nFrom 17 Oct 2026 08:00: 17 messages.")]
    public void Graylog_accounting_rejects_a_fact_from_another_record(string path, string text)
    {
        using var json = JsonDocument.Parse("""
            {"data":{"lines":[{"time":"2026-10-17T09:00:00Z","source":"first.example.net","text":"expected text"}],
            "top":{"sources":[{"value":"first.example.net","count":17}]},
            "over_time":[{"from":"2026-10-17T09:00:00Z","count":17}]}}
            """);
        var fact = Fields(json.RootElement)[path].Single();
        Assert.False(ShownFor(path, [fact.Value], fact.Scope, text));
    }

    [Theory]
    [InlineData("current")]
    [InlineData("past")]
    [InlineData("oldest")]
    [InlineData("empty")]
    [InlineData("device")]
    [InlineData("rich")]
    public async Task Graylog_fields_in_MCPs_direct_projection_are_shown_in_their_own_rows_or_counted_as_omitted(string scenario)
    {
        var past = scenario is "past" or "oldest";
        using var nms = new FixtureServer(path => path == "devices" ? new(200,
            """{"devices":[{"device_id":1,"hostname":"core-sw1.example.net","display":"Core switch","ip":"192.0.2.1","location":"Lab","status":1,"disabled":0}]}""") : null);
        using var logs = SharedLogSearchHttpTests.Server(deepPaging: scenario == "oldest", empty: scenario == "empty", inject: scenario == "rich");
        using var mobile = new SharedLogSearchHttpTests.Mobile(nms.Root, logs.Root);
        await mobile.OpenAsync();
        using var graylog = new GraylogApi(NullLogger<GraylogApi>.Instance);
        graylog.Configure(new GraylogConnection(logs.Root, GraylogSettings.Version21, null, "fixture", "synthetic-password"));
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(nms.Root, FixtureServer.Token));
        using var host = HostLogSearch.Create(client, graylog, new FakeTimeProvider(SharedLogSearchHttpTests.Now), new());
        var request = new SearchLogsRequest(DeviceId: scenario == "device" ? 1 : null,
            Since: past ? "2026-10-16T07:00:00Z" : null, Until: past ? "2026-10-16T09:00:00Z" : null,
            Stream: "Network devices", Limit: 1);
        var result = (await host.ReadAsync(request)).Result!;
        var text = await mobile.Search.Run(JsonSerializer.Serialize(new
        {
            device = request.DeviceId?.ToString(System.Globalization.CultureInfo.InvariantCulture), since = request.Since,
            until = request.Until, stream = request.Stream, limit = request.Limit,
        }), default);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        };
        var fields = Fields(JsonSerializer.SerializeToElement(new { data = result.Data }, options));
        var missing = fields.Where(field => !field.Value.GroupBy(v => v.Scope)
            .All(record => ShownFor(field.Key, record.Select(v => v.Value).ToList(), record.Key, text)))
            .Select(f => f.Key).ToList();
        Assert.True(missing.Count == 0, $"search_logs {scenario}: missing fields {string.Join(", ", missing)}\n{text}");
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
        if (scenario == "oldest")
        {
            Assert.False(result.NewestRead);
            Assert.Equal(1, result.Data.Count);
            Assert.Contains("Oldest samples; newest exceed Graylog paging.", text);
            Assert.Contains("Outside today's window", text);
        }
        if (scenario == "device") Assert.Contains(mobile.Evidence, e => e.Reference.Object is { Kind: "device", Id: 1 });
        else Assert.Empty(mobile.Evidence);
    }
}
