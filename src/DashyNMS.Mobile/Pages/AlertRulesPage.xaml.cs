using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Rules;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Pages;

/// <summary>LibreNMS's alert rules, read-only (#22): from More, or pinned to the tab bar.</summary>
public partial class AlertRulesPage : ContentPage, IDetailHost, ISearchHost
{
	private readonly AlertRulesViewModel _viewModel;
    private IDisposable? _edges;
    private int? _detailId;
    private AlertRuleView? _detail;

	public AlertRulesPage(AlertRulesViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading rules…", "Refreshing rules…");
		SearchReveal.Attach(List, SearchSlot, Search);
        SearchButton.Command = new Command(OpenSearch);
        List.Scrolled += (_, _) => { if (Search.IsFocused) KeyboardDismiss.Close(Search); };
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
		_viewModel.RefreshCommand.Execute(null);
	}

    public void OpenSearch() => SearchReveal.Reveal(Search);
    private void OnSearchCompleted(object? sender, EventArgs e) => KeyboardDismiss.Close(Search);

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        KeyboardDismiss.Close(Search);
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
        var outside = Math.Max(0, Width - Body.Width);
        Body.Padding = new Thickness(16 + clear.Left, 0, 16 + Math.Max(0, clear.Right - outside), 0);
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
        ListCard.Margin = new Thickness(0, 0, 0, foot);
        ListFoot.HeightRequest = spacer;
        // The host supplies only the insets reaching the detail column. The view owns its content padding.
        _detail?.PlaceContent(new Thickness(0, 0, clear.Right, clear.Bottom));
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
