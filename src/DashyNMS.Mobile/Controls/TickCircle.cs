using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// A row's tick in selection mode (#85), as Mail and Photos draw it: an
/// empty ring, or a filled accent circle with a tick. Drawn rather than an
/// image, so it follows the theme.
/// </summary>
public sealed class TickCircle : Grid
{
    public static readonly BindableProperty IsTickedProperty = BindableProperty.Create(
        nameof(IsTicked), typeof(bool), typeof(TickCircle), false,
        propertyChanged: (view, _, value) => ((TickCircle)view).Show((bool)value));

    private readonly Ellipse _ring = new() { StrokeThickness = 1.5, WidthRequest = 22, HeightRequest = 22 };
    private readonly Path _tick = new()
    {
        Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M6 11.5 L9.5 15 L16 8")!,
        Stroke = Colors.White,
        StrokeThickness = 2,
        StrokeLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        WidthRequest = 22,
        HeightRequest = 22,
        Aspect = Stretch.None,
        IsVisible = false,
    };

    public TickCircle()
    {
        WidthRequest = 22;
        HeightRequest = 22;
        VerticalOptions = LayoutOptions.Center;
        Add(_ring);
        Add(_tick);
        Show(false);
    }

    public bool IsTicked
    {
        get => (bool)GetValue(IsTickedProperty);
        set => SetValue(IsTickedProperty, value);
    }

    private void Show(bool ticked)
    {
        _tick.IsVisible = ticked;
        if (ticked)
        {
            _ring.Fill = Colour("PrimaryStroke");
            _ring.Stroke = Colour("PrimaryStroke");
        }
        else
        {
            _ring.Fill = Colors.Transparent;
            _ring.Stroke = Colour(Application.Current?.RequestedTheme == AppTheme.Dark ? "TextFaintDark" : "TextFaintLight");
        }

        SemanticProperties.SetDescription(this, ticked ? "Selected" : "Not selected");
    }

    private static Color Colour(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color colour ? colour : Colors.Gray;
}
