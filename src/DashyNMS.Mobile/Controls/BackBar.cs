namespace DashyNMS.Mobile.Controls;

/// <summary>
/// The mock-ups' top bar for a pushed page: "‹ Devices" in the accent
/// colour on the left, the page's glyphs on the right, and no title - the
/// page's own header card names the device or alert. The navigation bar is
/// hidden in its place.
/// </summary>
/// <remarks>
/// Call <see cref="Apply"/> from the page's OnAppearing. Its row is the
/// Dashboard's top line (#253), so "‹ Devices" sits where every page's title
/// does; the page puts no top padding of its own above it.
/// </remarks>
public sealed class BackBar : Grid
{
    private readonly IconButton _back = new() { Kind = IconButtonKind.Plain, Icon = Icons.Back, Text = "Back", Padding = new Thickness(0, 0, 8, 0) };
    private readonly InlineGlassFallback _fallback = new();
    private WeakReference<ContentPage>? _ownerPage;
    private bool _showsBack = true;
#if IOS
    private bool _hiddenForGlass;
    private Action? _fallbackBack;
#endif

    public BackBar()
    {
        ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)];
        RowDefinitions = [new RowDefinition(new GridLength(TopBar.Height))];
        Padding = new Thickness(0, TopBar.Offset, 0, 0);
        TopBar.Fit(this);
        _back.HorizontalOptions = LayoutOptions.Start;
        _back.VerticalOptions = LayoutOptions.Center;
        _back.Command = new Command(OnBackTapped);
        Add(_back);
        SetColumn((IView)_fallback, 2);
        Add(_fallback);
#if IOS
        Loaded += (_, _) =>
        {
            TabBarButtons.Moved -= OnGlassMoved;
            TabBarButtons.Moved += OnGlassMoved;
            RefreshFallback();
        };
        Unloaded += (_, _) => TabBarButtons.Moved -= OnGlassMoved;
#endif
    }

    private void OnBackTapped()
    {
#if IOS
        if (_fallbackBack is { } back)
        {
            back();
            return;
        }
#endif
        _ = Shell.Current.GoToAsync("..");
    }

#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(RefreshFallback);
#endif

    /// <summary>
    /// Off beside a list on a larger screen (#88): there's no page to go
    /// back from, but the glyphs at the right still belong.
    /// </summary>
    public bool ShowsBack
    {
        get => _showsBack;
        set
        {
            _showsBack = value;
            _back.IsVisible = value;
        }
    }

    /// <summary>The page's glyphs, at the right: pin, export.</summary>
    public View? Actions
    {
        get => Children.OfType<View>().FirstOrDefault(v => v != _back && v != _fallback);
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
        _ownerPage = new WeakReference<ContentPage>(page);
        Shell.SetNavBarIsVisible(page, false);
        _back.Text = PreviousTitle(page);
        SemanticProperties.SetDescription(_back, "Back to " + _back.Text);
#if IOS
        RefreshFallback();
#endif
    }

#if IOS
    private void RefreshFallback()
    {
        if (_ownerPage is null || !_ownerPage.TryGetTarget(out var page))
        {
            return;
        }

        var fallback = TabBarButtons.GetFallback(page);
        _fallback.Sync(page);
        _fallbackBack = fallback.Active ? fallback.Back?.Tapped : null;
        if (fallback.Active && (fallback.Back is not null || fallback.Overflow.Count > 0))
        {
            if (!IsVisible) _hiddenForGlass = true;
            IsVisible = true;
            _back.IsVisible = fallback.Back is not null;
            if (fallback.Back is not null)
            {
                _back.Text = PreviousTitle(page);
                SemanticProperties.SetDescription(_back, fallback.Back.Label);
            }
        }
        else
        {
            _back.IsVisible = _showsBack;
            if (_hiddenForGlass)
            {
                _hiddenForGlass = false;
                IsVisible = false;
            }
        }
    }
#endif

    /// <summary>
    /// The title of the page before <paramref name="page"/> - or its tab's,
    /// when that was the tab's first page (Shell keeps that as null in the
    /// stack). Found by where the page is in the stack, since the first time
    /// it appears the stack may not hold it yet (#142).
    /// </summary>
    internal static string PreviousTitle(Page page)
    {
        var stack = page.Navigation.NavigationStack;
        var index = IndexOf(stack, page);
        if (index < 0 && Shell.Current?.Navigation.NavigationStack is { Count: > 0 } shellStack)
        {
            var shellIndex = IndexOf(shellStack, page);
            if (shellIndex >= 0 || stack.Count == 0)
            {
                stack = shellStack;
                index = shellIndex;
            }
        }

        var previous = index > 0 ? stack[index - 1] : index < 0 && stack.Count > 0 ? stack[^1] : null;
        if (ReferenceEquals(previous, page)) previous = null;
        var title = previous?.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            var tab = Shell.Current?.CurrentItem?.CurrentItem;
            title = (tab?.CurrentItem as IShellContentController)?.Page?.Title;
            if (string.IsNullOrWhiteSpace(title)) title = tab?.Title;
        }

        return string.IsNullOrWhiteSpace(title) ? "Back" : title;
    }

    private static int IndexOf(IReadOnlyList<Page> stack, Page page)
    {
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(stack[i], page)) return i;
        }

        return -1;
    }
}
