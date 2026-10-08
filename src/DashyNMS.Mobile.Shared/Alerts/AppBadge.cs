using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Alerts;

/// <summary>
/// The count on the app icon - the phone's version of desktop's Alerts tab
/// badge and tray icon.
/// </summary>
public interface IAppBadge
{
    /// <summary>False where the platform can't put a number on the icon, so Settings doesn't offer it.</summary>
    bool IsSupported { get; }

    /// <summary>Shows <paramref name="count"/> on the app icon; 0 clears it.</summary>
    void SetCount(int count);
}

/// <summary>
/// Where the platform has no way to set a number (Android: launchers show a
/// dot or count from the app's notifications instead), and in tests.
/// </summary>
public sealed class NoAppBadge : IAppBadge
{
    public bool IsSupported => false;

    public void SetCount(int count)
    {
    }
}

/// <summary>What the badge counts - desktop's Alerts tab badge rule, from desktop's own settings.</summary>
public static class AlertBadge
{
    /// <summary>
    /// Active alerts, plus acknowledged ones when
    /// <see cref="AppSettings.AlertTabBadgeIncludesAcknowledged"/> says so -
    /// never recovered ones - or 0 with <see cref="AppSettings.ShowAlertTabBadge"/> off.
    /// Only those at <paramref name="minimum"/> severity or above
    /// (<see cref="AlertCountThreshold"/>, #96), decided by Core's
    /// <see cref="AlertCounting"/> so desktop's badge counts the same (#168).
    /// </summary>
    public static int Count(IEnumerable<Alert> alerts, AppSettings settings, AlertSeverity minimum = AlertSeverity.Ok) =>
        Counted(alerts, settings, minimum).Count();

    /// <summary>The alerts <see cref="Count"/> counts; the Alerts tab's dot (<see cref="AlertTabDot"/>) looks at the same ones.</summary>
    public static IEnumerable<Alert> Counted(IEnumerable<Alert> alerts, AppSettings settings, AlertSeverity minimum = AlertSeverity.Ok) => settings.ShowAlertTabBadge
        ? alerts.Where(a => AlertCounting.Counts(a, minimum, settings.AlertTabBadgeIncludesAcknowledged))
        : [];
}
