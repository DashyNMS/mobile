using Microsoft.Maui.Controls.Shapes;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// A tab bar glyph drawn as a line in the theme's secondary text colour, for
/// places that show one outside the tab bar - the What's new sheet (#268).
/// </summary>
/// <remarks>
/// The tab bar tints its images itself; an <see cref="Image"/> elsewhere
/// shows them as drawn, black, which all but vanishes on a dark card. So the
/// same shapes are paths here, as <see cref="IconButton"/>'s are. They're
/// Resources/Images' tab_*.svg turned into path data (rectangles and circles
/// as their outlines): change both together.
/// </remarks>
public sealed class Glyph : ContentView
{
    /// <summary>Each tab image's shape, by the image's name.</summary>
    internal static readonly IReadOnlyDictionary<string, string> Shapes = new Dictionary<string, string>
    {
        ["tab_alertrules.png"] = "M3.5 6.5l1.5 1.5 3-3 M3.5 13.5l1.5 1.5 3-3 M11 7h9.5 M11 14h9.5 M11 20h9.5 M4.5 20h2",
        ["tab_alerts.png"] = "M6 9.5a6 6 0 0 1 12 0c0 5 2 6.5 2 6.5H4s2-1.5 2-6.5z M10 19.5a2.2 2.2 0 0 0 4 0",
        ["tab_dashboard.png"] = "M4.6 3H8.9A1.6 1.6 0 0 1 10.5 4.6V8.9A1.6 1.6 0 0 1 8.9 10.5H4.6A1.6 1.6 0 0 1 3 8.9V4.6A1.6 1.6 0 0 1 4.6 3Z M15.1 3H19.4A1.6 1.6 0 0 1 21 4.6V8.9A1.6 1.6 0 0 1 19.4 10.5H15.1A1.6 1.6 0 0 1 13.5 8.9V4.6A1.6 1.6 0 0 1 15.1 3Z M4.6 13.5H8.9A1.6 1.6 0 0 1 10.5 15.1V19.4A1.6 1.6 0 0 1 8.9 21H4.6A1.6 1.6 0 0 1 3 19.4V15.1A1.6 1.6 0 0 1 4.6 13.5Z M15.1 13.5H19.4A1.6 1.6 0 0 1 21 15.1V19.4A1.6 1.6 0 0 1 19.4 21H15.1A1.6 1.6 0 0 1 13.5 19.4V15.1A1.6 1.6 0 0 1 15.1 13.5Z",
        ["tab_devices.png"] = "M4.6 3.5H19.4A1.6 1.6 0 0 1 21 5.1V8.9A1.6 1.6 0 0 1 19.4 10.5H4.6A1.6 1.6 0 0 1 3 8.9V5.1A1.6 1.6 0 0 1 4.6 3.5Z M4.6 13.5H19.4A1.6 1.6 0 0 1 21 15.1V18.9A1.6 1.6 0 0 1 19.4 20.5H4.6A1.6 1.6 0 0 1 3 18.9V15.1A1.6 1.6 0 0 1 4.6 13.5Z M7 7h.01M10 7h.01M7 17h.01M10 17h.01 M15 7h3M15 17h3",
        ["tab_graylog.png"] = "M4 5.5h11M4 10h7M4 14.5h5 M12.2 15A3.8 3.8 0 1 0 19.8 15A3.8 3.8 0 1 0 12.2 15Z M18.8 17.8 21 20",
        ["tab_groups.png"] = "M12 3.5 3 8.2l9 4.7 9-4.7z M3 12.2l9 4.7 9-4.7 M3 16.2l9 4.7 9-4.7",
        ["tab_health.png"] = "M3 12h4l2.5-6 5 12 2.5-6H21",
        ["tab_logs.png"] = "M7 4H17A2 2 0 0 1 19 6V19A2 2 0 0 1 17 21H7A2 2 0 0 1 5 19V6A2 2 0 0 1 7 4Z M9 4V2.8h6V4 M8.5 9.5h7M8.5 13h7M8.5 16.5h4.5",
        ["tab_map.png"] = "M12 21s-6.5-5.6-6.5-11a6.5 6.5 0 0 1 13 0c0 5.4-6.5 11-6.5 11z M9.6 10A2.4 2.4 0 1 0 14.4 10A2.4 2.4 0 1 0 9.6 10Z",
        ["tab_more.png"] = "M4 6h16M4 12h16M4 18h16",
        ["tab_neighbours.png"] = "M4.5 3.5H8.5A1.5 1.5 0 0 1 10 5V7A1.5 1.5 0 0 1 8.5 8.5H4.5A1.5 1.5 0 0 1 3 7V5A1.5 1.5 0 0 1 4.5 3.5Z M15.5 15.5H19.5A1.5 1.5 0 0 1 21 17V19A1.5 1.5 0 0 1 19.5 20.5H15.5A1.5 1.5 0 0 1 14 19V17A1.5 1.5 0 0 1 15.5 15.5Z M6.5 8.5v4a2 2 0 0 0 2 2h9v1",
        ["tab_networkmap.png"] = "M9.5 5A2.5 2.5 0 1 0 14.5 5A2.5 2.5 0 1 0 9.5 5Z M2.5 18A2.5 2.5 0 1 0 7.5 18A2.5 2.5 0 1 0 2.5 18Z M16.5 18A2.5 2.5 0 1 0 21.5 18A2.5 2.5 0 1 0 16.5 18Z M10.8 7.2 6.2 15.8 M13.2 7.2l4.6 8.6 M7.5 18h9",
        ["tab_settings.png"] = "M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z M9 12A3 3 0 1 0 15 12A3 3 0 1 0 9 12Z",
    };

    public static readonly BindableProperty NameProperty = BindableProperty.Create(
        nameof(Name), typeof(string), typeof(Glyph), null,
        propertyChanged: (view, _, value) => ((Glyph)view).SetShape(value as string));

    // MAUI's Path is sealed, so the view owns the drawing rather than inheriting it.
    private readonly Microsoft.Maui.Controls.Shapes.Path _path = new()
    {
        StrokeThickness = 1.8,
        StrokeLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        WidthRequest = 24,
        HeightRequest = 24,
        Aspect = Stretch.None,
        Scale = 0.9,
    };

    public Glyph()
    {
        WidthRequest = 24;
        HeightRequest = 24;
        Content = _path;
        ApplyTheme();
        if (Application.Current is { } app)
        {
            app.RequestedThemeChanged += (_, _) => ApplyTheme();
        }
    }

    /// <summary>A tab image's name, "tab_alerts.png"; one without a shape draws nothing.</summary>
    public string? Name
    {
        get => (string?)GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    private void SetShape(string? name) =>
        _path.Data = name is not null && Shapes.TryGetValue(name, out var shape)
            ? (Geometry?)new PathGeometryConverter().ConvertFromInvariantString(shape)
            : null;

    private void ApplyTheme()
    {
        var key = Application.Current?.RequestedTheme == AppTheme.Dark ? "TextSecondaryDark" : "TextSecondaryLight";
        _path.Stroke = Application.Current?.Resources.TryGetValue(key, out var colour) == true && colour is Color c ? c : Colors.Gray;
    }
}
