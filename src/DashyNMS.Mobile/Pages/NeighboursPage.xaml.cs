using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
using DashyNMS.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Pages;

/// <summary>Network-wide links, either pinned or pushed from More.</summary>
public partial class NeighboursPage : ContentPage, IDetailHost, ISearchHost, IActivePage
{
    private readonly NeighboursViewModel _viewModel;
    private readonly ButtonSearch _search;
    private IDisposable? _edges;
    private bool _active;
    private NeighbourLinkView? _detail;
#if IOS
    private PageActionPlace? _actionPlace;
#endif

    public NeighboursPage(NeighboursViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading neighbours…", "Refreshing neighbours…");
        _search = new ButtonSearch(SearchSlot, SearchField, Search, List);
        SearchButton.Command = new Command(OpenSearch);
        Body.SizeChanged += (_, _) => PlaceControls();
        ChipScroll.SizeChanged += (_, _) => PlaceControls();
        SearchSlot.SizeChanged += (_, _) => PlaceControls();
        Split.SplitChanged += (_, _) => PlaceControls();
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        Header.BackInGlass = TabBarButtons.IsAvailable;
        SearchButton.IsVisible = !TabBarButtons.IsAvailable;
        if (TabBarButtons.IsAvailable) Header.Actions = null;
        List.HandlerChanged += (_, _) => StopInsets();
        List.Loaded += (_, _) => StopInsets();
#endif
        viewModel.RefreshCommand.Execute(null);
    }

    public bool TryShowDetail(string route, IDictionary<string, object>? parameters)
    {
        if (!Split.IsSplit || route != Routes.NeighbourLink || Handler?.MauiContext?.Services is not { } services)
            return false;
        var view = new NeighbourLinkView(services.GetRequiredService<NeighbourLinkViewModel>());
        view.Bar.ShowsBack = false;
        view.Load(parameters);
        Split.Detail = _detail = view;
        PlaceControls();
        return true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
        // Preserve constructor-only fetching and the original saved-group return policy.
        _viewModel.GroupsChanged();
    }

    /// <summary>Restores controls without changing data, query, filters or the retained detail.</summary>
    public void Activate()
    {
        _active = true;
        Header.Apply(this);
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
#if IOS
        _actionPlace = null;
        SyncActions();
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
#endif
        PlaceControls();
    }

#if IOS
    private void SyncActions()
    {
        if (!_active || !TabBarButtons.IsAvailable) return;
        var place = Header.ResolveActionPlacement(this, 1);
        if (_actionPlace == place) return;
        _actionPlace = place;
        List<GlassAction> bar = [];
        if (PageHeader.IsPushed(this))
            bar.Add(new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => _ = Shell.Current.GoToAsync(".."), IsBack: true));
        var groups = new GlassAction("square.grid.2x2", "Neighbourhoods", () => _viewModel.EditGroupsCommand.Execute(null));
        if (place == PageActionPlace.Header)
        {
            Header.SetHeaderGlass([groups]);
        }
        else
        {
            Header.SetHeaderGlass([]);
            bar.Add(groups);
        }
        TabBarButtons.Show(this, OpenSearch, "Search neighbours", bar);
    }
#endif

    public void OpenSearch() => _search.Open();
    private void OnSearchCompleted(object? sender, EventArgs e) => _search.Close();

    protected override void OnDisappearing()
    {
        _active = false;
        _search.Close();
        _edges?.Dispose();
        _edges = null;
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
        base.OnDisappearing();
    }

    private void PlaceControls()
    {
        if (!_active || !ActivePage.IsCurrent(this)) return;
        var clear = CoveredEdges.ForScrolling(this);
        var left = 16 + clear.Left;
        var right = 16 + Math.Max(0, clear.Right - Math.Max(0, Width - Body.Width));
        Header.Margin = new Thickness(left, 0, right, 0);
        SearchField.Margin = new Thickness(left, 2, right, 0);
#if IOS
        ChipStripPlacement.Apply(this, ChipScroll, Body, topMargin: 10);
#else
        ChipScroll.Padding = new Thickness(left, 0, right, 0);
#endif
        CountLine.Margin = new Thickness(left + 4, 6, right, 4);
        ErrorCard.Margin = new Thickness(left, 0, right, 8);
        Busy.Margin = new Thickness(left, 0, right, 0);
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
        ListCard.Margin = new Thickness(left, 0, right, 0);
        ListFoot.HeightRequest = SectionListPlacement.Resolve(obstruction).Footer;
        BottomFade.SetCoveredHeight(!sideBar ? clear.Bottom : 0);
        SyncActions();
#else
        ListCard.Margin = new Thickness(left, 0, right, 12 + clear.Bottom);
        ListFoot.HeightRequest = 0;
#endif
        // ListDetail's trailing star pane reaches the right and bottom page edges.
        if (Split.IsSplit) _detail?.PlaceContent(new Thickness(0, 0, clear.Right, clear.Bottom));
    }

#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);
    private void StopInsets()
    {
        static UIKit.UIScrollView? Find(UIKit.UIView view) =>
            view as UIKit.UIScrollView ?? view.Subviews.Select(Find).FirstOrDefault(found => found is not null);
        if (List.Handler?.PlatformView is UIKit.UIView view && Find(view) is { } scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }
#endif
}
