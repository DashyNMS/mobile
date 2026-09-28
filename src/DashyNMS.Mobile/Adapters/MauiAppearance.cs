using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Adapters;

/// <summary>The light/dark choice, in the phone's preferences, applied through MAUI's UserAppTheme.</summary>
public sealed class MauiAppearance : IAppearance
{
    private const string Key = "appearance";

    public AppearanceChoice Current =>
        Enum.TryParse<AppearanceChoice>(Preferences.Default.Get(Key, nameof(AppearanceChoice.System)), out var choice)
            ? choice
            : AppearanceChoice.System;

    public void Set(AppearanceChoice choice)
    {
        Preferences.Default.Set(Key, choice.ToString());
        Apply();
    }

    /// <summary>At start-up, and whenever the choice changes.</summary>
    public void Apply()
    {
        if (Application.Current is { } app)
        {
            app.UserAppTheme = Current switch
            {
                AppearanceChoice.Light => AppTheme.Light,
                AppearanceChoice.Dark => AppTheme.Dark,
                _ => AppTheme.Unspecified,
            };
        }
    }
}
