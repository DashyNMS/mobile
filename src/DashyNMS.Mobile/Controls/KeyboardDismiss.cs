namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Closes the keyboard once a search is done (issue #36): when the keyboard's
/// search key is pressed, and when the list below is scrolled - the two
/// things people do next. Set on every SearchBar and CollectionView through
/// the implicit styles in Styles.xaml, so no page has to wire it up.
/// </summary>
public static class KeyboardDismiss
{
    /// <summary>On a SearchBar: its search key closes the keyboard, and scrolling a list closes it while it's focused.</summary>
    public static readonly BindableProperty OnSearchProperty = BindableProperty.CreateAttached(
        "OnSearch", typeof(bool), typeof(KeyboardDismiss), false, propertyChanged: OnSearchChanged);

    /// <summary>On a CollectionView: scrolling it closes the keyboard of whichever search box has it.</summary>
    public static readonly BindableProperty OnScrollProperty = BindableProperty.CreateAttached(
        "OnScroll", typeof(bool), typeof(KeyboardDismiss), false, propertyChanged: OnScrollChanged);

    /// <summary>Only one search box has the keyboard at a time; weak so a closed page isn't kept alive.</summary>
    private static WeakReference<SearchBar>? _focused;

    public static bool GetOnSearch(BindableObject view) => (bool)view.GetValue(OnSearchProperty);

    public static void SetOnSearch(BindableObject view, bool value) => view.SetValue(OnSearchProperty, value);

    public static bool GetOnScroll(BindableObject view) => (bool)view.GetValue(OnScrollProperty);

    public static void SetOnScroll(BindableObject view, bool value) => view.SetValue(OnScrollProperty, value);

    private static void OnSearchChanged(BindableObject view, object oldValue, object newValue)
    {
        if (view is not SearchBar searchBar)
        {
            return;
        }

        searchBar.SearchButtonPressed -= OnSearchPressed;
        searchBar.Focused -= OnFocused;
        searchBar.Unfocused -= OnUnfocused;

        if (newValue is true)
        {
            searchBar.SearchButtonPressed += OnSearchPressed;
            searchBar.Focused += OnFocused;
            searchBar.Unfocused += OnUnfocused;
        }
    }

    private static void OnScrollChanged(BindableObject view, object oldValue, object newValue)
    {
        if (view is not ItemsView list)
        {
            return;
        }

        list.Scrolled -= OnScrolled;
        if (newValue is true)
        {
            list.Scrolled += OnScrolled;
        }
    }

    private static void OnFocused(object? sender, FocusEventArgs e)
    {
        if (sender is SearchBar searchBar)
        {
            _focused = new WeakReference<SearchBar>(searchBar);
        }
    }

    private static void OnUnfocused(object? sender, FocusEventArgs e)
    {
        if (_focused is not null && _focused.TryGetTarget(out var focused) && ReferenceEquals(focused, sender))
        {
            _focused = null;
        }
    }

    private static void OnSearchPressed(object? sender, EventArgs e)
    {
        if (sender is SearchBar searchBar)
        {
            Hide(searchBar);
        }
    }

    private static void OnScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        // Scrolled also fires as a list lays out; only a real drag moves it
        // far enough to matter, and nothing happens without a focused box.
        if (Math.Abs(e.VerticalDelta) < 4 || _focused is null || !_focused.TryGetTarget(out var searchBar))
        {
            return;
        }

        Hide(searchBar);
    }

    private static void Hide(SearchBar searchBar)
    {
        _focused = null;

        // Unfocus alone leaves Android's keyboard up; hiding alone leaves
        // iOS's search box focused, which brings the keyboard straight back.
        _ = searchBar.HideSoftInputAsync(CancellationToken.None);
        searchBar.Unfocus();
    }
}
