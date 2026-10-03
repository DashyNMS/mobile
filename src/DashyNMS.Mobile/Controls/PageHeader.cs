namespace DashyNMS.Mobile.Controls;

/// <summary>
/// A page's big in-page title (#69), as in the mock-ups, with the page's
/// actions beside it. The system navigation bar is never shown (#142): a
/// page that's the root of its tab shows just this, and the same page pushed
/// from More, Devices or Alerts shows "‹ More" above it, as <see cref="BackBar"/>
/// does - so a page looks the same however it was reached.
/// </summary>
/// <remarks>
/// <para>Call <see cref="Apply"/> from the page's OnAppearing.</para>
/// <para>It used to hand a pushed page the system bar instead, deciding by the
/// navigation stack's size in OnAppearing. The first time a page is pushed
/// the stack doesn't hold it yet, so it looked like a tab's root and got this
/// header; coming back to it, the stack was right and it switched to the
/// system bar.</para>
/// </remarks>
public sealed class PageHeader : Grid
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(PageHeader), string.Empty,
        propertyChanged: (view, _, value) => ((PageHeader)view)._title.Text = value as string);

    private readonly Label _title = new() { Style = (Style)Application.Current!.Resources["PageTitle"] };

    private readonly IconButton _back = new()
    {
        Kind = IconButtonKind.Plain,
        Icon = Icons.Back,
        Text = "Back",
        Padding = new Thickness(0, 0, 8, 0),
        HorizontalOptions = LayoutOptions.Start,
        IsVisible = false,
    };

    private EventHandler? _titleTapped;

    public PageHeader()
    {
        ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)];
        RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];
        MinimumHeightRequest = 52;
        Margin = new Thickness(0, 4, 0, 4);

        _back.Command = new Command(async () => await Shell.Current.GoToAsync(".."));
        Add(_back);
        SetRow(_title, 1);
        Add(_title);
        SemanticProperties.SetHeadingLevel(_title, SemanticHeadingLevel.Level1);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// A tap on the title itself - Settings counts them for its easter egg
    /// (#99). The title only listens once something does, so other pages'
    /// titles stay plain headings for VoiceOver.
    /// </summary>
    public event EventHandler? TitleTapped
    {
        add
        {
            if (_titleTapped is null)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += (_, _) => _titleTapped?.Invoke(this, EventArgs.Empty);
                _title.GestureRecognizers.Add(tap);
            }

            _titleTapped += value;
        }

        remove => _titleTapped -= value;
    }

    /// <summary>The page's actions, at the title's right.</summary>
    public View? Actions
    {
        get => Children.OfType<View>().FirstOrDefault(v => v != _title && v != _back);
        set
        {
            if (Actions is { } old)
            {
                Remove(old);
            }

            if (value is not null)
            {
                SetRow((IView)value, 1);
                SetColumn((IView)value, 1);
                value.VerticalOptions = LayoutOptions.Center;
                Add(value);
            }
        }
    }

    /// <summary>
    /// Hides the navigation bar, and shows "‹ More" (or wherever it came from)
    /// when <paramref name="page"/> was pushed rather than being its tab's own.
    /// </summary>
    public void Apply(ContentPage page)
    {
        Shell.SetNavBarIsVisible(page, false);
        IsVisible = true;

        var pushed = IsPushed(page);
        _back.IsVisible = pushed;
        if (pushed)
        {
            _back.Text = BackBar.PreviousTitle(page);
            SemanticProperties.SetDescription(_back, "Back to " + _back.Text);
        }
    }

    /// <summary>
    /// Whether <paramref name="page"/> was pushed: it isn't the page its tab
    /// itself shows. Asked of Shell rather than counted from the navigation
    /// stack, which may not hold the page yet when it first appears.
    /// </summary>
    internal static bool IsPushed(Page page)
    {
        if (Shell.Current?.CurrentItem?.CurrentItem?.CurrentItem is IShellContentController content && content.Page is { } root)
        {
            return !ReferenceEquals(root, page);
        }

        return page.Navigation.NavigationStack.Count > 1;
    }
}
