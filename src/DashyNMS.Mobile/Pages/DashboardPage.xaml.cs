using DashyNMS.Mobile.Controls;
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
public partial class DashboardPage : ContentPage
{
	/// <summary>The gap kept between the cards and a side tab bar's glass.</summary>
	private const double GlassGap = 12;

	private readonly DashboardViewModel _viewModel;

	public DashboardPage(DashboardViewModel viewModel)
	{
		InitializeComponent();
#if IOS
		// Keep clear of the status bar only; the other edges are placed in PlaceControls.
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
#endif
		BindingContext = _viewModel = viewModel;

		// A pull starts the refresh, with no spinner: the logo's heartbeat shows the load.
		Controls.PullToRefresh.HideSpinner(Refresh);

		// Beside the status bar where there's room, as every page's line (#278).
		Controls.TopBar.Fit(TopLine);

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
#if IOS
		// Nothing to search: the actions alone.
		TabBarButtons.Show(this, null, string.Empty,
		[
			new GlassAction("slider.horizontal.3", "Edit dashboard", () => _viewModel.CustomiseCommand.Execute(null)),
			new GlassAction("arrow.clockwise", "Refresh the dashboard", () => _viewModel.RefreshCommand.Execute(null)),
		]);
		TabBarButtons.Moved += OnTabBarButtonsMoved;
#endif
		PlaceControls();

		// Also picks up Edit dashboard's changes, and the phone's theme for the graph.
		_viewModel.DarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
		_viewModel.RefreshCommand.Execute(null);

		// A card just added lands at the bottom: brought into view, outlined (#140).
		Dispatcher.Dispatch(() => _ = ShowHighlightedAsync());
	}

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
		var edges = CoveredEdges.Of(this);
		double left = edges.Left, right = edges.Right, bottom = edges.Bottom;
		double? toastFoot = null;
#if IOS
		// Above a bottom tab bar, the glass buttons float over its right end.
		bottom += TabBarButtons.CoveredBottom;
		if (Handler?.PlatformView is UIKit.UIView page && TabBarButtons.PillIn(page) is { } pill && pill.Height > pill.Width)
		{
			// A side tab bar: wherever UIKit's safe area ends, the cards stop a
			// gap short of the pill's column, and the toast's foot is level with
			// the pill's, as one row of glass and toast.
			if (pill.Left + (pill.Width / 2) > page.Bounds.Width / 2)
			{
				right = Math.Max(right, page.Bounds.Width - pill.Left + GlassGap - 16);
			}
			else
			{
				left = Math.Max(left, pill.Right + GlassGap - 16);
			}

			toastFoot = Math.Max(0, page.Bounds.Height - pill.Bottom);
		}
#endif
		TopLine.Margin = new Thickness(left, 0, right, 0);
		CardColumn.Padding = new Thickness(16 + left, 8, 16 + right, 16 + bottom);
		Toast.Margin = new Thickness(12 + left, 0, 12 + right, toastFoot ?? 16 + bottom);
	}

#if IOS
	private void OnTabBarButtonsMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);
#endif

	private async Task ShowHighlightedAsync()
	{
		if (CardList.Children.OfType<View>().FirstOrDefault(v => v.BindingContext is DashboardCard { IsHighlighted: true }) is { } card)
		{
			await Scroller.ScrollToAsync(card, ScrollToPosition.Center, animated: true);
		}
	}
}
