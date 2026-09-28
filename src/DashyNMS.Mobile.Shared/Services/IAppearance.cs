namespace DashyNMS.Mobile.Services;

/// <summary>Light or dark: the phone's own setting, or one fixed.</summary>
public enum AppearanceChoice
{
    System,
    Light,
    Dark,
}

/// <summary>
/// The app's light/dark appearance, kept as a phone preference rather than in
/// desktop's settings.
/// </summary>
/// <remarks>
/// Desktop's <c>AppSettings.Theme</c> defaults to Dark, and a settings file
/// can't tell "never chosen" from "chose Dark" - so following it would switch
/// every existing phone to dark mode. On a phone, following the system is the
/// right default.
/// </remarks>
public interface IAppearance
{
    AppearanceChoice Current { get; }

    /// <summary>Remembers <paramref name="choice"/> and applies it straight away.</summary>
    void Set(AppearanceChoice choice);
}

/// <summary>Kept in memory only: the default until the app head supplies the platform's, and in tests.</summary>
public sealed class InMemoryAppearance : IAppearance
{
    public AppearanceChoice Current { get; private set; }

    public void Set(AppearanceChoice choice) => Current = choice;
}
