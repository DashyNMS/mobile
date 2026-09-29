namespace DashyNMS.Mobile.Controls;

/// <summary>
/// The mock-ups' top bar for a pushed page: "‹ Devices" in the accent
/// colour on the left, the page's glyphs on the right, and no title - the
/// page's own header card names the device or alert. The navigation bar is
/// hidden in its place.
/// </summary>
/// <remarks>Call <see cref="Apply"/> from the page's OnAppearing.</remarks>
public sealed class BackBar : Grid
{
    private readonly IconButton _back = new() { Kind = IconButtonKind.Plain, Icon = Icons.Back, Text = "Back", Padding = new Thickness(0, 0, 8, 0) };

    public BackBar()
    {
        ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)];
        MinimumHeightRequest = 48;
        _back.HorizontalOptions = LayoutOptions.Start;
        _back.Command = new Command(async () => await Shell.Current.GoToAsync(".."));
        Add(_back);
    }

    /// <summary>The page's glyphs, at the right: pin, export.</summary>
    public View? Actions
    {
        get => Children.OfType<View>().FirstOrDefault(v => v != _back);
        set
        {
            if (Actions is { } old)
            {
                Remove(old);
            }

            if (value is not null)
            {
                SetColumn((IView)value, 2);
                value.VerticalOptions = LayoutOptions.Center;
                Add(value);
            }
        }
    }

    /// <summary>
    /// Hides the navigation bar and names the way back after the page before
    /// ("Devices", "Alerts", another device) - the tab's own title when that
    /// was the tab's first page, which Shell keeps as null in the stack.
    /// </summary>
    public void Apply(ContentPage page)
    {
        Shell.SetNavBarIsVisible(page, false);
        var stack = page.Navigation.NavigationStack;
        var previous = stack.Count > 1 ? stack[^2] : null;
        var title = previous?.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = Shell.Current?.CurrentItem?.CurrentItem?.Title;
        }

        _back.Text = string.IsNullOrWhiteSpace(title) ? "Back" : title;
        SemanticProperties.SetDescription(_back, "Back to " + _back.Text);
    }
}
