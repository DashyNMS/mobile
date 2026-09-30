using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// A list with the item you tap beside it, as Mail and Settings do on an
/// iPad (#88): once the page is wide enough (<see cref="ScreenLayout.SplitsListAndDetail"/>)
/// the list keeps to the left and <see cref="Detail"/> takes the rest; any
/// narrower and it's the list alone, and tapping opens a page as on a phone.
/// </summary>
/// <remarks>
/// It follows its own width, so Split View, Stage Manager, a Mac window
/// being resized and an iPhone Duo unfolding all change it, not only
/// rotation. The list is the child given in XAML; what's showing in the
/// pane is kept when the page narrows, and comes back when it widens.
/// </remarks>
public sealed class ListDetail : Grid
{
    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(ListDetail), string.Empty,
        propertyChanged: (view, _, value) => ((ListDetail)view)._placeholder.Text = (string)value);

    private readonly Grid _pane = new() { IsVisible = false };
    private readonly ContentView _detail = new();
    private readonly Label _placeholder = new()
    {
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
        HorizontalTextAlignment = TextAlignment.Center,
        Margin = new Thickness(24),
    };

    public ListDetail()
    {
        ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(0))];

        if (Application.Current?.Resources is { } resources)
        {
            _placeholder.Style = resources.TryGetValue("Caption", out var caption) ? caption as Style : null;
        }

        // A hairline between list and detail, as the cards' own strokes.
        var line = new BoxView { WidthRequest = 1, HorizontalOptions = LayoutOptions.Start };
        line.SetAppThemeColor(BoxView.ColorProperty, Colour("StrokeLight"), Colour("StrokeDark"));
        _pane.Add(_placeholder);
        _pane.Add(_detail);
        _pane.Add(line);
        this.Add(_pane, column: 1);

        SizeChanged += (_, _) => Adapt();
    }

    /// <summary>Shown in the pane until something's chosen: "Choose an alert to see it here."</summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>Whether the page is wide enough for the pane, so a tap should fill it rather than open a page.</summary>
    public bool IsSplit { get; private set; }

    /// <summary>What's in the pane - the tapped alert or device - or null for the placeholder.</summary>
    public View? Detail
    {
        get => _detail.Content;
        set
        {
            _detail.Content = value;
            _placeholder.IsVisible = value is null;
        }
    }

    private void Adapt()
    {
        if (Width <= 0)
        {
            return;
        }

        var split = ScreenLayout.SplitsListAndDetail(Width);
        ColumnDefinitions[0].Width = split ? new GridLength(ScreenLayout.ListPaneWidth(Width)) : GridLength.Star;
        ColumnDefinitions[1].Width = split ? GridLength.Star : new GridLength(0);
        _pane.IsVisible = split;
        IsSplit = split;
    }

    private static Color Colour(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color colour ? colour : Colors.Transparent;
}
