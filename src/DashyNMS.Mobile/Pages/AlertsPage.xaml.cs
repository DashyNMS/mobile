using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Alerts;
using DesktopNMS.Core.Models;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Pages;

/// <summary>The Alerts tab; the dashboard's counts come here with <see cref="Routes.AlertFilterParameter"/>.</summary>
/// <remarks>
/// Laid out as Devices is, so the two tabs work the same way. On iOS the
/// screen is one display: the chips scroll in from under the camera, and
/// the list runs on down to the foot of the Liquid Glass tab bar - under a
/// bottom one, its rows scrolling clear; short of a side one's column, its
/// foot level with the pill's. Search, Refresh and Export are glass by the
/// tab bar on iOS 26 and later, and header icons before that and on
/// Android. What can be tapped is placed clear of the camera and glass here
/// (see <see cref="CoveredEdges"/>); beside a detail pane (#88) only the
/// edges that reach the list's own column count.
/// </remarks>
public partial class AlertsPage : ContentPage, IQueryAttributable, IDetailHost, ISearchHost
{
	/// <summary>The gap kept between the list and a side tab bar's glass.</summary>
	private const double GlassGap = 12;

	/// <summary>How long the search box takes to slide in or out.</summary>
	private const uint SearchSlide = 220;

	/// <summary>How long the bulk bar takes to slide.</summary>
	private const uint CardMotion = 250;

	private readonly AlertsViewModel _viewModel;
	private readonly ShortcutReturn _shortcut = new();

	/// <summary>The alert showing beside the list, if any - highlighted while the pane is there.</summary>
	private AlertKey? _shown;

	/// <summary>The alert beside the list (#88), padded clear of the glass with the list.</summary>
	private AlertDetailView? _detail;

	public AlertsPage(AlertsViewModel viewModel)
	{
		InitializeComponent();
#if IOS
		// Keep clear of the status bar only; the other edges are placed in PlaceControls.
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
#endif
		BindingContext = _viewModel = viewModel;
		PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading alerts…", "Refreshing alerts…");
		Split.SplitChanged += (_, _) =>
		{
			_viewModel.SelectKey(Split.IsSplit ? _shown : null);
			PlaceControls();
		};

		CoveredEdges.Watch(this, PlaceControls);

		// The list's column narrows as a detail pane comes, and the bulk bar
		// sets how far the last row must scroll to clear it.
		ListColumn.SizeChanged += (_, _) => PlaceControls();
		Bulk.SizeChanged += (_, _) => PlaceControls();

		_viewModel.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(AlertsViewModel.IsSearchOpen))
			{
				Dispatcher.Dispatch(() => SlideSearch(_viewModel.IsSearchOpen));
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

		// Search, Refresh and Export are Liquid Glass by the tab bar instead.
		if (TabBarButtons.IsAvailable)
		{
			HeaderActions.IsVisible = false;
		}
#endif
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.AlertFilterParameter, out var value) && value is string kind)
		{
			_shortcut.Arrived();
			_viewModel.ShowOnly(kind);
		}

        if (query.TryGetValue(Routes.AlertGroupKeyParameter, out var group) && AlertTargets.ParseGroup(group.ToString()) is { } key)
        {
            _shortcut.Arrived();
            _viewModel.ShowGroup(key);
        }
        if (query.TryGetValue(Routes.AlertNoticeParameter, out var notice) && notice is string text)
        {
            _shortcut.Arrived();
            _viewModel.ShowNotice(text);
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
	/// An alert tapped with room beside the list (#88) opens there. Acknowledging
	/// it there changes its row here, as acknowledging from the list does.
	/// </summary>
	public bool TryShowDetail(string route, IDictionary<string, object>? parameters)
	{
		if (!Split.IsSplit || route != Routes.AlertDetail || Handler?.MauiContext?.Services is not { } services)
		{
			return false;
		}

		var view = new AlertDetailView(services.GetRequiredService<AlertDetailViewModel>());
		view.Bar.ShowsBack = false;
		view.ViewModel.AlertChanged += (_, change) => _viewModel.ShowChange(change);
		view.Load(parameters);
		Split.Detail = _detail = view;
		PlaceControls();
		_shown = parameters?.TryGetValue(Routes.AlertKeyParameter, out var id) == true ? AlertTargets.ParseKey(id.ToString()) : null;
		_viewModel.SelectKey(_shown);
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
#if IOS
		TabBarButtons.Show(this, OpenSearch, "Search alerts",
		[
			new("arrow.clockwise", "Refresh alerts", () => _viewModel.RefreshCommand.Execute(null)),
			new("square.and.arrow.up", "Export as CSV", () => _viewModel.ExportCsvCommand.Execute(null)),
		]);
		TabBarButtons.Moved += OnTabBarButtonsMoved;
#endif

		// As it was left, without sliding in again.
		Bulk.IsVisible = _viewModel.Selection.IsSelecting;
		PlaceControls();
		_viewModel.RefreshCommand.Execute(null);
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
	/// Slides <paramref name="card"/> (the bulk bar) up from the foot of the
	/// screen, or back down out of sight - then <paramref name="then"/>. At
	/// once with Reduce Motion.
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
	/// Keeps the header, the search box, the chips' ends, the list and the
	/// bulk bar clear of the camera and the tab bar while the list runs on
	/// underneath a bottom one. The chips' strip itself reaches the screen's
	/// edge, so a chip scrolls in from under the camera, but its padding lets
	/// the last one scroll clear.
	/// </summary>
	private void PlaceControls()
	{
		var edges = CoveredEdges.Of(this);
		double pageWidth = Width, clearLeft = edges.Left, clearRight = edges.Right, covered = edges.Bottom;

		// How far up from the screen's foot the list and the bulk bar end.
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
				// gap short of the pill's column, and its foot and the bulk bar's
				// are level with the pill's, as one row of glass and list.
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
		ListCard.Margin = new Thickness(left, 0, right, foot);
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

		// The alert beside the list runs on to the screen's foot too, its
		// cards padded clear of a side tab bar's column and a bottom one.
		_detail?.PlaceContent(new Thickness(0, 0, clearRight, covered));
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
