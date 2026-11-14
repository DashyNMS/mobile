using System.Globalization;
using System.Text;
using System.Text.Json;
using DashyNMS.Agent.Catalogue;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// A lookup's arguments, checked against the catalogue parameters the phone offers before any request
/// (#635). The Swift bridge hands over the model's arguments as one JSON object.
/// </summary>
/// <remarks>
/// Unknown, duplicate and mistyped arguments are refused, as are values outside a parameter's
/// documented choices or range, so a model's slip never becomes a request. Refusals name the
/// parameter from the catalogue, never the model's own text, which could carry anything.
/// Plain text that isn't a JSON object is read as the tool's main argument (its query or device),
/// for tests and callers that predate the bridge's JSON.
/// </remarks>
public sealed class IntelligenceToolArguments
{
    private readonly Dictionary<string, object> values;

    private IntelligenceToolArguments(Dictionary<string, object> values) => this.values = values;

    /// <summary>Text the tool needs to tell the model, rather than run, when the arguments don't fit.</summary>
    public static string Refusal(string detail) => "The lookup wasn't run: " + detail;

    /// <summary>Longest argument text accepted, so a runaway answer can't become a request.</summary>
    internal const int MaxInput = 4096;

    /// <summary>
    /// Reads and checks the arguments. Returns null with <paramref name="error"/> set when they don't fit.
    /// </summary>
    /// <param name="input">The bridge's JSON object, empty for none, or plain text for <paramref name="primary"/>.</param>
    /// <param name="parameters">The parameters offered for this tool.</param>
    /// <param name="primary">The parameter plain text fills, or null when the tool has none.</param>
    public static IntelligenceToolArguments? Parse(string? input, IReadOnlyList<ToolParameter> parameters, string? primary, out string? error)
    {
        error = null;
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        var text = input?.Trim() ?? "";
        if (text.Length > MaxInput)
        {
            error = Refusal("the arguments are too long.");
            return null;
        }

        if (text.Length > 0 && text[0] is not ('{' or '['))
        {
            if (primary is null || parameters.All(p => p.Name != primary))
            {
                error = Refusal("arguments must be a JSON object.");
                return null;
            }

            values[primary] = text;
        }
        else if (text.Length > 0)
        {
            try
            {
                using var json = JsonDocument.Parse(text);
                if (json.RootElement.ValueKind != JsonValueKind.Object)
                {
                    error = Refusal("arguments must be a JSON object.");
                    return null;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in json.RootElement.EnumerateObject())
                {
                    if (!seen.Add(property.Name))
                    {
                        error = Refusal("an argument was given twice.");
                        return null;
                    }

                    if (parameters.FirstOrDefault(p => p.Name == property.Name) is not { } parameter)
                    {
                        error = Refusal("an argument isn't one this lookup takes. It takes " + Names(parameters) + ".");
                        return null;
                    }

                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }

                    if (Read(parameter, property.Value) is not { } value)
                    {
                        error = Refusal($"{parameter.Name} must be {Kind(parameter)}.");
                        return null;
                    }

                    values[parameter.Name] = value;
                }
            }
            catch (JsonException)
            {
                error = Refusal("arguments must be a JSON object.");
                return null;
            }
        }

        foreach (var parameter in parameters)
        {
            // A blank required text is as good as none: models often send "" for a field they can't fill.
            if (!values.TryGetValue(parameter.Name, out var value) || (parameter.Required && value is string blank && string.IsNullOrWhiteSpace(blank)))
            {
                if (parameter.Required)
                {
                    error = Refusal($"{parameter.Name} is required.");
                    return null;
                }

                continue;
            }

            if (Check(parameter, value) is { } problem)
            {
                error = Refusal(problem);
                return null;
            }
        }

        return new(values);
    }

    /// <summary>
    /// A text argument exactly as given, or null when it was left out or blank. Not trimmed: a group
    /// or location name is matched exactly by the shared operation, which trims what it must itself.
    /// </summary>
    public string? Text(string name)
        => values.TryGetValue(name, out var value) && value is string text && !string.IsNullOrWhiteSpace(text) ? text : null;

    /// <summary>A boolean argument, or <paramref name="fallback"/> when it was left out.</summary>
    public bool Flag(string name, bool fallback = false) => values.TryGetValue(name, out var value) && value is bool flag ? flag : fallback;

    /// <summary>An integer argument, or null when it was left out.</summary>
    public int? Number(string name) => values.TryGetValue(name, out var value) && value is int number ? number : null;

    /// <summary>A string-list argument, or null when it was left out.</summary>
    public IReadOnlyList<string>? List(string name) => values.TryGetValue(name, out var value) ? value as IReadOnlyList<string> : null;

    private static object? Read(ToolParameter parameter, JsonElement value) => parameter.Type switch
    {
        ToolParameterType.String when value.ValueKind == JsonValueKind.String => value.GetString(),
        ToolParameterType.Integer when value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) => number,
        ToolParameterType.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
        ToolParameterType.StringList when value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(i => i.ValueKind == JsonValueKind.String)
            => value.EnumerateArray().Select(i => i.GetString()!).ToArray(),
        _ => null,
    };

    /// <summary>The documented choices and range, which the bridge's schema also guides the model to.</summary>
    private static string? Check(ToolParameter parameter, object value)
    {
        if (parameter.AllowedValues is { } allowed)
        {
            IReadOnlyList<string> given = value switch
            {
                string text => new[] { text },
                IReadOnlyList<string> list => list,
                _ => Array.Empty<string>(),
            };
            if (given.Any(g => !string.IsNullOrWhiteSpace(g) && !allowed.Contains(g.Trim(), StringComparer.OrdinalIgnoreCase)))
            {
                return $"{parameter.Name} must be one of {string.Join(", ", allowed)}.";
            }
        }

        if (value is int number && ((parameter.Minimum is { } min && number < min) || (parameter.Maximum is { } max && number > max)))
        {
            return $"{parameter.Name} must be from {Bound(parameter.Minimum)} to {Bound(parameter.Maximum)}.";
        }

        return null;
    }

    private static string Bound(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "any";

    private static string Names(IReadOnlyList<ToolParameter> parameters)
        => parameters.Count == 0 ? "none" : string.Join(", ", parameters.Select(p => p.Name));

    private static string Kind(ToolParameter parameter) => parameter.Type switch
    {
        ToolParameterType.Integer => "a whole number",
        ToolParameterType.Boolean => "true or false",
        ToolParameterType.StringList => "a list of text",
        _ => "text",
    };

    /// <summary>
    /// The offered parameters as the Swift bridge reads them to build each tool's generation schema:
    /// a JSON array of name, type, required, description and any documented values or range. Written
    /// by hand rather than serialised by reflection, which iOS's trimming and AOT would warn about.
    /// </summary>
    public static string Schema(IReadOnlyList<ToolParameter> parameters)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var p in parameters)
            {
                writer.WriteStartObject();
                writer.WriteString("name", p.Name);
                writer.WriteString("type", p.Type switch
                {
                    ToolParameterType.Integer => "integer",
                    ToolParameterType.Boolean => "boolean",
                    ToolParameterType.StringList => "stringList",
                    _ => "string",
                });
                writer.WriteBoolean("required", p.Required);
                writer.WriteString("description", p.Description);
                if (p.AllowedValues is { Count: > 0 } allowed)
                {
                    writer.WriteStartArray("allowedValues");
                    foreach (var value in allowed) writer.WriteStringValue(value);
                    writer.WriteEndArray();
                }

                if (p.Minimum is { } min) writer.WriteNumber("minimum", min);
                if (p.Maximum is { } max) writer.WriteNumber("maximum", max);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
