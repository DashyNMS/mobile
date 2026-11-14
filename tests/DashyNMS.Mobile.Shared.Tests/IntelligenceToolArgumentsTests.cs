using System.Text.Json;
using DashyNMS.Agent.Catalogue;
using DashyNMS.Mobile.Intelligence;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// Arguments checked against the catalogue before any request (#635), and the parameter list the
/// Swift bridge builds each tool's generation schema from.
/// </summary>
public sealed class IntelligenceToolArgumentsTests
{
    private static readonly IReadOnlyList<ToolParameter> Parameters =
    [
        new("device", ToolParameterType.String, "The device.") { Required = true },
        new("severity", ToolParameterType.String, "Severity.") { AllowedValues = ["critical", "warning", "ok"] },
        new("limit", ToolParameterType.Integer, "How many.") { Default = 10, Minimum = 1, Maximum = 50 },
        new("verbose", ToolParameterType.Boolean, "Every field.") { Default = false },
        new("sections", ToolParameterType.StringList, "Sections.") { AllowedValues = ["vlans", "fdb"] },
    ];

    private static IntelligenceToolArguments? Parse(string input, out string? error) => IntelligenceToolArguments.Parse(input, Parameters, "device", out error);

    [Fact]
    public void Typed_arguments_are_read_as_the_catalogue_types_them()
    {
        var arguments = Parse("""{"device":" core-sw1 ","severity":"Critical","limit":20,"verbose":true,"sections":["vlans"]}""", out var error);

        Assert.Null(error);
        Assert.Equal(" core-sw1 ", arguments!.Text("device"));
        Assert.Equal("Critical", arguments.Text("severity"));
        Assert.Equal(20, arguments.Number("limit"));
        Assert.True(arguments.Flag("verbose"));
        Assert.Equal(new[] { "vlans" }, arguments.List("sections")!);
    }

    [Fact]
    public void Nulls_and_blanks_read_as_left_out()
    {
        var arguments = Parse("""{"device":"core-sw1","severity":null,"limit":null}""", out _);

        Assert.Null(arguments!.Text("severity"));
        Assert.Null(arguments.Number("limit"));
        Assert.False(arguments.Flag("verbose"));
        Assert.True(arguments.Flag("verbose", fallback: true));
        Assert.Null(arguments.List("sections"));
    }

    [Fact]
    public void Plain_text_is_the_main_argument()
    {
        Assert.Equal("core-sw1", Parse("core-sw1", out _)!.Text("device"));
        Assert.Null(IntelligenceToolArguments.Parse("core-sw1", Parameters, null, out var error));
        Assert.Equal("The lookup wasn't run: arguments must be a JSON object.", error);
    }

    [Theory]
    [InlineData("{}", "device is required.")]
    [InlineData("""{"device":""}""", "device is required.")]
    [InlineData("""{"device":"  "}""", "device is required.")]
    [InlineData("""{"device":7}""", "device must be text.")]
    [InlineData("""{"device":"a","limit":"20"}""", "limit must be a whole number.")]
    [InlineData("""{"device":"a","limit":1.5}""", "limit must be a whole number.")]
    [InlineData("""{"device":"a","verbose":"yes"}""", "verbose must be true or false.")]
    [InlineData("""{"device":"a","sections":"vlans"}""", "sections must be a list of text.")]
    [InlineData("""{"device":"a","sections":["vlans",3]}""", "sections must be a list of text.")]
    [InlineData("""{"device":"a","severity":"loud"}""", "severity must be one of critical, warning, ok.")]
    [InlineData("""{"device":"a","sections":["vlans","routing"]}""", "sections must be one of vlans, fdb.")]
    [InlineData("""{"device":"a","limit":0}""", "limit must be from 1 to 50.")]
    [InlineData("""{"device":"a","limit":51}""", "limit must be from 1 to 50.")]
    [InlineData("""{"device":"a","offset":5}""", "an argument isn't one this lookup takes. It takes device, severity, limit, verbose, sections.")]
    [InlineData("""{"device":"a","device":"b"}""", "an argument was given twice.")]
    [InlineData("""["a"]""", "arguments must be a JSON object.")]
    [InlineData("""{"device":""", "arguments must be a JSON object.")]
    public void Arguments_outside_the_catalogue_are_refused_naming_only_the_parameter(string input, string problem)
    {
        Assert.Null(Parse(input, out var error));
        Assert.Equal("The lookup wasn't run: " + problem, error);
    }

    [Fact]
    public void Refusals_never_echo_the_models_own_text()
    {
        Assert.Null(Parse("""{"device":"a","password=synthetic-secret":"x"}""", out var unknown));
        Assert.Null(Parse("""{"device":"a","severity":"password=synthetic-secret"}""", out var value));

        Assert.DoesNotContain("synthetic-secret", unknown, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-secret", value, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlong_arguments_are_refused()
    {
        Assert.Null(Parse(new string('x', IntelligenceToolArguments.MaxInput + 1), out var error));
        Assert.Equal("The lookup wasn't run: the arguments are too long.", error);
    }

    [Fact]
    public void A_tool_without_parameters_takes_nothing()
    {
        Assert.NotNull(IntelligenceToolArguments.Parse("", [], null, out _));
        Assert.NotNull(IntelligenceToolArguments.Parse("{}", [], null, out _));
        Assert.Null(IntelligenceToolArguments.Parse("""{"query":"x"}""", [], null, out var error));
        Assert.Equal("The lookup wasn't run: an argument isn't one this lookup takes. It takes none.", error);
    }

    [Fact]
    public void The_bridge_schema_carries_each_parameter_as_the_catalogue_has_it()
    {
        using var json = JsonDocument.Parse(IntelligenceToolArguments.Schema(Parameters));
        var list = json.RootElement.EnumerateArray().ToList();

        Assert.Equal(new[] { "device", "severity", "limit", "verbose", "sections" }, list.Select(p => p.GetProperty("name").GetString()));
        Assert.Equal(new[] { "string", "string", "integer", "boolean", "stringList" }, list.Select(p => p.GetProperty("type").GetString()));
        Assert.Equal(new[] { true, false, false, false, false }, list.Select(p => p.GetProperty("required").GetBoolean()));
        Assert.Equal("The device.", list[0].GetProperty("description").GetString());
        Assert.Equal(new[] { "critical", "warning", "ok" }, list[1].GetProperty("allowedValues").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal(1, list[2].GetProperty("minimum").GetInt32());
        Assert.Equal(50, list[2].GetProperty("maximum").GetInt32());
        Assert.False(list[0].TryGetProperty("allowedValues", out _));
        Assert.False(list[3].TryGetProperty("minimum", out _));
        Assert.Equal("[]", IntelligenceToolArguments.Schema([]));
    }

    [Fact]
    public void Every_offered_tool_has_a_schema_the_bridge_can_read()
    {
        foreach (var name in CompactNetworkLookups.ReviewedTools)
        {
            var (_, parameters) = CompactNetworkLookups.Definition(name);
            using var json = JsonDocument.Parse(IntelligenceToolArguments.Schema(parameters));
            Assert.Equal(parameters.Select(p => p.Name), json.RootElement.EnumerateArray().Select(p => p.GetProperty("name").GetString()));
        }
    }
}
