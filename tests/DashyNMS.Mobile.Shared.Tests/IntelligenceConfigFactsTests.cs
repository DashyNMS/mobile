using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DashyNMS.Agent;
using DashyNMS.Agent.Unimus;
using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed partial class IntelligenceFactsTests
{
    private static string? ConfigRecordMarker(string path, JsonElement record)
    {
        if (path is "backup" or "backups" or "from" or "to" && record.TryGetProperty("id", out var id))
            return $"[[configbackup:{path}:{id.GetInt32()}]]";
        if (path == "unimus_device" && record.TryGetProperty("id", out var unimus)) return $"[[configdevice:{unimus.GetInt32()}]]";
        if (path == "changes" && record.TryGetProperty("unimus_device", out var device)) return $"[[configchange:{device.GetProperty("id").GetInt32()}]]";
        if (path == "unreadable" && record.TryGetProperty("unimus_device", out var unreadable)) return $"[[configunreadable:{unreadable.GetString()}]]";
        return null;
    }
    private static string? ConfigFactBlock(string text, string marker)
    {
        var parts = marker[2..^2].Split(':', 3);
        var lines = text.Split('\n');
        switch (parts[0])
        {
            case "configline":
                return parts[1] == "config" ? lines.FirstOrDefault(l => l.StartsWith("Config line " + parts[2] + ": ", StringComparison.Ordinal))
                    : lines.Where(l => l.StartsWith("Config diff ", StringComparison.Ordinal)).Skip(int.Parse(parts[2], CultureInfo.InvariantCulture)).FirstOrDefault();
            case "configbackup":
                if (parts[1] == "backups") return lines.Skip(1).FirstOrDefault(l => l.StartsWith("Backup " + parts[2] + ";", StringComparison.Ordinal));
                var header = lines[0];
                var label = (parts[1] switch { "from" => "From ", "to" => "To ", _ => "" }) + "Backup " + parts[2] + ";";
                var start = header.IndexOf(label, StringComparison.Ordinal);
                if (start < 0) return null;
                var end = header.IndexOf(". ", start, StringComparison.Ordinal);
                return header[start..(end < 0 ? header.Length : end)];
            case "configdevice": return lines.FirstOrDefault(l => l.StartsWith("Unimus device " + parts[1] + ";", StringComparison.Ordinal));
            case "configchange": return lines.FirstOrDefault(l => l.Contains("Unimus device " + parts[1] + ";", StringComparison.Ordinal));
            case "configunreadable": return lines.FirstOrDefault(l => l.StartsWith("Unreadable Unimus device " + CompactNetworkLookups.Clean(parts[1]) + ";", StringComparison.Ordinal));
            default: return null;
        }
    }
    private static bool? ConfigFactShown(string path, JsonElement value, string text)
    {
        var name = path[(path.LastIndexOf('.') + 1)..];
        if (path == "config_text") return text.Contains("Config text left out by the saved Summary choice.", StringComparison.Ordinal);
        if (name is "valid_since" or "valid_until" or "changed_at" or "previous_version_since")
            return text.Contains(IntelligenceTime.At(DateTime.Parse(value.GetString()!, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), new FakeTimeProvider()), StringComparison.Ordinal);
        if (name is "newest" or "first_backup") return text.Contains((name == "newest" ? "; newest " : "; first backup ") + (value.GetBoolean() ? "yes" : "no"), StringComparison.Ordinal);
        if (path is "config" or "config_diff")
            return text.EndsWith(path == "config" && value.GetString() == "" ? "(blank)" : CompactNetworkLookups.Clean(value.GetString()), StringComparison.Ordinal);
        if (path == "config_diff_lines_omitted") return text.Contains("Source cap omitted " + value.GetInt32().ToString(CultureInfo.InvariantCulture) + " diff lines.", StringComparison.Ordinal);
        return null;
    }

    [Theory]
    [InlineData("config", "[[configline:config:2]]", "expected text", "Config line 2: wrong text\nConfig line 3: expected text")]
    [InlineData("config_diff", "[[configline:config_diff:0]]", "+ expected text", "Config diff - wrong text\nConfig diff + expected text")]
    [InlineData("backups.valid_since", "[[configbackup:backups:900]]", "2026-10-17T09:00:00Z", "Header\nBackup 900; valid since 16 Oct 2026 09:00\nBackup 901; valid since 17 Oct 2026 09:00")]
    public void Config_accounting_rejects_another_line_or_revisions_fact(string path, string marker, string value, string text)
        => Assert.False(ShownFor(path, [JsonSerializer.SerializeToElement(value)], marker, text));

    [Theory]
    [InlineData("config_backup", "full")]
    [InlineData("config_backup", "summary")]
    [InlineData("config_backup", "paged")]
    [InlineData("config_diff", "full")]
    [InlineData("config_diff", "summary")]
    [InlineData("config_diff", "cap")]
    [InlineData("recent_config_changes", "full")]
    [InlineData("recent_config_changes", "first")]
    [InlineData("recent_config_changes", "unreadable")]
    public async Task Unimus_fields_in_MCPs_direct_projection_are_shown_in_their_record_or_counted_as_omitted(string tool, string scenario)
    {
        var mode = scenario == "summary" ? AppSettings.IntelligenceConfigTextSummary : AppSettings.IntelligenceConfigTextFull;
        var routes = SharedConfigLookupsHttpTests.Routes();
        if (scenario == "first")
        {
            var page = JsonNode.Parse(routes["/api/v2/devices/40/backups"].Body)!;
            page["data"]!.AsArray().RemoveAt(1);
            page["paginator"]!["totalCount"] = 1;
            routes["/api/v2/devices/40/backups"] = new(200, page.ToJsonString());
        }
        if (scenario == "unreadable") routes["/api/v2/devices/40/backups"] = new(403, """{"message":"password=synthetic-secret"}""");
        if (scenario == "cap") routes["/api/v2/backups/diff"] = new(200, JsonSerializer.Serialize(new
        {
            data = new { lineGroups = new[] { new { type = "CHANGED", revisedLines = Enumerable.Range(0, 205).Select(i => new { text = "interface Port" + i }) } } },
        }));
        using var nms = SharedConfigLookupsHttpTests.NmsServer();
        using var configs = SharedConfigLookupsHttpTests.Server(path => routes.GetValueOrDefault(path));
        using var mobile = new SharedConfigLookupsHttpTests.Mobile(nms.Root, configs.Root, mode);
        using var api = new UnimusApi(NullLogger<UnimusApi>.Instance);
        api.Configure(new UnimusConnection(configs.Root, "synthetic-fixture-token"));
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(nms.Root, FixtureServer.Token));
        using var host = HostConfigLookups.Create(client, api, new FakeTimeProvider(SharedConfigLookupsHttpTests.Now),
            new() { ConfigText = mode == AppSettings.IntelligenceConfigTextFull ? ConfigText.Full : ConfigText.Summary });
        object data = tool switch
        {
            "config_backup" => (await host.ReadBackupAsync(new(1, Offset: scenario == "paged" ? 1 : 0, Limit: 1))).Result!.Data,
            "config_diff" => (await host.ReadDiffAsync(new(1))).Result!.Data,
            _ => (await host.ReadChangesAsync(new(Limit: 1))).Result!.Data,
        };
        var arguments = new Dictionary<string, object?>();
        if (tool != "recent_config_changes") arguments["device"] = "1";
        if (tool != "config_diff") arguments["limit"] = 1;
        if (tool == "config_backup") arguments["offset"] = scenario == "paged" ? 1 : 0;
        var text = await mobile.Tool(tool).Run(JsonSerializer.Serialize(arguments), default);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        };
        var fields = Fields(JsonSerializer.SerializeToElement(new { data }, options));
        var missing = fields.Where(field => !field.Value.GroupBy(v => v.Scope)
            .All(record => ShownFor(field.Key, record.Select(v => v.Value).ToList(), record.Key, text)))
            .Select(f => f.Key).ToList();
        Assert.True(missing.Count == 0, $"{tool} {scenario}: missing fields {string.Join(", ", missing)}\n{text}");
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
        if (scenario == "cap") Assert.Contains("Source cap omitted 5 diff lines.", text);
        if (scenario == "first") Assert.Contains("first backup yes", text);
        Assert.DoesNotContain("synthetic-secret", text);
    }
}
