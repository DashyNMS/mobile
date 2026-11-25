using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

public partial class HealthPage : ContentPage, ISearchHost, IActivePage
{
    private readonly HealthViewModel _viewModel;
    private IDisposable? _edges;
    private bool _active;
    private readonly ButtonSearch _search;

    /// <summary>The glass Refresh button's refresh: shows the busy card, as a pull does.</summary>
    private readonly Action _refresh;

    public HealthPage(HealthViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _refresh = PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading sensors…", "Refreshing sensors…");
        _search = new ButtonSearch(SearchSlot, SearchField, Search, List);
        SearchButton.Command = new Command(OpenSearch);
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        Header.BackInGlass = TabBarButtons.IsAvailable;
        SearchButton.IsVisible = !TabBarButtons.IsAvailable;
        List.HandlerChanged += (_, _) => StopInsets();
        List.Loaded += (_, _) => StopInsets();
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
        _ = _viewModel.RefreshIfStaleAsync();
    }

    /// <summary>Reclaims retained-page controls without reloading readings or resetting filters.</summary>
    public void Activate()
    {
        _active = true;
        Header.Apply(this);
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
#if IOS
        List<GlassAction> actions = [];
        if (PageHeader.IsPushed(this))
            actions.Add(new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => _ = Shell.Current.GoToAsync(".."), IsBack: true));
        actions.Add(new("arrow.clockwise", "Refresh sensors", _refresh));
        TabBarButtons.Show(this, OpenSearch, "Search sensors", actions);
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
#endif
        PlaceControls();
    }

    public void OpenSearch() => _search.Open();

    private void OnSearchCompleted(object? sender, EventArgs e) => _search.Close();

    protected override void OnDisappearing()
    {
        _active = false;
        base.OnDisappearing();
        _search.Close();
        _edges?.Dispose();
        _edges = null;
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
    }

    private void PlaceControls()
    {
        // Native safe-area and glass callbacks may already be queued on departure.
        if (!_active || !ActivePage.IsCurrent(this)) return;

        var clear = CoveredEdges.ForScrolling(this);
        Body.Padding = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
#if IOS
        var sideBar = false;
        var obstruction = 12 + clear.Bottom;
        if (Handler?.PlatformView is UIKit.UIView page && page.Bounds.Height > 0)
        {
            var nativePill = TabBarButtons.PillIn(page);
            sideBar = nativePill is { } p && p.Height > p.Width;
            OccupiedArea? pill = nativePill is { } bar ? new(bar.Left, bar.Top, bar.Right, bar.Bottom) : null;
            var glass = TabBarButtons.Occupied(page)
                .Select(r => new OccupiedArea(r.Left, r.Top, r.Right, r.Bottom)).ToArray();
            obstruction = SetupActionPlacement.BottomGap(page.Bounds.Height,
                CoveredEdges.Of(this).Bottom + (pill is null ? TabBarButtons.CoveredBottom : 0), pill, glass);
        }
        ListCard.Margin = Thickness.Zero;
        ListFoot.HeightRequest = SectionListPlacement.Resolve(obstruction).Footer;
        BottomFade.SetCoveredHeight(!sideBar ? clear.Bottom : 0);
#else
        ListCard.Margin = new Thickness(0, 0, 0, 12 + clear.Bottom);
        ListFoot.HeightRequest = 0;
#endif
    }

#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

    private void StopInsets()
    {
        if (List.Handler?.PlatformView is UIKit.UIView view && FindScroll(view) is { } scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }

    private static UIKit.UIScrollView? FindScroll(UIKit.UIView view) =>
        view as UIKit.UIScrollView ?? view.Subviews.Select(FindScroll).FirstOrDefault(found => found is not null);
#endif
}
