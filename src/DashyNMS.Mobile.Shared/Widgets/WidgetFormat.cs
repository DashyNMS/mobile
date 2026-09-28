using System.Globalization;

namespace DashyNMS.Mobile.Widgets;

/// <summary>
/// The words the widgets put round a snapshot's numbers, worked out as they
/// draw so ages keep up while the snapshot doesn't change. Android's widgets
/// use these directly; the iOS widget's Swift mirrors them
/// (<c>ios-widget/DashyNMSWidget/Format.swift</c>): change both together.
/// </summary>
public static class WidgetFormat
{
    /// <summary>"just now", "12 min", "3 hr", "1 day", "4 days" - short enough for a widget row.</summary>
    public static string Age(long unixSeconds, DateTimeOffset now)
    {
        if (unixSeconds <= 0)
        {
            return string.Empty;
        }

        var age = now - DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        return age.TotalMinutes < 1 ? "just now"
            : age.TotalHours < 1 ? $"{(int)age.TotalMinutes} min"
            : age.TotalDays < 1 ? $"{(int)age.TotalHours} hr"
            : (int)age.TotalDays == 1 ? "1 day"
            : $"{(int)age.TotalDays} days";
    }

    /// <summary>"Checked 2 min ago", so a stale widget looks stale.</summary>
    public static string Checked(long unixSeconds, DateTimeOffset now, string verb = "Checked")
    {
        var age = Age(unixSeconds, now);
        return age.Length == 0 ? string.Empty : age == "just now" ? $"{verb} just now" : $"{verb} {age} ago";
    }

    /// <summary>A pinned device's second line: "Down for 12 min" rather than a bare "Down" when its alert says since when.</summary>
    public static string DeviceStatus(WidgetDevice device, DateTimeOffset now) =>
        device.State == "down" && device.Since > 0 && Age(device.Since, now) is { Length: > 0 } age && age != "just now"
            ? $"Down for {age}"
            : device.Status;

    /// <summary>A count that fits a widget: "99+" past two digits.</summary>
    public static string Count(int value) => value > 99 ? "99+" : value.ToString(CultureInfo.CurrentCulture);

    /// <summary>
    /// The pie chart's slices, as fractions of a circle in drawing order
    /// (critical, warning, acknowledged, OK). A slice that exists always
    /// gets a sliver, so one critical device among thousands still shows.
    /// </summary>
    public static IReadOnlyList<(string State, double Fraction)> PieSlices(WidgetDeviceCounts counts)
    {
        const double MinimumSlice = 0.015;
        var parts = new (string State, int Count)[]
        {
            ("critical", counts.Critical),
            ("warning", counts.Warning),
            ("acknowledged", counts.Acknowledged),
            ("ok", counts.Ok),
        };
        var total = parts.Sum(p => p.Count);
        if (total == 0)
        {
            return [];
        }

        var raw = parts.Where(p => p.Count > 0).Select(p => (p.State, Fraction: Math.Max((double)p.Count / total, MinimumSlice))).ToList();
        var sum = raw.Sum(p => p.Fraction);
        return raw.Select(p => (p.State, p.Fraction / sum)).ToList();
    }

    /// <summary>"42/48" devices with no open alerts, for the middle of the ring.</summary>
    public static string OkShare(WidgetDeviceCounts counts)
    {
        var watched = counts.Critical + counts.Warning + counts.Acknowledged + counts.Ok;
        return $"{counts.Ok}/{watched}";
    }
}
