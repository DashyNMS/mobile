using DashyNMS.Mobile.Services;
#if IOS
using Foundation;
using UIKit;
#endif

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
        if (Application.Current is not { } app)
        {
            return;
        }

        var choice = Current;
        app.UserAppTheme = choice switch
        {
            AppearanceChoice.Light => AppTheme.Light,
            AppearanceChoice.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };

#if IOS
        ApplyWindowStyle(choice);
        if (choice == AppearanceChoice.System)
        {
            SyncSystemTheme();
        }
#endif
    }

#if IOS
    /// <summary>
    /// Propagates a live iOS appearance change to MAUI when the app follows the
    /// system theme (<see cref="AppTheme.Unspecified"/>). Explicit Light/Dark
    /// overrides are left untouched.
    /// </summary>
    /// <remarks>
    /// On iOS 17+ and iPhone Duo, <c>TraitCollectionDidChange</c> can fire before
    /// child controllers update their traits or be skipped across scene/trait
    /// overrides, leaving MAUI surfaces on the previous theme while native chrome
    /// updates (#703). Notifying both immediately and on the next main run-loop
    /// turn is idempotent and ensures <see cref="Application.RequestedThemeChanged"/>
    /// fires once UIKit's trait hierarchy has settled.
    /// </remarks>
    internal static void SyncSystemTheme()
    {
        if (Application.Current is not { UserAppTheme: AppTheme.Unspecified } app)
        {
            return;
        }

        ((IApplication)app).ThemeChanged();
        NSRunLoop.Main.BeginInvokeOnMainThread(() =>
        {
            if (Application.Current is { UserAppTheme: AppTheme.Unspecified } current)
            {
                ((IApplication)current).ThemeChanged();
            }
        });
    }

    private static void ApplyWindowStyle(AppearanceChoice choice)
    {
        var style = choice switch
        {
            AppearanceChoice.Light => UIUserInterfaceStyle.Light,
            AppearanceChoice.Dark => UIUserInterfaceStyle.Dark,
            _ => UIUserInterfaceStyle.Unspecified,
        };

        foreach (var scene in UIApplication.SharedApplication.ConnectedScenes)
        {
            if (scene is UIWindowScene windowScene)
            {
                foreach (var window in windowScene.Windows)
                {
                    if (window.OverrideUserInterfaceStyle != style)
                    {
                        window.OverrideUserInterfaceStyle = style;
                    }
                }
            }
        }
    }
#endif
}
