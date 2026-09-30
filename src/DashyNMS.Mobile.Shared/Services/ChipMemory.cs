namespace DashyNMS.Mobile.Services;

/// <summary>
/// Which of a page's filter chips were on, kept between visits and launches
/// (#81) for the pages desktop's settings have no place for - Devices' state
/// chips, Health's severity chips. Alerts' live in desktop's own
/// <c>AlertFilterSettings</c>.
/// </summary>
public sealed class ChipMemory
{
    private readonly IAppPreferences _preferences;
    private readonly string _page;

    public ChipMemory(IAppPreferences preferences, string page)
    {
        _preferences = preferences;
        _page = page;
    }

    /// <summary>The chip as last left, or <paramref name="fallback"/> (on) if it never has been.</summary>
    public bool Get(string chip, bool fallback = true) =>
        _preferences.Get(Key(chip)) is { } value ? value == "1" : fallback;

    public void Set(string chip, bool on) => _preferences.Set(Key(chip), on ? "1" : "0");

    private string Key(string chip) => $"chips.{_page}.{chip}";
}
