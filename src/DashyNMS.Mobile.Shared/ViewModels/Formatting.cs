namespace DashyNMS.Mobile.ViewModels;

internal static class Formatting
{
    /// <summary>LibreNMS uptime (seconds) as "12d 4h", "3h 20m", "45m" or "—" when unknown.</summary>
    public static string Uptime(long seconds)
    {
        if (seconds <= 0)
        {
            return "—";
        }

        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalDays >= 1 ? $"{(int)span.TotalDays}d {span.Hours}h"
            : span.TotalHours >= 1 ? $"{span.Hours}h {span.Minutes}m"
            : $"{Math.Max(1, span.Minutes)}m";
    }

    /// <summary>How long ago: "just now", "5m ago", "3h ago", "2d ago".</summary>
    public static string Age(TimeSpan age) =>
        age.TotalMinutes < 1 ? "just now"
        : age.TotalHours < 1 ? $"{(int)age.TotalMinutes}m ago"
        : age.TotalDays < 1 ? $"{(int)age.TotalHours}h ago"
        : $"{(int)age.TotalDays}d ago";
}
