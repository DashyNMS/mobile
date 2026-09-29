namespace DashyNMS.Mobile.Controls;

/// <summary>
/// A page's big in-page title (#69), as in the mock-ups: on a page that's
/// the root of its tab, the navigation bar is hidden and this shows instead,
/// with the page's actions beside it. Pushed from More, the page gets the
/// navigation bar back - for its back button - and this hides, so the
/// title isn't shown twice. Toolbar items only show with the bar, so pages
/// keep them for the pushed case and repeat them here.
/// </summary>
/// <remarks>Call <see cref="Apply"/> from the page's OnAppearing.</remarks>
public sealed class PageHeader : Grid
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(PageHeader), string.Empty,
        propertyChanged: (view, _, value) => ((PageHeader)view)._title.Text = value as string);

    private readonly Label _title = new() { Style = (Style)Application.Current!.Resources["PageTitle"] };

    public PageHeader()
    {
        ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)];
        MinimumHeightRequest = 52;
        Margin = new Thickness(0, 4, 0, 4);
        Add(_title);
        SemanticProperties.SetHeadingLevel(_title, SemanticHeadingLevel.Level1);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The page's actions, at the title's right.</summary>
    public View? Actions
    {
        get => Children.OfType<View>().FirstOrDefault(v => v != _title);
        set
        {
            if (Actions is { } old)
            {
                Remove(old);
            }

            if (value is not null)
            {
                SetColumn((IView)value, 1);
                value.VerticalOptions = LayoutOptions.Center;
                Add(value);
            }
        }
    }

    /// <summary>Shows this and hides the navigation bar when <paramref name="page"/> is its tab's first page.</summary>
    public void Apply(ContentPage page)
    {
        var isRoot = page.Navigation.NavigationStack.Count <= 1;
        Shell.SetNavBarIsVisible(page, !isRoot);
        IsVisible = isRoot;
    }
}
