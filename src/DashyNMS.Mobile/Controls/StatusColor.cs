using DashyNMS.Mobile.Converters;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Theme-aware status text colouring via MAUI's <c>SetAppThemeColor</c>.
/// Unlike a one-shot <see cref="StatusColorConverter"/> binding, this updates
/// open labels immediately whenever the system or app appearance switches (#703).
/// </summary>
public static class StatusColor
{
    public static readonly BindableProperty TextProperty = BindableProperty.CreateAttached(
        "Text",
        typeof(object),
        typeof(StatusColor),
        null,
        propertyChanged: OnTextChanged);

    public static object? GetText(BindableObject view) => view.GetValue(TextProperty);

    public static void SetText(BindableObject view, object? value) => view.SetValue(TextProperty, value);

    private static void OnTextChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is not Label label)
        {
            return;
        }

        var status = StatusColorConverter.Status(newValue);
        var lightKey = status is null ? "TextLight" : status + "TextLight";
        var darkKey = status is null ? "TextDark" : status + "TextDark";
        var light = Resource(lightKey) ?? Colors.Black;
        var dark = Resource(darkKey) ?? Colors.White;
        label.SetAppThemeColor(Label.TextColorProperty, light, dark);
    }

    private static Color? Resource(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var color) == true ? color as Color : null;
}
