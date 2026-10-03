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

    /// <summary>
    /// Off beside a list on a larger screen (#88): there's no page to go
    /// back from, but the glyphs at the right still belong.
    /// </summary>
    public bool ShowsBack
    {
        get => _back.IsVisible;
        set => _back.IsVisible = value;
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
        _back.Text = PreviousTitle(page);
        SemanticProperties.SetDescription(_back, "Back to " + _back.Text);
    }

    /// <summary>
    /// The title of the page before <paramref name="page"/> - or its tab's,
    /// when that was the tab's first page (Shell keeps that as null in the
    /// stack). Found by where the page is in the stack, since the first time
    /// it appears the stack may not hold it yet (#142).
    /// </summary>
    internal static string PreviousTitle(Page page)
    {
        var stack = page.Navigation.NavigationStack;
        var index = -1;
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(stack[i], page))
            {
                index = i;
                break;
            }
        }

        var previous = index > 0 ? stack[index - 1] : index < 0 && stack.Count > 0 ? stack[^1] : null;
        var title = previous?.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = Shell.Current?.CurrentItem?.CurrentItem?.Title;
        }

        return string.IsNullOrWhiteSpace(title) ? "Back" : title;
    }
}
