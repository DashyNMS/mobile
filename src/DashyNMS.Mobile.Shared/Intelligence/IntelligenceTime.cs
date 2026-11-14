using System.Globalization;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// Times and durations in lookup text, written for the person rather than as UTC or raw seconds (#636).
/// </summary>
/// <remarks>
/// The on-device model is unreliable at converting a UTC timestamp to local time or seconds to days,
/// and desktop's instructions ask for local times and say an alert's age isn't an outage's length.
/// So a lookup states each time in the person's own time zone with its date, and each length in days
/// and hours, and the model only has to copy them.
/// </remarks>
public static class IntelligenceTime
{
    /// <summary>"20 Oct 2026 08:30" in the person's time zone. Invariant month names: the app is in English.</summary>
    public static string At(DateTime utc, TimeProvider time)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(Utc(utc), time.LocalTimeZone).ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>"24 days 17 hours", "3 hours", "40 minutes" or "under a minute"; never negative.</summary>
    public static string Length(TimeSpan length)
    {
        if (length < TimeSpan.Zero) length = TimeSpan.Zero;
        if (length.TotalDays >= 1)
        {
            var days = (int)length.TotalDays;
            return Count(days, "day") + (length.Hours > 0 ? " " + Count(length.Hours, "hour") : "");
        }

        if (length.TotalHours >= 1) return Count((int)length.TotalHours, "hour");
        return length.TotalMinutes >= 1 ? Count((int)length.TotalMinutes, "minute") : "under a minute";
    }

    /// <summary>"20 Oct 2026 08:30, 24 days 17 hours ago": when, and how long before this lookup.</summary>
    public static string AtAndAgo(DateTime utc, DateTimeOffset now, TimeProvider time)
        => At(utc, time) + ", " + Length(now.UtcDateTime - Utc(utc)) + " ago";

    /// <summary>Shared results give UTC; one without a kind is taken as UTC too.</summary>
    private static DateTime Utc(DateTime value)
        => value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    private static string Count(int n, string noun) => n.ToString(CultureInfo.InvariantCulture) + " " + noun + (n == 1 ? "" : "s");
}
