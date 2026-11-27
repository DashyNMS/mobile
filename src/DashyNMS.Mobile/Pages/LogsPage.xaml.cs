using System.ComponentModel;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.Logs"/>: from More or the dashboard for every
/// device, or from a device's Event log section with its id and name - the
/// same page, with the Device chip fixed to it (#118, #125).
/// </summary>
public partial class LogsPage : ContentPage, IQueryAttributable, IDetailHost, ISearchHost, IActivePage
{
	private const uint SheetMotion = 220;
	private const double DimmedTo = 0.55;

	private readonly LogsViewModel _viewModel;
	private readonly TabAlignedListLayout _listLayout;
	private readonly ButtonSearch _search;
	private bool _active;
	private bool _isFilterSheetOpen;
	private Thickness _lastGutters = new(16, 0, 16, 0);
#if IOS
	private Foundation.NSObject? _contentSizeObserver;
#endif

	public LogsPage(LogsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		FilterSheet.Attach(_viewModel);
		FilterSheet.CloseRequested += (_, _) => SetFilterSheetOpen(false);
		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
		_viewModel.AutoRefresh.PropertyChanged += OnAutoRefreshPropertyChanged;

		_listLayout = new TabAlignedListLayout(this, Split, ListColumn, ListCard, List, ListFoot, clear =>
		{
			if (Split.Detail is LogEntryView detail) detail.PlaceContent(clear);
		}, PlaceGutters, BottomFade) { FullHeight = true };
		PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading the log…", "Refreshing the log…");
		_search = new ButtonSearch(SearchSlot, SearchField, Search, List);
		SearchSlot.SizeChanged += (_, _) => _listLayout.Place();
		FilterStrip.SizeChanged += (_, _) => _listLayout.Place();
		CompactFilterContainer.SizeChanged += (_, _) => _listLayout.Place();
		ListColumn.SizeChanged += (_, _) =>
		{
			var viewportHeight = ListColumn.Height > 0 ? ListColumn.Height : Height;
			FilterSheet.UpdateMaxHeight(viewportHeight);
			ApplyFilterMode(_lastGutters);
		};
		SearchButton.Command = new Command(OpenSearch);
#if IOS
		Header.BackInGlass = TabBarButtons.IsAvailable;
		SearchButton.IsVisible = !TabBarButtons.IsAvailable;
		Loaded += (_, _) =>
		{
			_contentSizeObserver ??= UIKit.UIApplication.Notifications.ObserveContentSizeCategoryChanged((_, _) =>
				Dispatcher.Dispatch(() => ApplyFilterMode(_lastGutters)));
		};
		Unloaded += (_, _) =>
		{
			_contentSizeObserver?.Dispose();
			_contentSizeObserver = null;
		};
#endif
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		var deviceId = query.TryGetValue(Routes.DeviceIdParameter, out var id) && id is int value ? value : (int?)null;
		query.TryGetValue(Routes.DeviceNameParameter, out var name);
		_viewModel.Initialise(deviceId, name as string);

		// An Event log card's See all: its type.
		if (query.TryGetValue(Routes.LogTypeParameter, out var type))
		{
			_viewModel.ShowEventType(type as string);
		}
	}

	/// <summary>An entry tapped with room beside the list (#88) opens there.</summary>
	public bool TryShowDetail(string route, IDictionary<string, object>? parameters)
	{
		if (!Split.IsSplit || route != Routes.LogEntry || Handler?.MauiContext?.Services is not { } services)
		{
			return false;
		}

		var view = new LogEntryView(services.GetRequiredService<LogEntryViewModel>());
		view.Bar.ShowsBack = false;
		view.ViewModel.IsBesideList = true;
		view.Load(parameters);
		Split.Detail = view;
#if IOS
		PlaceFooter();
#endif
		return true;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Activate();
		FilterSheet.Refresh();
		FilterSheet.IsVisible = _isFilterSheetOpen;
		Dim.IsVisible = _isFilterSheetOpen;
		Dim.Opacity = _isFilterSheetOpen ? DimmedTo : 0;
		ApplyFilterMode(_lastGutters);
		_ = _viewModel.EnsureLoadedAsync();
	}

	/// <summary>Restores retained controls without fetching or resetting the source.</summary>
	public void Activate()
	{
		_active = true;
		Header.Apply(this);
#if IOS
		if (TabBarButtons.IsAvailable)
		{
			TabBarButtons.Show(this, OpenSearch, _viewModel.SearchPlaceholder,
			[
				new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => _ = Shell.Current.GoToAsync(".."), IsBack: true),
			]);
		}
		TabBarButtons.Moved -= OnGlassMoved;
		TabBarButtons.Moved += OnGlassMoved;
		PlaceFooter();
#endif
		_viewModel.AutoRefresh.SetShowing(true);
	}

	/// <summary>Auto-refresh only ticks while the page is showing (#214).</summary>
	protected override void OnDisappearing()
	{
		_active = false;
		base.OnDisappearing();
		CloseSearch();
		if (_isFilterSheetOpen)
		{
			SetFilterSheetOpen(false);
		}
#if IOS
		TabBarButtons.Moved -= OnGlassMoved;
		TabBarButtons.Hide(this);
#endif
		_viewModel.AutoRefresh.SetShowing(false);
	}

	public void OpenSearch()
	{
		if (!_active)
		{
			return;
		}

		if (_isFilterSheetOpen)
		{
			SetFilterSheetOpen(false);
		}

		_search.Open();
	}

	private void CloseSearch() => _search.Close();
	private void OnSearchCompleted(object? sender, EventArgs e) => CloseSearch();
	private void OnOpenFiltersClicked(object? sender, EventArgs e) => SetFilterSheetOpen(true);
	private void OnDimTapped(object? sender, TappedEventArgs e) => SetFilterSheetOpen(false);

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(LogsViewModel.ShowingEventLog) || (e.PropertyName is nameof(LogsViewModel.HasEntries) && !_viewModel.HasEntries))
		{
			Split.Detail = null;
		}

		if (e.PropertyName is nameof(LogsViewModel.CanClearDevice) or nameof(LogsViewModel.DeviceChipText))
		{
			ApplyFilterMode(_lastGutters);
		}
	}

	private void OnAutoRefreshPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(AutoRefresh.IsOn) or null or "")
		{
			ApplyFilterMode(_lastGutters);
		}
	}

	/// <summary>Keeps the filter clip under the camera while resting controls own safe gutters.</summary>
	private void PlaceGutters(Thickness gutters)
	{
		_lastGutters = gutters;
		var left = gutters.Left;
		var right = gutters.Right;
		Header.Margin = new Thickness(left, 0, right, 0);
		LogSwitch.Margin = new Thickness(left, 0, right, 0);
		SearchField.Margin = new Thickness(left, 4, right, 8);
		CompactFilterContainer.Margin = new Thickness(left, 10, right, 0);
#if IOS
		ChipStripPlacement.Apply(this, FilterStrip, ListColumn, topMargin: 10);
#else
		FilterStrip.Padding = new Thickness(left, 0, right, 0);
#endif
		Summary.Margin = new Thickness(left + 4, 6, right, 4);
		ErrorCard.Margin = new Thickness(left, 0, right, 8);
		Busy.Margin = new Thickness(left, 0, right, 0);
		ListCard.Margin = new Thickness(left, 0, right, ListCard.Margin.Bottom);

		var cardBottom = ComputeSheetBottom();
		FilterSheet.Margin = new Thickness(left, 6, right, cardBottom);
		var viewportHeight = ListColumn.Height > 0 ? ListColumn.Height : Height;
		FilterSheet.UpdateMaxHeight(viewportHeight);
		ApplyFilterMode(gutters);
	}

	private void ApplyFilterMode(Thickness gutters)
	{
		var columnWidth = ListColumn.Width > 0 ? ListColumn.Width : Width;
		var availableWidth = Math.Max(0, columnWidth - gutters.Left - gutters.Right);
		var mode = LogFilterLayout.Resolve(
			availableWidth,
			StatusFilterBar.CurrentFontScale(),
			_viewModel.CanClearDevice,
			_viewModel.AutoRefresh.IsOn,
			_viewModel.DeviceChipText);

		var compact = mode == LogFilterMode.CompactButton;
		if (CompactFilterContainer.IsVisible != compact)
		{
			CompactFilterContainer.IsVisible = compact;
		}

		if (FilterStrip.IsVisible == compact)
		{
			FilterStrip.IsVisible = !compact;
		}

		if (!compact && _isFilterSheetOpen)
		{
			SetFilterSheetOpen(false);
		}
	}

	private double ComputeSheetBottom()
	{
#if IOS
		var edges = CoveredEdges.Of(this);
		var covered = edges.Bottom + TabBarButtons.CoveredBottom;
		if (Handler?.PlatformView is UIKit.UIView page && TabBarButtons.PillIn(page) is { } pill)
		{
			var sideBar = pill.Height > pill.Width;
			if (sideBar)
			{
				return Math.Max(12, page.Bounds.Height - pill.Bottom);
			}

			var glass = TabBarButtons.Occupied(page)
				.Select(r => new OccupiedArea(r.Left, r.Top, r.Right, r.Bottom)).ToArray();
			return SetupActionPlacement.BottomGap(page.Bounds.Height, edges.Bottom,
				new OccupiedArea(pill.Left, pill.Top, pill.Right, pill.Bottom), glass);
		}

		return 12 + covered;
#else
		return 12;
#endif
	}

	private void SetFilterSheetOpen(bool open)
	{
		if (_isFilterSheetOpen == open)
		{
			return;
		}

		_isFilterSheetOpen = open;
		if (open)
		{
			CloseSearch();
			FilterSheet.Refresh();
			FilterSheet.Margin = new Thickness(_lastGutters.Left, 6, _lastGutters.Right, ComputeSheetBottom());
			var viewportHeight = ListColumn.Height > 0 ? ListColumn.Height : Height;
			FilterSheet.UpdateMaxHeight(viewportHeight);
		}

		Dispatcher.Dispatch(() =>
		{
			SlideSheet(FilterSheet, _isFilterSheetOpen);
			FadeDim(_isFilterSheetOpen);
		});
	}

	private static void SlideSheet(View sheet, bool show)
	{
		const string name = "LogFilterSheetSlide";
		sheet.AbortAnimation(name);
		var height = sheet.Height > 0 ? sheet.Height : 280;
		var offscreen = height + sheet.Margin.Bottom + 8;

		if (show)
		{
			if (!sheet.IsVisible)
			{
				sheet.TranslationY = offscreen;
				sheet.Opacity = 0;
				sheet.IsVisible = true;
			}

			if (ReducedMotion.IsOn)
			{
				sheet.TranslationY = 0;
				sheet.Opacity = 1;
				return;
			}

			var startY = sheet.TranslationY;
			var startOpacity = sheet.Opacity;
			sheet.Animate(
				name,
				v =>
				{
					sheet.TranslationY = startY * (1 - v);
					sheet.Opacity = startOpacity + ((1 - startOpacity) * v);
				},
				length: SheetMotion,
				easing: Easing.CubicOut);
			return;
		}

		if (!sheet.IsVisible)
		{
			return;
		}

		if (ReducedMotion.IsOn)
		{
			sheet.IsVisible = false;
			sheet.TranslationY = 0;
			sheet.Opacity = 1;
			return;
		}

		var fromY = sheet.TranslationY;
		var fromOpacity = sheet.Opacity;
		sheet.Animate(
			name,
			v =>
			{
				sheet.TranslationY = fromY + ((offscreen - fromY) * v);
				sheet.Opacity = fromOpacity * (1 - v);
			},
			length: SheetMotion,
			easing: Easing.CubicIn,
			finished: (_, cancelled) =>
			{
				if (!cancelled)
				{
					sheet.IsVisible = false;
					sheet.TranslationY = 0;
					sheet.Opacity = 1;
				}
			});
	}

	private void FadeDim(bool dim)
	{
		const string name = "LogFilterDim";
		Dim.AbortAnimation(name);
		var from = Dim.IsVisible ? Dim.Opacity : 0;
		var to = dim ? DimmedTo : 0;
		if (dim)
		{
			Dim.Opacity = from;
			Dim.IsVisible = true;
		}

		if (ReducedMotion.IsOn || Math.Abs(from - to) < 0.001)
		{
			Dim.Opacity = to;
			Dim.IsVisible = dim;
			return;
		}

		Dim.Animate(
			name,
			v => Dim.Opacity = from + ((to - from) * v),
			length: SheetMotion,
			easing: Easing.CubicInOut,
			finished: (_, cancelled) =>
			{
				if (!cancelled && !dim)
				{
					Dim.IsVisible = false;
				}
			});
	}

#if IOS
	private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceFooter);
	private void PlaceFooter()
	{
		if (!_active || !ActivePage.IsCurrent(this)) return;
		_listLayout.Place();
	}
#endif
}
