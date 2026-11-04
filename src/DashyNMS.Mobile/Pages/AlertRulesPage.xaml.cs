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
        view.SizeChanged += (_, _) => { if (ReferenceEquals(_detail, view)) PlaceControls(); };
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
        if (!_active || Shell.Current?.CurrentPage != this) return;

        var clear = CoveredEdges.ForScrolling(this);
        var outside = Math.Max(0, Width - Body.Width);
        var left = 16 + clear.Left;
        var right = 16 + Math.Max(0, clear.Right - outside);
        Header.Margin = new Thickness(left, 0, right, 0);
        SearchField.Margin = new Thickness(left, 2, right, 0);
        // Keep the native clip full-width; only its scrolling content owns safe gutters,
        // as in Devices/Alerts. Offscreen chips may travel beneath the camera.
        ChipScroll.Padding = new Thickness(left, 0, right, 0);
        CountLine.Margin = new Thickness(left + 4, 6, right, 4);
        ErrorCard.Margin = new Thickness(left, 0, right, 8);
        Busy.Margin = new Thickness(left, 0, right, 0);
        var foot = 12 + clear.Bottom;
        var spacer = 0.0;
#if IOS
        if (Handler?.PlatformView is UIKit.UIView page && TabBarButtons.PillIn(page) is { } pill
            && (pill.Height > pill.Width || pill.Top > page.Bounds.Height / 2))
        {
            foot = Math.Max(0, page.Bounds.Height - pill.Bottom);
            if (pill.Height <= pill.Width && clear.Bottom > foot)
                spacer = clear.Bottom - foot + 8;
        }
#endif
        ListCard.Margin = new Thickness(left, 0, right, foot);
        ListFoot.HeightRequest = spacer;
        // The host supplies only the insets reaching the detail column. The view owns its content padding.
        var detailRight = clear.Right;
#if IOS
        if (Handler?.PlatformView is UIKit.UIView host && _detail?.Handler?.PlatformView is UIKit.UIView detail)
        {
            var bounds = detail.ConvertRectToView(detail.Bounds, host);
            detailRight = Math.Max(0, clear.Right - Math.Max(0, host.Bounds.Width - bounds.Right));
        }
#endif
        if (Split.IsSplit) _detail?.PlaceContent(new Thickness(0, 0, detailRight, clear.Bottom));
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
