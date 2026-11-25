namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Shared inline fallback controls shown in a page header only when zero 44pt
/// Liquid Glass targets fit in the tab bar's lane. Feeds the active owner's
/// guarded callbacks and hides again as soon as glass space returns, leaving
/// zero duplicate focus targets.
/// </summary>
internal sealed class InlineGlassFallback : HorizontalStackLayout
{
    private const string EllipsisIcon = "M5 12A1 1 0 1 0 5.01 12 M12 12A1 1 0 1 0 12.01 12 M19 12A1 1 0 1 0 19.01 12";

    private readonly IconButton _search = new()
    {
        Kind = IconButtonKind.Plain,
        Icon = Icons.Search,
        MinimumWidthRequest = 44,
        MinimumHeightRequest = 44,
        IsVisible = false,
    };

    private readonly IconButton _overflow = new()
    {
        Kind = IconButtonKind.Plain,
        Icon = EllipsisIcon,
        MinimumWidthRequest = 44,
        MinimumHeightRequest = 44,
        IsVisible = false,
    };

#if IOS
    private Action? _searchAction;
    private IReadOnlyList<GlassAction> _overflowActions = [];
    private WeakReference<ContentPage>? _page;
#endif

    public InlineGlassFallback()
    {
        Spacing = 4;
        VerticalOptions = LayoutOptions.Center;
        HorizontalOptions = LayoutOptions.End;
        IsVisible = false;
        SemanticProperties.SetDescription(_overflow, "More actions");
#if IOS
        _search.Command = new Command(() => _searchAction?.Invoke());
        _overflow.Command = new Command(async () => await OpenOverflowAsync());
#endif
        Children.Add(_search);
        Children.Add(_overflow);
    }

    /// <summary>Synchronises inline Search and Overflow visibility with the active owner's glass lane state.</summary>
    public bool Sync(ContentPage page)
    {
#if IOS
        var state = TabBarButtons.GetFallback(page);
        if (!state.Active)
        {
            _searchAction = null;
            _overflowActions = [];
            _page = null;
            _search.IsVisible = false;
            _overflow.IsVisible = false;
            IsVisible = false;
            return false;
        }

        _page = new WeakReference<ContentPage>(page);
        _searchAction = state.Search;
        _overflowActions = state.Overflow;
        _search.IsVisible = state.Search is not null;
        if (!string.IsNullOrWhiteSpace(state.SearchLabel))
        {
            SemanticProperties.SetDescription(_search, state.SearchLabel);
        }

        _overflow.IsVisible = state.Overflow.Count > 0;
        IsVisible = _search.IsVisible || _overflow.IsVisible;
        return true;
#else
        IsVisible = false;
        return false;
#endif
    }

#if IOS
    private async Task OpenOverflowAsync()
    {
        if (_page is null || !_page.TryGetTarget(out var page))
        {
            return;
        }

        // Resolve live so labels and IsEnabled reflect the latest TabBarButtons.Show call.
        var live = TabBarButtons.GetFallback(page);
        var enabled = (live.Active ? live.Overflow : _overflowActions).Where(a => a.IsEnabled).ToList();
        if (enabled.Count == 0)
        {
            return;
        }

        var choice = await page.DisplayActionSheetAsync("Actions", "Cancel", null, enabled.Select(a => a.Label).ToArray());
        var selected = enabled.FirstOrDefault(a => a.Label == choice);
        selected?.Tapped();
    }
#endif
}
