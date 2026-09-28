using System.Globalization;

namespace DashyNMS.Mobile.DeviceSections;

/// <summary>Human-readable rates, sizes and durations for the Device View sections.</summary>
public static class Units
{
    private static readonly string[] BitPrefixes = ["b/s", "kb/s", "Mb/s", "Gb/s", "Tb/s"];
    private static readonly string[] BytePrefixes = ["B", "KB", "MB", "GB", "TB", "PB"];

    /// <summary>Bits per second in network units (1000s): "12.3 Mb/s".</summary>
    public static string Bits(double? bitsPerSecond) => Scale(bitsPerSecond, 1000, BitPrefixes);

    /// <summary>A port's speed - "1 Gb/s", "100 Mb/s" - without needless decimals.</summary>
    public static string Speed(long? bitsPerSecond) => bitsPerSecond is > 0 ? Scale(bitsPerSecond, 1000, BitPrefixes) : "—";

    /// <summary>Storage in 1024s, as LibreNMS shows it: "37.2 GB".</summary>
    public static string Bytes(long? bytes) => Scale(bytes, 1024, BytePrefixes);

    public static string Percent(double? value) =>
        value is { } v ? v.ToString(v >= 99.995 && v < 100 ? "0.000" : v >= 10 ? "0.#" : "0.##", CultureInfo.CurrentCulture) + "%" : "—";

    /// <summary>"3d 4h", "2h 5m", "45m", "12s".</summary>
    public static string Duration(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        return span.TotalDays >= 1 ? $"{(int)span.TotalDays}d {span.Hours}h"
            : span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m"
            : span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes}m"
            : $"{(int)span.TotalSeconds}s";
    }

    /// <summary>"Last 24 hours" for 86400 and friends; otherwise the duration itself.</summary>
    public static string Window(long seconds) => seconds switch
    {
        86400 => "Last 24 hours",
        604800 => "Last 7 days",
        2592000 => "Last 30 days",
        31536000 => "Last 365 days",
        _ => "Last " + Duration(TimeSpan.FromSeconds(seconds)),
    };

    private static string Scale(double? value, double step, string[] units)
    {
        if (value is not { } v || double.IsNaN(v))
        {
            return "—";
        }

        var unit = 0;
        while (Math.Abs(v) >= step && unit < units.Length - 1)
        {
            v /= step;
            unit++;
        }

        var format = unit == 0 || Math.Abs(v) >= 100 || v == Math.Floor(v) ? "0" : "0.#";
        return v.ToString(format, CultureInfo.CurrentCulture) + " " + units[unit];
    }
}
