using System.Text.Json;
using System.Text.Json.Serialization;
using DashyNMS.Agent.Devices;
using DashyNMS.Agent.Summaries;
using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed partial class IntelligenceFactsTests
{
    [Theory]
    [InlineData("wireless", 12.345, "12.345")]
    [InlineData("wireless", 0.00012345, "0.00012345")]
    [InlineData("wireless", null, "unknown")]
    [InlineData("poe", 12.345, "12.345")]
    [InlineData("poe", null, "unknown")]
    [InlineData("availability", 99.9999, "99.9999")]
    public void Detail_measurements_preserve_precision_and_missing_values(string section, double? value, string expected)
    {
        var data = new DeviceDetailsData
        {
            Device = new DeviceBrief(1, "core-sw1.example.net", "Core switch", "192.0.2.1", "Lab", "up"),
            Wireless = section == "wireless" ? [new WirelessDetail("signal", "Radio", value, null)] : null,
            Poe = section == "poe" ? new PoeDetails([new PoeDetailBudget("Power", value, value, value, value)], null, null) : null,
            Availability = section == "availability" ? [new AvailabilityDetail("day", value!.Value)] : null,
        };
        var found = new DeviceDetailsResult("Details", data, [section], new Dictionary<string, int> { [section] = 1 });
        var text = CompactNetworkLookups.DeviceDetailRows(found, Now, new FakeTimeProvider(Now), true, "Core switch").Text;
        if (section == "wireless") Assert.Contains("value " + expected, text);
        if (section == "poe")
        {
            Assert.Contains("used " + expected + " W", text);
            Assert.Contains("total " + expected + " W", text);
            Assert.Contains("remaining " + expected + " W", text);
            Assert.Contains("used " + expected + "%", text);
        }
        if (section == "availability") Assert.Contains(expected + "%", text);
    }

    [Theory]
    [InlineData("inventory", true, "yes")]
    [InlineData("inventory", false, "no")]
    [InlineData("inventory", null, "unknown")]
    [InlineData("routing", true, "yes")]
    [InlineData("routing", false, "no")]
    [InlineData("routing", null, "unknown")]
    public void Nullable_detail_facts_preserve_known_negatives_and_missing_values(string section, bool? flag, string expected)
    {
        // The current Shared HTTP projection omits negative flags. Exercise the
        // complete typed contract independently, including an explicit false.
        var data = new DeviceDetailsData
        {
            Device = new DeviceBrief(1, "core-sw1.example.net", "Core switch", "192.0.2.1", "Lab", "up"),
            Inventory = section == "inventory" ? [new InventoryDetail("chassis", "Chassis", null, null, null, null, null, null, null, flag)] : null,
            Routing = section == "routing" ? new RoutingDetails([new BgpDetail("192.0.2.9", 64512, null, null, "idle", flag, null, null)], [], []) : null,
        };
        var found = new DeviceDetailsResult("Details", data, [section], new Dictionary<string, int> { [section] = 1 });
        var text = CompactNetworkLookups.DeviceDetailRows(found, Now, new FakeTimeProvider(Now), true, "Core switch").Text;
        Assert.Contains((section == "inventory" ? "replaceable " : "admin down ") + expected, text);
        var fields = Fields(JsonSerializer.SerializeToElement(new { data }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
        var path = section == "inventory" ? "inventory.replaceable" : "routing.bgp_sessions.admin_down";
        if (flag is not null) Assert.All(fields[path], fact => Assert.True(ShownFor(path, [fact.Value], fact.Scope, text)));
        else Assert.DoesNotContain(path, fields.Keys);
    }

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
