using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Pages;

/// <summary>The Devices tab; Groups &amp; locations comes back here with <see cref="Routes.GroupParameter"/> or <see cref="Routes.LocationParameter"/>.</summary>
/// <remarks>
/// On iOS the screen is one display, as on the Dashboard and the Network
/// map: the chips scroll in from under the camera, and the list runs on down
/// to the foot of the Liquid Glass tab bar - under a bottom one, its rows
/// scrolling clear; short of a side one's column, its foot level with the
/// pill's. Search, Sort &amp; filter and Refresh are glass by the tab bar on
/// iOS 26 and later - with the pin of a device showing beside the list -
/// and header icons before that and on Android. What can
/// be tapped is placed clear of the camera and glass here (see
/// <see cref="CoveredEdges"/>); beside a detail pane (#88) only the edges
/// that reach the list's own column count.
/// </remarks>
public partial class DevicesPage : ContentPage, IQueryAttributable, IDetailHost, ISearchHost
{
	/// <summary>The gap kept between the list and a side tab bar's glass.</summary>
	private const double GlassGap = 12;

	/// <summary>How long the search box takes to slide in or out.</summary>
	private const uint SearchSlide = 220;

	/// <summary>How long the cards at the foot take to slide, and the list to dim.</summary>
	private const uint CardMotion = 250;

	/// <summary>How far the list fades back behind the Sort &amp; filter card - the Network map dims to 0.3.</summary>
	private const double DimmedTo = 0.7;

	private readonly DevicesViewModel _viewModel;
	private readonly ShortcutReturn _shortcut = new();

	/// <summary>The device showing beside the list, if any - highlighted while the pane is there.</summary>
	private int? _shown;

	/// <summary>On screen, so a change of filters can swap the glass's Sort &amp; filter icon.</summary>
	private bool _showing;

	/// <summary>The device beside the list (#88), whose pin is glass with the list's actions.</summary>
	private DeviceDetailView? _detail;

	/// <summary>What the glass last showed: Sort &amp; filter filled, and the pane's pin (shown, filled).</summary>
	private (bool Filtered, bool Pin, bool Pinned)? _glass;

	public DevicesPage(DevicesViewModel viewModel)
	{
		InitializeComponent();
#if IOS
		// Keep clear of the status bar only; the other edges are placed in PlaceControls.
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
#endif
		BindingContext = _viewModel = viewModel;
		PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading devices…", "Refreshing devices…");
		Split.SplitChanged += (_, _) =>
		{
			_viewModel.Select(Split.IsSplit ? _shown : null);
			PlaceControls();
#if IOS
			UpdateGlass();
#endif
		};

		CoveredEdges.Watch(this, PlaceControls);

		// The list's column narrows as a detail pane comes, and the bulk bar
		// sets how far the last row must scroll to clear it.
		ListColumn.SizeChanged += (_, _) => PlaceControls();
		Bulk.SizeChanged += (_, _) => PlaceControls();

		_viewModel.PropertyChanged += (_, e) =>
		{
			switch (e.PropertyName)
			{
				case nameof(DevicesViewModel.IsSearchOpen):
					Dispatcher.Dispatch(() => SlideSearch(_viewModel.IsSearchOpen));
					break;
				case nameof(DevicesViewModel.IsFilterPanelOpen):
					Dispatcher.Dispatch(() =>
					{
						SlideCard(FilterCard, _viewModel.IsFilterPanelOpen);
						FadeDim(_viewModel.IsFilterPanelOpen);
					});
					break;
#if IOS
				case nameof(DevicesViewModel.HasPanelFilters):
					Dispatcher.Dispatch(UpdateGlass);
					break;
#endif
			}
		};
		_viewModel.Selection.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(BulkSelection.IsSelecting))
			{
				Dispatcher.Dispatch(() => SlideCard(Bulk, _viewModel.Selection.IsSelecting, PlaceControls));
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

		var tapOff = new TapGestureRecognizer();
		tapOff.Tapped += (_, _) => _viewModel.IsFilterPanelOpen = false;
		Dim.GestureRecognizers.Add(tapOff);

#if IOS
		// No close button riding on the keyboard: tapping off the box or
		// pressing Search already closes it. The empty accessory takes the
		// place of whatever would ride there, and the assistant's buttons are
		// the other way iOS puts them beside the keyboard.
		Search.HandlerChanged += (_, _) =>
		{
			if (Search.Handler?.PlatformView is UIKit.UITextField field)
			{
				field.InputAccessoryView = new UIKit.UIView(CoreGraphics.CGRect.Empty);
				field.InputAssistantItem.LeadingBarButtonGroups = [];
				field.InputAssistantItem.TrailingBarButtonGroups = [];
			}
		};

		// The list keeps its rows clear of the tab bar with its footer, sized
		// in PlaceControls; UIKit's own inset for the safe area would add the
		// bar's height again.
		List.HandlerChanged += (_, _) => StopInsetAdjustment();
		List.Loaded += (_, _) => StopInsetAdjustment();

		// Search, Sort & filter and Refresh are Liquid Glass by the tab bar instead.
		if (TabBarButtons.IsAvailable)
		{
			SearchButton.IsVisible = false;
			FilterButton.IsVisible = false;
		}
#endif
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		var group = query.TryGetValue(Routes.GroupParameter, out var g) ? g as string : null;
		var location = query.TryGetValue(Routes.LocationParameter, out var l) ? l as string : null;
		if (group is not null || location is not null)
		{
			_shortcut.Arrived();
			_viewModel.ShowOnly(group, location);
		}
		else if (query.TryGetValue(Routes.StateParameter, out var s) && s is DesktopNMS.Core.Models.DeviceState state)
		{
			_shortcut.Arrived();
			_viewModel.ShowOnlyState(state);
		}

		// Applied once: coming back to the tab later shouldn't reapply it.
		query.Clear();
	}

	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		_shortcut.Left(this, args);
	}

	protected override void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);
		if (_shortcut.OpenedAfresh())
		{
			_viewModel.ShowSavedFilter();
		}
	}

	/// <summary>
	/// A device tapped with room beside the list (#88) opens there, as does
	/// one it links to (a neighbour, say); its sections and graphs still open
	/// as pages of their own.
	/// </summary>
	public bool TryShowDetail(string route, IDictionary<string, object>? parameters)
	{
		if (!Split.IsSplit || route != Routes.DeviceDetail || Handler?.MauiContext?.Services is not { } services)
		{
			return false;
		}

		var view = new DeviceDetailView(services.GetRequiredService<DeviceDetailViewModel>());
		view.Bar.ShowsBack = false;
#if IOS
		// Its pin joins the list's glass by the tab bar, so the pane needs no top bar.
		if (TabBarButtons.IsAvailable)
		{
			view.UseGlass();
		}

		if (_detail is { } old)
		{
			old.ViewModel.PropertyChanged -= OnDetailChanged;
			old.ViewModel.TogglePinCommand.CanExecuteChanged -= OnDetailPinChanged;
		}

		view.ViewModel.PropertyChanged += OnDetailChanged;
		view.ViewModel.TogglePinCommand.CanExecuteChanged += OnDetailPinChanged;
#endif
		view.Load(parameters);
		Split.Detail = _detail = view;
		PlaceControls();
#if IOS
		UpdateGlass();
#endif
		_shown = parameters?.TryGetValue(Routes.DeviceIdParameter, out var id) == true ? id as int? : null;
		_viewModel.Select(_shown);
		return true;
	}

	/// <summary>
	/// The glass search button, the header's icon and ⌘F: opens search, or
	/// puts the cursor back in the box if it's open - closing is tapping off
	/// it, pressing Search or scrolling the list.
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

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
		_showing = true;
#if IOS
		_glass = null;
		UpdateGlass();
		TabBarButtons.Moved += OnTabBarButtonsMoved;
#endif

		// As they were left, without sliding in again.
		FilterCard.IsVisible = _viewModel.IsFilterPanelOpen;
		Dim.IsVisible = _viewModel.IsFilterPanelOpen;
		Dim.Opacity = _viewModel.IsFilterPanelOpen ? DimmedTo : 0;
		Bulk.IsVisible = _viewModel.Selection.IsSelecting;
		PlaceControls();

		// Every time, like the other tabs: after a sign-out and sign-in the
		// page is reused, and a once-only load would keep the old server's list.
		_viewModel.RefreshCommand.Execute(null);
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_showing = false;
		KeyboardDismiss.Close(Search);
#if IOS
		TabBarButtons.Moved -= OnTabBarButtonsMoved;
		TabBarButtons.Hide(this);
#endif
	}

	// The keyboard's search key: the list is narrowed, so the box goes.
	private void OnSearchCompleted(object? sender, EventArgs e) => _viewModel.DismissSearch();

	/// <summary>
	/// Slides the search box down from beneath the title, the chips and list
	/// easing down to make room, and into typing - or back up out of sight,
	/// closing the keyboard. Shown or hidden at once with Reduce Motion.
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

	/// <summary>
	/// Slides <paramref name="card"/> (Sort &amp; filter, or the bulk bar) up
	/// from the foot of the screen, or back down out of sight - then
	/// <paramref name="then"/>. At once with Reduce Motion.
	/// </summary>
	private void SlideCard(View card, bool show, Action? then = null)
	{
		const string name = "CardSlide";
		double Travel() => (card.Height > 0 ? card.Height : 240) + card.Margin.Bottom + 8;

		// Already up, or already gone: nothing to move.
		if (show == card.IsVisible && !card.AnimationIsRunning(name) && card.TranslationY == 0)
		{
			then?.Invoke();
			return;
		}

		card.AbortAnimation(name);
		if (ReducedMotion.IsOn || (!show && !card.IsVisible))
		{
			card.IsVisible = show;
			card.TranslationY = 0;
			card.Opacity = 1;
			then?.Invoke();
			return;
		}

		if (show && !card.IsVisible)
		{
			// Out of sight before it shows, or it flashes in place first.
			card.Opacity = 0;
			card.TranslationY = Travel();
			card.IsVisible = true;
		}

		// Once laid out, so it knows how far it has to come.
		Dispatcher.Dispatch(() =>
		{
			var fromY = show && card.Opacity == 0 ? Travel() : card.TranslationY;
			var fromOpacity = card.Opacity;
			var toY = show ? 0 : Travel();
			var toOpacity = show ? 1.0 : 0;
			card.Animate(
				name,
				v =>
				{
					card.TranslationY = fromY + ((toY - fromY) * v);
					card.Opacity = fromOpacity + ((toOpacity - fromOpacity) * v);
				},
				length: CardMotion,
				easing: show ? Easing.CubicOut : Easing.CubicIn,
				finished: (_, cancelled) =>
				{
					if (cancelled)
					{
						return;
					}

					if (!show)
					{
						card.IsVisible = false;
						card.TranslationY = 0;
						card.Opacity = 1;
					}

					then?.Invoke();
				});
		});
	}

	/// <summary>
	/// Fades the list back behind the Sort &amp; filter card, and brings it
	/// back the same way, rather than all at once.
	/// </summary>
	private void FadeDim(bool dim)
	{
		const string name = "ListDim";
		Dim.AbortAnimation(name);
		var from = Dim.IsVisible ? Dim.Opacity : 0;
		var to = dim ? DimmedTo : 0;
		if (dim)
		{
			Dim.Opacity = from;
			Dim.IsVisible = true;
		}

		if (ReducedMotion.IsOn || from == to)
		{
			Dim.Opacity = to;
			Dim.IsVisible = dim;
			return;
		}

		Dim.Animate(
			name,
			v => Dim.Opacity = from + ((to - from) * v),
			length: CardMotion,
			easing: Easing.CubicInOut,
			finished: (_, cancelled) =>
			{
				if (!cancelled && !dim)
				{
					Dim.IsVisible = false;
				}
			});
	}

	/// <summary>
	/// Keeps the header, the search box, the chips' ends, the list and the
	/// cards at its foot clear of the camera and the tab bar while the list
	/// runs on underneath a bottom one. The chips' strip itself reaches the
	/// screen's edge, so a chip scrolls in from under the camera, but its
	/// padding lets the last one scroll clear.
	/// </summary>
	private void PlaceControls()
	{
		var edges = CoveredEdges.Of(this);
		double pageWidth = Width, clearLeft = edges.Left, clearRight = edges.Right, covered = edges.Bottom;

		// How far up from the screen's foot the list and the cards end.
		double? listFoot = null, cardFoot = null;
#if IOS
		// Above a bottom tab bar, the glass buttons float over its right end.
		covered += TabBarButtons.CoveredBottom;
		if (Handler?.PlatformView is UIKit.UIView page && TabBarButtons.PillIn(page) is { } pill)
		{
			pageWidth = page.Bounds.Width;
			var pageHeight = page.Bounds.Height;
			if (pill.Height > pill.Width)
			{
				// A side tab bar: wherever UIKit's safe area ends, the list stops a
				// gap short of the pill's column, and its foot and the cards' are
				// level with the pill's, as one row of glass and list.
				if (pill.Left + (pill.Width / 2) > pageWidth / 2)
				{
					clearRight = Math.Max(clearRight, pageWidth - pill.Left + GlassGap - 16);
				}
				else
				{
					clearLeft = Math.Max(clearLeft, pill.Right + GlassGap - 16);
				}

				listFoot = cardFoot = Math.Max(0, pageHeight - pill.Bottom);
			}
			else if (pill.Top > pageHeight / 2)
			{
				// A bottom tab bar (not an iPad's, along the top): the list runs on
				// beneath it to its foot, the rows scrolling clear of it.
				listFoot = Math.Max(0, pageHeight - pill.Bottom);
			}
		}
#endif
		// Beside a detail pane, only what reaches into the list's own column.
		var columnWidth = ListColumn.Width > 0 ? ListColumn.Width : pageWidth;
		var left = 16 + clearLeft;
		var right = 16 + Math.Max(0, clearRight - Math.Max(0, pageWidth - columnWidth));
		var foot = listFoot ?? 12 + covered;
		var cardBottom = cardFoot ?? 12 + covered;

		Header.Margin = new Thickness(left, 0, right, 0);
		Notices.Padding = new Thickness(left, 0, right, 0);
		ChipStrip.Padding = new Thickness(left, 6, right, 0);
		CountLine.Margin = new Thickness(left + 4, 6, right, 2);
		RecentStrip.Margin = new Thickness(left, 2, right, 6);
		ListCard.Margin = new Thickness(left, 0, right, foot);
		FilterCard.Margin = new Thickness(left, 6, right, cardBottom);
		Bulk.Margin = new Thickness(left, 6, right, cardBottom);
		var field = new Thickness(left, 4, right, 8);
		if (SearchField.Margin != field)
		{
			SearchField.Margin = field;
		}

		// What covers the list's foot - the tab bar and its glass, the bulk
		// bar - and so how far the last row must scroll to come clear of it.
		var over = listFoot is not null && cardFoot is null ? covered : 0;
		if (Bulk.IsVisible && Bulk.Height > 0)
		{
			over = Math.Max(over, cardBottom + Bulk.Height);
		}

		var spacer = over > foot ? over - foot + 8 : 0;
		if (ListFoot.HeightRequest != spacer)
		{
			ListFoot.HeightRequest = spacer;
		}

		// The device beside the list runs on to the screen's foot too, its
		// cards padded clear of a side tab bar's column and a bottom one.
		_detail?.PlaceContent(new Thickness(0, 0, clearRight, covered));
	}

#if IOS
	private void OnTabBarButtonsMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

	/// <summary>
	/// Search, Sort &amp; filter - filled while a filter or sort is on - and
	/// Refresh by the tab bar, with the pin of the device showing beside the
	/// list; shown again only when one of them changes.
	/// </summary>
	private void UpdateGlass()
	{
		var detail = Split.IsSplit ? _detail?.ViewModel : null;
		var pin = detail?.TogglePinCommand.CanExecute(null) == true;
		var state = (Filtered: _viewModel.HasPanelFilters, Pin: pin, Pinned: pin && detail!.IsPinned);
		if (!_showing || _glass == state)
		{
			return;
		}

		_glass = state;
		List<GlassAction> actions =
		[
			new(
				state.Filtered ? "line.3.horizontal.decrease.circle.fill" : "line.3.horizontal.decrease.circle",
				state.Filtered ? "Sort and filter, filters on" : "Sort and filter",
				() => _viewModel.ToggleFilterPanelCommand.Execute(null)),
			new("arrow.clockwise", "Refresh devices", () => _viewModel.RefreshCommand.Execute(null)),
		];
		if (state.Pin)
		{
			actions.Add(GlassActions.Pin(state.Pinned, () => detail!.TogglePinCommand.Execute(null)));
		}

		TabBarButtons.Show(this, OpenSearch, "Search devices", actions);
	}

	private void OnDetailChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(DeviceDetailViewModel.IsPinned))
		{
			Dispatcher.Dispatch(UpdateGlass);
		}
	}

	private void OnDetailPinChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(UpdateGlass);

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
