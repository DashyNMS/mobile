using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Adapters;

/// <summary>Phone-only choices, such as the tab bar's pins, in MAUI's Preferences.</summary>
public sealed class MauiPreferences : IAppPreferences
{
    public string? Get(string key) => Preferences.Default.ContainsKey(key) ? Preferences.Default.Get(key, string.Empty) : null;

    public void Set(string key, string? value)
    {
        if (value is null)
        {
            Preferences.Default.Remove(key);
        }
        else
        {
            Preferences.Default.Set(key, value);
        }
    }
}
