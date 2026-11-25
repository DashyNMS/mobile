using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.CustomiseDashboard"/>, from the Dashboard's pencil.</summary>
public partial class CustomiseDashboardPage : ContentPage, IActivePage
{
	private readonly CustomiseDashboardViewModel _viewModel;
#if IOS
	private readonly HeaderGlassGroup _headerGlass = new() { IsVisible = false };
	private PageActionPlace? _actionPlace;
#endif

	public CustomiseDashboardPage(CustomiseDashboardViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        if (TabBarButtons.IsAvailable) TopBar.IsVisible = false;
        Heading.SetColumn(_headerGlass, 1);
        Heading.Add(_headerGlass);
        List.HandlerChanged += (_, _) => StopInsetAdjustment();
        List.Loaded += (_, _) => StopInsetAdjustment();
#endif
        Controls.TopBar.Fit(Heading);
        CoveredEdges.Watch(this, PlaceControls);
        List.SizeChanged += (_, _) => PlaceColumns();
        Toast.SizeChanged += (_, _) => PlaceFooter();
        Toast.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsVisible)) PlaceFooter();
        };
	}

	/// <summary>Back from a card's set-up or the card picker: the cards may have changed (#87, #140).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Activate();
		_viewModel.Attach();
	}

	public void Activate()
	{
		TopBar.Apply(this);
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
        if (!TabBarButtons.IsAvailable) return;
        var place = PageActionPlacement.Resolve(TabBarButtons.IsAvailable, Width, Height);
        if (_actionPlace == place) return;
        _actionPlace = place;
        if (place == PageActionPlace.HeaderGlass)
        {
            _headerGlass.Actions = [new GlassAction("plus", "Add a card", () => _viewModel.AddCardCommand.Execute(null))];
            _headerGlass.IsVisible = true;
        }
        else
        {
            _headerGlass.Actions = [];
            _headerGlass.IsVisible = false;
        }
        TabBarButtons.Show(this, null, string.Empty,
        [
            new GlassAction("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => _ = Shell.Current.GoToAsync(".."), IsBack: true),
        ]);
    }
#endif

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.Detach();
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
	}

    /// <summary>Pad the list's footer so its final action scrolls clear without shortening the viewport.</summary>
    private void PlaceControls()
    {
#if IOS
        SyncActions();
        if (TabBarButtons.IsAvailable)
            TopBar.IsVisible = TabBarButtons.GetFallback(this).Active;
#endif
        var edges = CoveredEdges.ForScrolling(this);
        Heading.Margin = TopBar.Margin = new Thickness(16 + edges.Left, 0, 16 + edges.Right, 0);
        List.Margin = new Thickness(16 + edges.Left, 0, 16 + edges.Right, 0);
        double foot = 16 + edges.Bottom;
#if IOS
        if (Handler?.PlatformView is UIKit.UIView page && TabBarButtons.PillIn(page) is { } pill && pill.Height > pill.Width)
            foot = Math.Max(0, page.Bounds.Height - pill.Bottom);
#endif
        Toast.Margin = new Thickness(12 + edges.Left, 0, 12 + edges.Right, foot);
#if IOS
        var sideBar = Handler?.PlatformView is UIKit.UIView pv && TabBarButtons.PillIn(pv) is { } p && p.Height > p.Width;
        BottomFade.SetCoveredHeight(!sideBar ? edges.Bottom : 0);
#endif
        PlaceFooter();
        PlaceColumns();
    }

    /// <summary>Keep Add reachable above the toast until its exit animation finishes.</summary>
    private void PlaceFooter()
    {
        double bottom = 16 + CoveredEdges.ForScrolling(this).Bottom;
        if (Toast.IsVisible)
            bottom = Math.Max(bottom, Toast.Margin.Bottom + Math.Max(52, Toast.Height) + 12);
        Footer.Padding = new Thickness(0, 12, 0, bottom);
    }

    private void PlaceColumns()
    {
        if (List.Width <= 0) return;
        var columns = DashboardColumns.Count(List.Width);
        if (CardLayout.Span != columns) CardLayout.Span = columns;
    }

#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

    /// <summary>The footer supplies clearance; native insets would add it again.</summary>
    private void StopInsetAdjustment()
    {
        if (List.Handler?.PlatformView is UIKit.UIView view && FindScroll(view) is { } scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }

    private static UIKit.UIScrollView? FindScroll(UIKit.UIView view) =>
        view as UIKit.UIScrollView ?? view.Subviews.Select(FindScroll).FirstOrDefault(found => found is not null);
#endif

	private void OnReorderCompleted(object? sender, EventArgs e) => _viewModel.SaveOrderCommand.Execute(null);
}
