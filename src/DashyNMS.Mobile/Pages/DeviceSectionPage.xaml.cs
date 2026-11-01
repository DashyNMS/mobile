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
/// Android, "‹ Back" stays above it. Search is the glass magnifier there too,
/// sliding its field down from beneath the title (#349); without glass, the
/// header's icon opens it.
/// </remarks>
public partial class DeviceSectionPage : ContentPage, IQueryAttributable, ISearchHost
{
	/// <summary>How long the search field takes to slide in or out, as Devices' and Alerts'.</summary>
	private const uint SearchSlide = 220;

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

		_viewModel.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(DeviceSectionViewModel.IsSearchOpen))
			{
				Dispatcher.Dispatch(() => SlideSearch(_viewModel.IsSearchOpen));
			}
		};

		// Done with the keyboard - put away, or the list scrolled: done searching.
		Search.Unfocused += (_, _) => _viewModel.DismissSearch();
		List.Scrolled += (_, _) =>
		{
			if (Search.IsFocused)
			{
				KeyboardDismiss.Close(Search);
			}
		};
#if IOS
		// Nothing riding on the keyboard: tapping off the field or pressing
		// Search already closes it.
		Search.HandlerChanged += (_, _) =>
		{
			if (Search.Handler?.PlatformView is UIKit.UITextField field)
			{
				field.InputAccessoryView = new UIKit.UIView(CoreGraphics.CGRect.Empty);
				field.InputAssistantItem.LeadingBarButtonGroups = [];
				field.InputAssistantItem.TrailingBarButtonGroups = [];
			}
		};

		// Search is the glass magnifier instead.
		if (TabBarButtons.IsAvailable)
		{
			HeaderSearch.IsVisible = false;
		}
#endif
	}

	/// <summary>
	/// The glass search button and ⌘F: opens search, or puts the cursor back
	/// in the field if it's open.
	/// </summary>
	public void OpenSearch()
	{
		if (_viewModel.IsSearchOpen)
		{
			Search.Focus();
		}
		else
		{
			_viewModel.IsSearchOpen = true;
		}
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
			TabBarButtons.Show(this, OpenSearch, _viewModel.SearchLabel,
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
		KeyboardDismiss.Close(Search);
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
		var field = new Thickness(left, 4, right, 8);
		if (SearchField.Margin != field)
		{
			SearchField.Margin = field;
		}

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

	// The keyboard's search key: the list is narrowed, so the field goes.
	private void OnSearchCompleted(object? sender, EventArgs e) => _viewModel.DismissSearch();

	/// <summary>
	/// Slides the search field down from beneath the title, the list easing
	/// down to make room, and into typing - or back up out of sight. At once
	/// with Reduce Motion.
	/// </summary>
	private void SlideSearch(bool open)
	{
		const string name = "SearchSlide";
		var full = SearchField.HeightRequest + SearchField.Margin.VerticalThickness;
		SearchSlot.AbortAnimation(name);
		if (open)
		{
			// Out of sight before it shows, or it flashes in place first.
			if (!SearchField.IsVisible && !ReducedMotion.IsOn)
			{
				SearchField.TranslationY = -full;
				SearchField.Opacity = 0;
			}

			SearchField.IsVisible = true;
			Dispatcher.Dispatch(() => Search.Focus());
		}
		else
		{
			KeyboardDismiss.Close(Search);
		}

		// Done: hidden once out of sight, unless opened again meanwhile.
		void Finish()
		{
			var stillOpen = _viewModel.IsSearchOpen;
			SearchField.IsVisible = stillOpen;
			SearchSlot.HeightRequest = stillOpen ? full : 0;
			SearchField.TranslationY = 0;
			SearchField.Opacity = 1;
		}

		if (ReducedMotion.IsOn || (!open && !SearchField.IsVisible))
		{
			Finish();
			return;
		}

		// From wherever it is now, so a quick open-close doesn't jump.
		var fromHeight = Math.Max(0, SearchSlot.HeightRequest);
		var fromY = SearchField.TranslationY;
		var fromOpacity = SearchField.Opacity;
		var toHeight = open ? full : 0;
		var toY = open ? 0 : -full;
		var toOpacity = open ? 1.0 : 0;
		SearchSlot.Animate(
			name,
			v =>
			{
				SearchSlot.HeightRequest = fromHeight + ((toHeight - fromHeight) * v);
				SearchField.TranslationY = fromY + ((toY - fromY) * v);
				SearchField.Opacity = fromOpacity + ((toOpacity - fromOpacity) * v);
			},
			length: SearchSlide,
			easing: open ? Easing.CubicOut : Easing.CubicIn,
			finished: (_, cancelled) =>
			{
				if (!cancelled)
				{
					Finish();
				}
			});
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
