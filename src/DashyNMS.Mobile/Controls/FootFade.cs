namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Full-width, input-transparent foot fade softening scrolling content where a
/// bottom tab bar, Liquid Glass or fixed foot control covers the bottom of a
/// viewport. Callers pass the covered height and <see cref="FootFade"/> adds
/// <see cref="LeadHeight"/> (24pt), fading from <c>Background*Clear</c> at 0 to
/// <c>Background*</c> at 92% opacity (<c>0xEB</c>) at 0.45 held to the base.
/// </summary>
public sealed class FootFade : BoxView
{
    /// <summary>Height added above the highest bottom obstruction before reaching full fade.</summary>
    public const double LeadHeight = 24;

    private const float PeakAlpha = 0xEB / 255f;

    public FootFade()
    {
        InputTransparent = true;
        VerticalOptions = LayoutOptions.End;
        HeightRequest = 0;
        IsVisible = false;
        AutomationProperties.SetIsInAccessibleTree(this, false);

        var lightClear = ThemeColour("BackgroundLightClear", Color.FromArgb("#00F4F6F9"));
        var darkClear = ThemeColour("BackgroundDarkClear", Color.FromArgb("#0011141A"));
        var lightPeak = ThemeColour("BackgroundLight", Color.FromArgb("#F4F6F9")).WithAlpha(PeakAlpha);
        var darkPeak = ThemeColour("BackgroundDark", Color.FromArgb("#11141A")).WithAlpha(PeakAlpha);

        var top = new GradientStop { Offset = 0 };
        top.SetAppThemeColor(GradientStop.ColorProperty, lightClear, darkClear);
        var peak = new GradientStop { Offset = 0.45f };
        peak.SetAppThemeColor(GradientStop.ColorProperty, lightPeak, darkPeak);

        Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = [top, peak],
        };
    }

    /// <summary>
    /// Updates the fade for <paramref name="coveredHeight"/> points of bottom
    /// obstruction inside the viewport; pass 0 to hide the fade when unobstructed.
    /// </summary>
    public void SetCoveredHeight(double coveredHeight)
    {
        if (!double.IsFinite(coveredHeight) || coveredHeight <= 0)
        {
            if (HeightRequest != 0) HeightRequest = 0;
            if (IsVisible) IsVisible = false;
            return;
        }

        var height = coveredHeight + LeadHeight;
        if (HeightRequest != height) HeightRequest = height;
        if (!IsVisible) IsVisible = true;
    }

    private static Color ThemeColour(string key, Color fallback) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color colour
            ? colour
            : fallback;
}
