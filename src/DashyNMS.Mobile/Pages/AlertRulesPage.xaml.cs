using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Rules;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Pages;

/// <summary>LibreNMS's alert rules, read-only (#22): from More, or pinned to the tab bar.</summary>
public partial class AlertRulesPage : ContentPage, IDetailHost, ISearchHost, IActivePage
{
	private readonly AlertRulesViewModel _viewModel;
    private IDisposable? _edges;
    private bool _active;
    private int? _detailId;
    private AlertRuleView? _detail;
    private readonly ButtonSearch _search;

	public AlertRulesPage(AlertRulesViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading rules…", "Refreshing rules…");
        _search = new ButtonSearch(SearchSlot, SearchField, Search, List);
        SearchButton.Command = new Command(OpenSearch);
        ChipScroll.SizeChanged += (_, _) => PlaceControls();
        SearchSlot.SizeChanged += (_, _) => PlaceControls();
        Body.SizeChanged += (_, _) => PlaceControls();
        Split.SplitChanged += (_, _) => { _viewModel.Select(Split.IsSplit ? _detailId : null); PlaceControls(); };
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        Header.BackInGlass = TabBarButtons.IsAvailable;
        SearchButton.IsVisible = !TabBarButtons.IsAvailable;
        List.HandlerChanged += (_, _) => StopInsets();
        List.Loaded += (_, _) => StopInsets();
#endif
	}

	/// <summary>A rule tapped with room beside the list (#88, #122) opens there.</summary>
	public bool TryShowDetail(string route, IDictionary<string, object>? parameters)
	{
		if (!Split.IsSplit || route != Routes.AlertRule || Handler?.MauiContext?.Services is not { } services)
		{
			return false;
		}

		var view = new AlertRuleView(services.GetRequiredService<AlertRuleViewModel>());
		view.Bar.ShowsBack = false;
		view.Load(parameters);
		Split.Detail = _detail = view;
        _detailId = view.ViewModel.RuleId;
        _viewModel.Select(_detailId);
        PlaceControls();
		return true;
	}

	// Every time: what's alerting changes while you're away.
	protected override void OnAppearing()
	{
		base.OnAppearing();
        Activate();
		_viewModel.RefreshCommand.Execute(null);
	}

    /// <summary>Restores retained-stack glass and geometry without refreshing or replacing selection.</summary>
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
        TabBarButtons.Show(this, OpenSearch, "Search rules", actions);
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
        // Watcher and glass callbacks can already be queued when a child takes ownership.
        if (!_active || !ActivePage.IsCurrent(this)) return;

        var clear = CoveredEdges.ForScrolling(this);
        var outside = Math.Max(0, Width - Body.Width);
        var left = 16 + clear.Left;
        var right = 16 + Math.Max(0, clear.Right - outside);
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
#else
        ListCard.Margin = new Thickness(left, 0, right, 12 + clear.Bottom);
        ListFoot.HeightRequest = 0;
#endif
        // ListDetail's trailing star column reaches the page's right edge. Pass its
        // clearance directly; native frames can still be stale during SizeChanged.
        if (Split.IsSplit) _detail?.PlaceContent(new Thickness(0, 0, clear.Right, clear.Bottom));
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
