using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// The dashboard: the server's cards, in the order they're customised.
/// </summary>
/// <remarks>
/// On iOS the cards scroll on down under the Liquid Glass tab bar and the
/// home indicator to the foot of the screen, as on the Network map. They
/// never go under the glass at the side: with a side tab bar (iPhone Duo)
/// the cards' column stops short of its column, at rest and while
/// scrolling. Edit dashboard and Refresh are glass by the tab bar on iOS 26
/// and later; before that, and on Android, Edit dashboard stays on the top
/// line (see <see cref="CoveredEdges"/>).
/// </remarks>
public partial class DashboardPage : ContentPage, IActivePage
{
	/// <summary>The gap kept between the cards and a side tab bar's glass.</summary>
	private const double GlassGap = 12;

	private readonly DashboardViewModel _viewModel;
	private readonly InlineGlassFallback _fallback = new();
#if IOS
	private readonly HeaderGlassGroup _headerGlass = new() { IsVisible = false };
	private PageActionPlace? _actionPlace;
#endif

	public DashboardPage(DashboardViewModel viewModel)
	{
		InitializeComponent();
#if IOS
		// Keep clear of the status bar only; the other edges are placed in PlaceControls.
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
		Scroller.HandlerChanged += (_, _) => StopInsets();
		Scroller.Loaded += (_, _) => StopInsets();
#endif
		BindingContext = _viewModel = viewModel;

		// A pull starts the refresh, with no spinner: the logo's heartbeat shows the load.
		Controls.PullToRefresh.HideSpinner(Refresh);

		// Beside the status bar where there's room, as every page's line (#278).
		Controls.TopBar.Fit(TopLine);
		TopLine.SetColumn(_fallback, 2);
		TopLine.Add(_fallback);
#if IOS
		TopLine.SetColumn(_headerGlass, 2);
		TopLine.Add(_headerGlass);
#endif
		Toast.SizeChanged += (_, _) => PlaceControls();
		Toast.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsVisible)) PlaceControls(); };

		CoveredEdges.Watch(this, PlaceControls);
#if IOS
		// Edit dashboard is Liquid Glass by the tab bar instead.
		if (TabBarButtons.IsAvailable)
		{
			CustomiseButton.IsVisible = false;
		}
#endif
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Activate();

		// Also picks up Edit dashboard's changes, and the phone's theme for the graph.
		_viewModel.DarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
		_viewModel.RefreshCommand.Execute(null);

		// A card just added lands at the bottom: brought into view, outlined (#140).
		Dispatcher.Dispatch(() => _ = ShowHighlightedAsync());
	}

	public void Activate()
	{
#if IOS
		_actionPlace = null;
		SyncActions();
		TabBarButtons.Moved -= OnTabBarButtonsMoved;
		TabBarButtons.Moved += OnTabBarButtonsMoved;
#endif
		PlaceControls();
	}

#if IOS
	private void SyncActions()
	{
		if (!TabBarButtons.IsAvailable) return;
		var actions = new GlassAction[]
		{
			new("slider.horizontal.3", "Edit dashboard", () => _viewModel.CustomiseCommand.Execute(null)),
			new("arrow.clockwise", "Refresh the dashboard", () => _viewModel.RefreshCommand.Execute(null)),
		};
		var place = PageActionPlacement.Resolve(TabBarButtons.IsAvailable, Width, Height);
		if (_actionPlace == place) return;
		_actionPlace = place;
		if (place == PageActionPlace.HeaderGlass)
		{
			_headerGlass.Actions = actions;
			_headerGlass.IsVisible = true;
			TabBarButtons.Show(this, null, string.Empty, []);
		}
		else
		{
			_headerGlass.Actions = [];
			_headerGlass.IsVisible = false;
			TabBarButtons.Show(this, null, string.Empty, actions);
		}
	}
#endif

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
#if IOS
		TabBarButtons.Moved -= OnTabBarButtonsMoved;
		TabBarButtons.Hide(this);
#endif
	}

	/// <summary>
	/// Keeps the top line, the cards and the toast clear of the camera and the
	/// tab bar while the cards scroll on underneath the bottom one. The
	/// cards' column is padded rather than the scroll view, so a card can
	/// scroll under a bottom tab bar but never sideways under a side one; its
	/// foot padding lets the last card and the time it was updated scroll clear.
	/// </summary>
	private void PlaceControls()
	{
#if IOS
		SyncActions();
		_fallback.Sync(this);
		StopInsets();
#endif
		var edges = CoveredEdges.Of(this);
		double left = edges.Left, right = edges.Right, bottom = edges.Bottom;
		double? toastFoot = null;
		var obstruction = 12 + bottom;
#if IOS
		// Above a bottom tab bar, the glass buttons float over its right end.
		bottom += TabBarButtons.CoveredBottom;
		obstruction = 12 + bottom;
		if (Handler?.PlatformView is UIKit.UIView page && page.Bounds.Height > 0)
		{
			var nativePill = TabBarButtons.PillIn(page);
			OccupiedArea? pill = nativePill is { } bar ? new(bar.Left, bar.Top, bar.Right, bar.Bottom) : null;
			var glass = TabBarButtons.Occupied(page)
				.Select(r => new OccupiedArea(r.Left, r.Top, r.Right, r.Bottom)).ToArray();
			obstruction = SetupActionPlacement.BottomGap(page.Bounds.Height,
				edges.Bottom + (pill is null ? TabBarButtons.CoveredBottom : 0), pill, glass);
			if (nativePill is { } side && side.Height > side.Width)
			{
				// A side tab bar: wherever UIKit's safe area ends, the cards stop a
				// gap short of the pill's column, and the toast's foot is level with
				// the pill's, as one row of glass and toast.
				if (side.Left + (side.Width / 2) > page.Bounds.Width / 2)
				{
					right = Math.Max(right, page.Bounds.Width - side.Left + GlassGap - 16);
				}
				else
				{
					left = Math.Max(left, side.Right + GlassGap - 16);
				}

				toastFoot = Math.Max(0, page.Bounds.Height - side.Bottom);
			}
		}
#endif
		TopLine.Margin = new Thickness(left, 0, right, 0);
		CardColumn.Padding = new Thickness(16 + left, 8, 16 + right, 0);
		var toastMarginBottom = toastFoot ?? obstruction;
		Toast.Margin = new Thickness(12 + left, 0, 12 + right, toastMarginBottom);
		var footer = SectionListPlacement.FooterGap + obstruction;
		if (Toast.IsVisible)
		{
			footer = Math.Max(footer, toastMarginBottom + Math.Max(52, Toast.Height) + SectionListPlacement.PanelGap);
		}
		if (Foot.HeightRequest != footer)
		{
			Foot.HeightRequest = footer;
		}
#if IOS
		var sideBar = Handler?.PlatformView is UIKit.UIView pv && TabBarButtons.PillIn(pv) is { } p && p.Height > p.Width;
		BottomFade.SetCoveredHeight(!sideBar ? bottom : 0);
#endif
	}

#if IOS
	private void OnTabBarButtonsMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

	private void StopInsets()
	{
		if (Scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
			scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
	}
#endif

	private async Task ShowHighlightedAsync()
	{
		if (CardList.Children.OfType<View>().FirstOrDefault(v => v.BindingContext is DashboardCard { IsHighlighted: true }) is { } card)
		{
			await Scroller.ScrollToAsync(card, ScrollToPosition.Center, animated: true);
		}
	}
}
