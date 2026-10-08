using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Alerts;

/// <summary>
/// The least serious alert the app icon's count and the Alerts tab's dot
/// take notice of (#96): Critical only, Critical and warning, or every alert
/// (the default, and how it always worked) - so a phone full of warnings
/// needn't show the same red number as an outage.
/// </summary>
/// <remarks>
/// <para>Desktop's settings since #168: <see cref="NotificationSettings.CountFrom"/>,
/// counted and worded by Core's <see cref="AlertCounting"/>, so desktop's tray
/// and Alerts badge count the same way (DashyNMS/desktop#269). This class is
/// the phone's way in to that setting.</para>
/// <para>It used to be a phone preference, <c>alerts.count.minimum</c>. A
/// value saved there is moved into the settings once, the first time this is
/// made, and the old key cleared.</para>
/// </remarks>
public sealed class AlertCountThreshold
{
    /// <summary>Where the phone kept it before #168.</summary>
    internal const string OldKey = "alerts.count.minimum";

    private readonly ISettingsStore _settings;

    public AlertCountThreshold(ISettingsStore settings, Services.IAppPreferences preferences)
    {
        _settings = settings;
        MoveOldSetting(preferences);
    }

    /// <summary>The choices, most inclusive first, as Settings lists them.</summary>
    public static IReadOnlyList<AlertSeverity> Choices => AlertCounting.Choices;

    public AlertSeverity Minimum
    {
        get => _settings.Current.Notifications.CountFrom;
        set
        {
            if (_settings.Current.Notifications.CountFrom == value)
            {
                return;
            }

            _settings.Current.Notifications.CountFrom = value;
            _settings.Save();
        }
    }

    /// <summary>"Every alert", "Critical and warning", "Critical only" - Core's words, as desktop's Settings.</summary>
    public static string Describe(AlertSeverity minimum) => AlertCounting.Describe(minimum);

    private void MoveOldSetting(Services.IAppPreferences preferences)
    {
        var old = preferences.Get(OldKey);
        if (old is null)
        {
            return;
        }

        // A choice made on the phone wins over the default; anything
        // unreadable is dropped, as it always read as the default.
        if (Enum.TryParse<AlertSeverity>(old, out var value) && Choices.Contains(value) && value != AlertSeverity.Ok)
        {
            Minimum = value;
        }

        preferences.Set(OldKey, null);
    }
}
