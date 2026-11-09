using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.GroupsLocations"/>, pinned or pushed from More.</summary>
public partial class GroupsLocationsPage : ContentPage, ISearchHost, IActivePage
{
	private readonly GroupsLocationsViewModel _viewModel;
	private bool _loaded;
	private IDisposable? _edges;
    private readonly ButtonSearch _search;

    /// <summary>The glass Refresh button's refresh: shows the busy card, as a pull does.</summary>
    private readonly Action _refresh;

	public GroupsLocationsPage(GroupsLocationsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
        _refresh = PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel,
            "Loading groups and locations…", "Refreshing groups and locations…");
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
		if (!_loaded)
		{
			_loaded = true;
			_viewModel.RefreshCommand.Execute(null);
		}
	}

    public void Activate()
    {
		Header.Apply(this);
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
#if IOS
        List<GlassAction> actions = [];
        if (PageHeader.IsPushed(this))
            actions.Add(new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => _ = Shell.Current.GoToAsync(".."), IsBack: true));
        actions.Add(new("arrow.clockwise", "Refresh groups and locations", _refresh));
        TabBarButtons.Show(this, OpenSearch, "Search groups and locations", actions);
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
#endif
        PlaceControls();
	}

    public void OpenSearch() => _search.Open();

    private void OnSearchCompleted(object? sender, EventArgs e) => _search.Close();

    protected override void OnDisappearing()
    {
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
        var clear = CoveredEdges.ForScrolling(this);
        Body.Padding = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
        ListFoot.HeightRequest = 12 + clear.Bottom;
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
