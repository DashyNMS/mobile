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
    /// </summary>
    public static int Count(IEnumerable<Alert> alerts, AppSettings settings) => settings.ShowAlertTabBadge
        ? alerts.Count(a => a.State == AlertState.Active
            || (a.State == AlertState.Acknowledged && settings.AlertTabBadgeIncludesAcknowledged))
        : 0;
}
