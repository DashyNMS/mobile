using System.Globalization;
using System.Text.Json;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// When LibreNMS last polled a device, and how long the poll took (#97).
/// Polls run every few minutes, so this says whether Device View's numbers
/// are current, which "last discovered" (every few hours) doesn't.
/// </summary>
/// <remarks>
/// Core's <see cref="Device"/> has no property for them yet, so they're
/// read from the payload's extra fields - see the upstream notes in
/// docs/DEVELOPING.md. Anything missing or unreadable is simply null.
/// </remarks>
internal static class LastPoll
{
    /// <summary>LibreNMS's "last_polled", as a server timestamp (for <see cref="DesktopNMS.Core.ServerTime"/>).</summary>
    public static DateTime? At(Device device)
    {
        if (Field(device, "last_polled") is not { ValueKind: JsonValueKind.String } value
            || value.GetString() is not { Length: > 0 } text)
        {
            return null;
        }

        // As LibreNMS writes it ("2026-10-01 09:41:07"), or ISO 8601 should that change.
        if (DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
            || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out at))
        {
            return at;
        }

        return null;
    }

    /// <summary>LibreNMS's "last_polled_timetaken": the poll's length in seconds, sent as a number or a string.</summary>
    public static double? Seconds(Device device)
    {
        var seconds = Field(device, "last_polled_timetaken") switch
        {
            { ValueKind: JsonValueKind.Number } number when number.TryGetDouble(out var value) => value,
            { ValueKind: JsonValueKind.String } text when double.TryParse(text.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) => value,
            _ => (double?)null,
        };
        return seconds is >= 0 ? seconds : null;
    }

    /// <summary>"took 4.2s", for beside the age.</summary>
    public static string? TookText(Device device) => Seconds(device) is { } seconds
        ? "took " + seconds.ToString(seconds < 10 ? "0.#" : "0", CultureInfo.CurrentCulture) + "s"
        : null;

    private static JsonElement? Field(Device device, string name) =>
        device.AdditionalData?.TryGetValue(name, out var value) == true ? value : null;
}
