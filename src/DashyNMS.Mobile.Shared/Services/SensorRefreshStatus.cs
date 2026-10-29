using DesktopNMS.Core.Services;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// Desktop's words for a sensor refresh that failed. A large LibreNMS can take
/// most of a minute to send the sensor list, so a timeout says which list was
/// slow and, when Core kept earlier readings, how old the ones on screen are
/// (DashyNMS/desktop#373) - rather than a bare "took too long" over them.
/// </summary>
public static class SensorRefreshStatus
{
    /// <summary>Shown over kept readings while the next sensor list loads.</summary>
    public const string Updating = "Updating…";

    /// <summary>Null when <paramref name="result"/> succeeded.</summary>
    public static string? Describe(SensorPollResult result)
    {
        if (result.Succeeded)
        {
            return null;
        }

        // Refreshing won't help, and Core keeps nothing for a refused token.
        if (result.IsPermissionDenied)
        {
            return result.ErrorMessage;
        }

        var reason = result.IsTimeout ? "LibreNMS took too long to send the sensor list" : result.ErrorMessage ?? "The sensor refresh failed";
        return result.ObservedAt is { } time
            ? $"{reason} - showing readings from {time.LocalDateTime:HH:mm}."
            : reason.TrimEnd('.') + ". Try refreshing again.";
    }

    /// <summary>Failed with nothing kept: there are no readings to show.</summary>
    public static bool HasNothingToShow(SensorPollResult result) => !result.Succeeded && result.ObservedAt is null;
}
