using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DashyNMS.Agent.Catalogue;
using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// Mobile's lookups against the facts MCP's own tools return (#636). Each case runs a phone tool over
/// the shared fixture MCP was recorded on, with the same arguments, and every field of MCP's recorded
/// JSON (<c>Fixtures/Intelligence/McpJson</c>) must appear in the phone's text or be listed under
/// "Facts left out" in docs/INTELLIGENCE.md with its reason. A field MCP adds fails here until someone
/// shows it or writes down why not.
/// </summary>
public sealed partial class IntelligenceFactsTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>When the lookups run: fixed, in UTC, so written times and lengths are exact.</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);

    public static TheoryData<string, string, string> Cases() => new()
    {
        { "find_device", "find_device", """{"query":"sw"}""" },
        { "find_device-verbose", "find_device", """{"query":"sw","verbose":true}""" },
        { "active_alerts", "active_alerts", "{}" },
        { "active_alerts-verbose", "active_alerts", """{"verbose":true}""" },
        { "network_overview", "network_overview", "{}" },
        { "device_health", "device_health", """{"device":"1"}""" },
        { "device_sensors", "device_sensors", """{"device":"1"}""" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Every_field_MCP_returns_is_shown_or_listed_as_left_out(string recording, string tool, string arguments)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "Intelligence", "McpJson", recording + ".json")));
        var leftOut = LeftOut(tool);
        var text = await Run(tool, arguments);

        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
        var missing = Fields(json.RootElement)
            .Where(field => !leftOut.Contains(field.Key))
            .Where(field => !field.Value.GroupBy(v => v.Scope).All(record => ShownFor(field.Key, record.Select(v => v.Value).ToList(), record.Key, text)))
            .Select(field => field.Key)
            .ToList();

        Assert.True(missing.Count == 0, $"{tool}: neither shown nor listed in docs/INTELLIGENCE.md: {string.Join(", ", missing)}\n{text}");
    }

    /// <summary>
    /// A field is shown for one record (a device, alert or sensor) when its values appear in that
    /// record's own rows, found by the record's marker. A record whose row isn't shown passes only when
    /// the reply counts omitted results; a list within a row (an alert's faults) may name some values
    /// and count the rest. Fields outside any record (totals, the uptime) are looked for in the whole reply.
    /// </summary>
    private static bool ShownFor(string path, IReadOnlyList<JsonElement> values, string? marker, string text)
    {
        var block = marker is null ? text : Block(text, marker);
        if (block is null) return text.Contains("more results omitted", StringComparison.Ordinal);
        // An alert's age, recorded in minutes when MCP's JSON was taken, is checked against when the same
        // alert opened (from the verbose recording) and the test's fixed clock.
        if (path.EndsWith("age_minutes", StringComparison.Ordinal) && marker is not null && Opened.TryGetValue(marker, out var opened))
            return block.Contains("open " + IntelligenceTime.Length(Now.UtcDateTime - opened), StringComparison.Ordinal)
                || block.Contains(IntelligenceTime.Length(Now.UtcDateTime - opened) + " ago", StringComparison.Ordinal);
        return block.Contains(" more", StringComparison.Ordinal)
            ? values.Any(value => Shown(path, value, block))
            : values.All(value => Shown(path, value, block));
    }

    /// <summary>When each alert opened, by its marker, from MCP's verbose record of the same fixture.</summary>
    private static readonly Dictionary<string, DateTime> Opened = LoadOpened();

    private static Dictionary<string, DateTime> LoadOpened()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "Intelligence", "McpJson", "active_alerts-verbose.json")));
        var opened = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        foreach (var device in json.RootElement.GetProperty("data").GetProperty("devices").EnumerateArray())
            foreach (var alert in device.GetProperty("alerts").EnumerateArray())
                if (alert.TryGetProperty("since", out var since) && since.ValueKind == JsonValueKind.String)
                    opened[$"[[alert:{alert.GetProperty("alert_id").GetInt64().ToString(CultureInfo.InvariantCulture)}]]"] =
                        DateTime.Parse(since.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        return opened;
    }

    /// <summary>The lines from the first naming the marker up to the next that names another record of its kind.</summary>
    private static string? Block(string text, string marker)
    {
        var lines = text.Split('\n');
        var start = Array.FindIndex(lines, l => l.Contains(marker, StringComparison.Ordinal));
        if (start < 0) return null;
        var kind = marker[..(marker.IndexOf(':') + 1)];
        var end = start + 1;
        while (end < lines.Length && !(lines[end].Contains(kind, StringComparison.Ordinal) && !lines[end].Contains(marker, StringComparison.Ordinal)))
            end++;
        return string.Join("\n", lines[start..end]);
    }

    /// <summary>Every row the doc lists is a field MCP really returns, so the list can't go stale.</summary>
    [Fact]
    public void Every_listed_field_is_one_MCP_returns()
    {
        var recorded = Cases().Select(c => ((string)c[1], (string)c[0]))
            .GroupBy(c => c.Item1)
            .ToDictionary(g => g.Key, g => g.SelectMany(c => Fields(JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "Intelligence", "McpJson", c.Item2 + ".json"))).RootElement).Keys).ToHashSet());
        // device_health's later facts are absent from the fixture's empty or failed parts; #634 brings them.
        string[] notInFixture = ["open_alerts", "open_faults", "cpu_max_percent", "memory_max_percent", "fullest_storage", "problem_ports", "problem_ports_omitted"];
        foreach (var (tool, fields) in Table("Facts left out").Where(r => r.Tool != "every tool"))
        {
            Assert.All(fields.Where(f => !notInFixture.Contains(f)), field => Assert.Contains(field, recorded[tool]));
        }
    }

    /// <summary>Every catalogue parameter of an offered tool is offered or listed with its reason (#635).</summary>
    [Fact]
    public void Every_catalogue_parameter_is_offered_or_listed_as_left_out()
    {
        var listed = Table("Parameters left out").GroupBy(r => r.Tool).ToDictionary(g => g.Key, g => g.SelectMany(r => r.Fields).ToHashSet());
        foreach (var name in CompactNetworkLookups.ReviewedTools)
        {
            var offered = CompactNetworkLookups.Offered[name];
            var notOffered = ToolCatalogue.Find(name)!.Parameters.Select(p => p.Name).Where(p => !offered.Contains(p)).ToHashSet();
            Assert.Equal(notOffered.Order(), (listed.GetValueOrDefault(name) ?? []).Order());
        }
    }

    [Fact]
    public void Times_are_local_with_the_date_and_lengths_in_days_and_hours()
    {
        var time = new FakeTimeProvider(Now);
        time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Synthetic+1", TimeSpan.FromHours(1), "Synthetic+1", "Synthetic+1"));

        Assert.Equal("20 Oct 2026 08:30", IntelligenceTime.At(new DateTime(2026, 10, 20, 7, 30, 0, DateTimeKind.Utc), time));
        Assert.Equal("24 days 16 hours", IntelligenceTime.Length(Now - new DateTimeOffset(2026, 10, 20, 7, 30, 0, TimeSpan.Zero)));
        Assert.Equal("1 day", IntelligenceTime.Length(TimeSpan.FromDays(1)));
        Assert.Equal("3 hours", IntelligenceTime.Length(TimeSpan.FromMinutes(200)));
        Assert.Equal("1 minute", IntelligenceTime.Length(TimeSpan.FromSeconds(90)));
        Assert.Equal("under a minute", IntelligenceTime.Length(TimeSpan.FromSeconds(-5)));
        Assert.Equal("20 Oct 2026 08:30, 24 days 16 hours ago", IntelligenceTime.AtAndAgo(new DateTime(2026, 10, 20, 7, 30, 0, DateTimeKind.Utc), Now, time));
    }

    // ---- Running a phone tool over the fixture MCP was recorded on.

    private static async Task<string> Run(string tool, string arguments)
    {
        var routes = Routes(tool);
        using var server = new FixtureServer(path => routes.GetValueOrDefault(path) ?? routes.GetValueOrDefault(path.Split('?')[0]));
        using var shared = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        var settings = Fakes.Settings(new AppSettings { TimeoutSeconds = 5 });
        var session = Fakes.Session();
        session.Connection.Returns(new LibreNmsConnection(server.Root, FixtureServer.Token, timeoutSeconds: 5));
        using var transports = new IntelligenceDeviceTransports(shared, settings, session);
        var time = new FakeTimeProvider(Now);
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        var lookup = new CompactNetworkLookups(Fakes.Client(), settings, session, time, transports.Open).Create().Single(l => l.Name == tool);
        return await lookup.Run(arguments, default);
    }

    private static Dictionary<string, Answer> Routes(string tool)
    {
        var routes = new Dictionary<string, Answer>(StringComparer.Ordinal);
        void Files(string fixture, JsonElement map)
        {
            foreach (var route in map.EnumerateObject())
                routes[route.Name] = new(200, File.ReadAllText(Path.Combine(Fixtures, fixture, route.Value.GetString()!)));
        }
        JsonElement Load(string fixture, string file) => JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, fixture, file))).RootElement;

        switch (tool)
        {
            case "find_device":
                foreach (var route in Load("DeviceSearch", "scopes.json").GetProperty("routes").EnumerateArray())
                {
                    var body = route.TryGetProperty("file", out var file)
                        ? File.ReadAllText(Path.Combine(Fixtures, "DeviceSearch", file.GetString()!))
                        : route.GetProperty("body").GetString()!;
                    routes[route.GetProperty("path").GetString()!] = new(route.GetProperty("status").GetInt32(), body);
                }
                break;
            case "active_alerts":
                Files("OpenAlerts", Load("OpenAlerts", "cases.json").GetProperty("routes").GetProperty("faults"));
                break;
            case "network_overview":
                foreach (var name in new[] { "devices", "faults", "ports", "system" })
                    routes[name] = new(200, File.ReadAllText(Path.Combine(Fixtures, "NetworkOverview", name + ".json")));
                break;
            case "device_health":
                Files("DeviceSummary", Load("DeviceSummary", "cases.json").GetProperty("routes"));
                break;
            case "device_sensors":
                return SharedDeviceSensorsHttpTests.Routes("fleet");
        }

        return routes;
    }

    // ---- MCP's fields, and whether the phone's text shows each.

    /// <summary>
    /// Every leaf field by its path under <c>data</c> (arrays left out of the path), with every value it
    /// takes and the marker of the record it belongs to: the nearest enclosing object with a sensor,
    /// alert or device id.
    /// </summary>
    private static Dictionary<string, List<(JsonElement Value, string? Scope)>> Fields(JsonElement root)
    {
        var fields = new Dictionary<string, List<(JsonElement, string?)>>(StringComparer.Ordinal);
        void Walk(JsonElement element, string path, string? scope)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var own = Marker(element, "sensor_id", "sensor") ?? Marker(element, "alert_id", "alert") ?? Marker(element, "device_id", "device");
                    foreach (var property in element.EnumerateObject())
                        Walk(property.Value, path.Length == 0 ? property.Name : path + "." + property.Name, own ?? scope);
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray()) Walk(item, path, scope);
                    break;
                case JsonValueKind.Null:
                    break;
                default:
                    if (!fields.TryGetValue(path, out var values)) fields[path] = values = [];
                    values.Add((element.Clone(), scope));
                    break;
            }
        }

        foreach (var property in root.EnumerateObject())
            Walk(property.Value, property.Name == "data" ? "" : property.Name, null);
        return fields;
    }

    private static string? Marker(JsonElement record, string idField, string kind)
        => record.TryGetProperty(idField, out var id) && id.ValueKind == JsonValueKind.Number ? $"[[{kind}:{id.GetInt64().ToString(CultureInfo.InvariantCulture)}]]" : null;

    /// <summary>
    /// The device states the phone may write for MCP's status word. The phone writes Core's reported
    /// state, which is more accurate where MCP's word isn't (DashyNMS/mcp#121): MCP says up for an
    /// ignored device, and down when LibreNMS left a flag out, which the phone calls Unknown.
    /// </summary>
    private static string[] States(string status) => status switch
    {
        "up" => ["Up", "Ignored", "Unknown"],
        "down" => ["Down", "Ignored", "Unknown"],
        "disabled" => ["Disabled"],
        _ => ["Unknown"],
    };

    private static bool Shown(string path, JsonElement value, string text)
    {
        var name = path[(path.LastIndexOf('.') + 1)..];
        if (name is "status" or "device_status")
            return States(value.GetString() ?? "").Any(state => Regex.IsMatch(text, $@"\b{state}\b"));
        // Measured when MCP's JSON was recorded, so not comparable by value; its source, the alert's
        // since, is compared by value below.
        if (name == "age_minutes")
            return Length().IsMatch(text);
        // Counts the row shows by saying nothing: no acknowledged faults reads "active", none left out reads no "more".
        if (name is "acknowledged_count" or "faults_omitted" or "alert_groups_omitted" or "down_devices_omitted" or "down_locations_omitted" && value.ValueKind == JsonValueKind.Number && value.GetInt64() == 0)
            return true;
        // An alert of one fault names that fault ("for Inlet") rather than counting it, as MCP's brief record does.
        if (path.EndsWith("alerts.fault_count", StringComparison.Ordinal) && value.ValueKind == JsonValueKind.Number && value.GetInt64() == 1)
            return true;
        // A reading that isn't there has no unit to write: "no reading" says so.
        if (name == "unit" && text.Contains("no reading", StringComparison.Ordinal))
            return true;
        // A reading with nothing to judge it by is "not assessed", the phone's more exact word for MCP's unknown.
        if (path == "readings.state" && value.GetString() == "unknown")
            return text.Contains("state unknown", StringComparison.Ordinal) || text.Contains("not assessed", StringComparison.Ordinal);
        if (name == "uptime_seconds")
            return text.Contains(IntelligenceTime.Length(TimeSpan.FromSeconds(value.GetInt64())), StringComparison.Ordinal);
        if (name == "since" && value.ValueKind == JsonValueKind.String)
        {
            var at = DateTime.Parse(value.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            var opened = at.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);
            var length = IntelligenceTime.Length(Now.UtcDateTime - at);
            // A fault's start is its time alone; an alert's is its length, with verbose its time and length.
            return path.EndsWith("faults.since", StringComparison.Ordinal)
                ? text.Contains("since " + opened, StringComparison.Ordinal)
                : text.Contains("open " + length, StringComparison.Ordinal) || text.Contains($"opened {opened}, {length} ago", StringComparison.Ordinal);
        }

        var written = value.ValueKind switch
        {
            JsonValueKind.String => CompactNetworkLookups.Clean(value.GetString()).Replace('_', ' '),
            JsonValueKind.Number => value.TryGetInt64(out var whole) ? whole.ToString(CultureInfo.InvariantCulture) : value.GetDouble().ToString("0.##", CultureInfo.InvariantCulture),
            JsonValueKind.True => "true",
            _ => "false",
        };
        // A number must stand on its own, so a 2 isn't found inside 2026 or 198.51.100.2.
        return value.ValueKind == JsonValueKind.Number
            ? Regex.IsMatch(text, $@"(?<![\d.]){Regex.Escape(written)}(?![\d.])")
            : text.Contains(written, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>An age written as a length: "open 3 hours" without verbose, "opened …, 24 days 16 hours ago" with it.</summary>
    [GeneratedRegex(@"\bopen(ed [^;]*?,)? (\d+ (day|hour|minute)s?|under a minute)")]
    private static partial Regex Length();

    // ---- The doc's tables.

    private static HashSet<string> LeftOut(string tool)
        => Table("Facts left out").Where(r => r.Tool == tool || r.Tool == "every tool").SelectMany(r => r.Fields).ToHashSet(StringComparer.Ordinal);

    /// <summary>The rows of the table under a heading: the tool (or "every tool") and the backticked names in the second column.</summary>
    private static List<(string Tool, string[] Fields)> Table(string heading)
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Docs", "INTELLIGENCE.md"));
        var start = Array.FindIndex(lines, l => l.TrimStart('#', ' ') == heading);
        Assert.True(start >= 0, $"docs/INTELLIGENCE.md has no \"{heading}\" section.");
        var rows = new List<(string, string[])>();
        foreach (var line in lines.Skip(start + 1).SkipWhile(l => !l.StartsWith('|')).TakeWhile(l => l.StartsWith('|')))
        {
            var cells = line.Split('|', StringSplitOptions.TrimEntries);
            if (cells.Length < 4 || cells[1] is "Tool" || cells[1].StartsWith("---", StringComparison.Ordinal)) continue;
            rows.Add((cells[1].Trim('`'), Ticked().Matches(cells[2]).Select(m => m.Groups[1].Value).ToArray()));
        }

        return rows;
    }

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex Ticked();
}
