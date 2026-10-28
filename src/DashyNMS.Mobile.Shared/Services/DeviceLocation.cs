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

    /// <summary>
    /// Fills in what the geocoded location object carries and the device row
    /// lacks - the location's id and coordinates - so the map can place the
    /// device (#84). Leaves anything already set alone.
    /// </summary>
    public static void CompleteFromLocationObject(Device device)
    {
        if (device.Location?.Trim() is not { } text || !text.StartsWith('{'))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            device.LocationId ??= Number(root, "id") is { } id && id == Math.Floor(id) ? (int)id : null;
            device.Latitude ??= Number(root, "lat");
            device.Longitude ??= Number(root, "lng");
        }
        catch (JsonException)
        {
            // Not JSON after all: nothing to add.
        }
    }

    /// <summary>A number, whether LibreNMS sent it as one or as text ("3.139").</summary>
    private static double? Number(JsonElement element, string property) =>
        !element.TryGetProperty(property, out var value) ? null
        : value.ValueKind == JsonValueKind.Number ? value.GetDouble()
        : value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

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
