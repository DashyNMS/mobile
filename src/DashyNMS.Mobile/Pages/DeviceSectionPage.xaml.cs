using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.DeviceSection"/>, with the device id, section and name.</summary>
/// <remarks>
/// On iOS the page keeps clear of the status bar only and places every other
/// edge itself (<see cref="CoveredEdges"/>), as the device's own page does:
/// with the whole safe area on each side, iPhone Duo's landscape screen left
/// the rows a narrow column in its middle (#344). On iOS 26 and later Back is
/// glass by the tab bar, so the title takes the top line; before that, and on
/// Android, "‹ Back" stays above it.
/// </remarks>
public partial class DeviceSectionPage : ContentPage, IQueryAttributable
{
	/// <summary>The narrowest a row goes before the list drops a column.</summary>
	private const double MinRowWidth = 340;

	/// <summary>The most rows across, however wide the screen.</summary>
	private const int MaxColumns = 3;

	private readonly DeviceSectionViewModel _viewModel;

	public DeviceSectionPage(DeviceSectionViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
#if IOS
		// Keep clear of the status bar only; the other edges are placed in PlaceControls.
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
		Header.BackInGlass = TabBarButtons.IsAvailable;

		// The footer keeps the last row clear of the tab bar; UIKit's own
		// inset for the safe area would add the bar's height again.
		List.HandlerChanged += (_, _) => StopInsetAdjustment();
		List.Loaded += (_, _) => StopInsetAdjustment();
#endif
		CoveredEdges.Watch(this, PlaceControls);

		// An inventory's nesting reads down one column, so it never spreads across.
		_viewModel.Groups.CollectionChanged += (_, _) => Dispatcher.Dispatch(PlaceControls);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.DeviceIdParameter, out var id) && id is int deviceId
			&& query.TryGetValue(Routes.SectionParameter, out var value) && value is DeviceSection section)
		{
			query.TryGetValue(Routes.DeviceNameParameter, out var name);
			_ = _viewModel.LoadAsync(deviceId, section, name as string);
		}
	}

	/// <summary>The app's own header, as every page (#142), and Back in the glass where there is glass.</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
#if IOS
		if (TabBarButtons.IsAvailable)
		{
			// Nothing to search from the glass: the section's own box is on the page.
			TabBarButtons.Show(this, null, string.Empty,
			[
				new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => _ = Shell.Current.GoToAsync("..")),
			]);
		}

		TabBarButtons.Moved += OnTabBarButtonsMoved;
#endif
		PlaceControls();
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
	/// Keeps the header, search and rows clear of the camera and a side tab
	/// bar's column, the rows a column or more across as the width allows,
	/// and the last row able to scroll clear of a bottom tab bar and its glass.
	/// </summary>
	private void PlaceControls()
	{
		if (Width <= 0)
		{
			return;
		}

		var clear = CoveredEdges.ForScrolling(this);
		var left = 16 + clear.Left;
		var right = 16 + clear.Right;

		Header.Margin = new Thickness(left, 0, right, 0);
		Search.Margin = new Thickness(left, 0, right, 0);
		PortOptions.Margin = new Thickness(left, 4, right, 0);
		ErrorLine.Margin = new Thickness(left, 8, right, 0);
		Busy.Margin = new Thickness(left, 8, right, 0);
		var list = new Thickness(left, 0, right, 0);
		if (List.Margin != list)
		{
			List.Margin = list;
		}

		var foot = 24 + clear.Bottom;
		if (ListFoot.HeightRequest != foot)
		{
			ListFoot.HeightRequest = foot;
		}

		var span = 1;
		var width = Width - left - right;
		if (width > 0 && !_viewModel.Groups.Any(g => g.Any(row => row.Depth > 0)))
		{
			span = Math.Clamp((int)((width + ListLayout.HorizontalItemSpacing) / (MinRowWidth + ListLayout.HorizontalItemSpacing)), 1, MaxColumns);
		}

		if (ListLayout.Span != span)
		{
			ListLayout.Span = span;
		}
	}

#if IOS
	private void OnTabBarButtonsMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

	private void StopInsetAdjustment()
	{
		if (List.Handler?.PlatformView is UIKit.UIView view && FindScroll(view) is { } scroll)
		{
			scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
		}
	}

	private static UIKit.UIScrollView? FindScroll(UIKit.UIView view) =>
		view as UIKit.UIScrollView ?? view.Subviews.Select(FindScroll).FirstOrDefault(found => found is not null);
#endif
}
