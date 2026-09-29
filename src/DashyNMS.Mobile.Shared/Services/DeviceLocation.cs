using System.Text.Json;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Services;

/// <summary>A device's location as its name, whatever shape LibreNMS sent it in.</summary>
public static class DeviceLocation
{
    /// <summary>
    /// <see cref="Device.Location"/>, readable. With geocoding on, LibreNMS
    /// sends the location as an object (<c>{"location": "Server room A",
    /// "lat": ...}</c>), which Core keeps as that object's raw JSON so the
    /// device still reads - and which showed as JSON in the Device card.
    /// This takes its name (<c>location</c>, else <c>name</c>) back out.
    /// </summary>
    public static string? LocationName(this Device device) => Name(device.Location);

    internal static string? Name(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        if (!text.StartsWith('{'))
        {
            return text;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            foreach (var property in new[] { "location", "name" })
            {
                if (document.RootElement.TryGetProperty(property, out var value)
                    && value.ValueKind == JsonValueKind.String
                    && value.GetString() is { Length: > 0 } name)
                {
                    return name.Trim();
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON after all: the text as it came.
        }

        return text;
    }
}
