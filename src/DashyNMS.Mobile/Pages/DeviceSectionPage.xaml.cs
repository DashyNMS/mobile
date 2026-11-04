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
public partial class DeviceSectionPage : ContentPage, IQueryAttributable, ISearchHost, IActivePage
{
    private IDisposable? _edges;
    private readonly ButtonSearch _search;
    private bool _active;
    private VisualElement? _filterFocus;
    private Window? _placementWindow;
#if IOS
    private readonly List<Foundation.NSObject> _keyboardObservers = [];
    private CoreGraphics.CGRect? _keyboardScreen;
#endif

	/// <summary>How long the Sort &amp; filter card takes to rise or go, and the list to dim, as Devices'.</summary>
	private const uint CardMotion = 250;

	/// <summary>How far the list fades back behind the Sort &amp; filter card.</summary>
	private const double DimmedTo = 0.7;

#if IOS
	private bool _showing;

	/// <summary>The glass last shown: the search label, and whether Ports' Sort &amp; filter is there and filled.</summary>
	private (string Search, bool Filter, bool Filtered)? _glass;
#endif

	/// <summary>The narrowest a row goes before the list drops a column.</summary>
	private const double MinRowWidth = 340;

	/// <summary>The most rows across, however wide the screen.</summary>
	private const int MaxColumns = 3;

	private readonly DeviceSectionViewModel _viewModel;

	public DeviceSectionPage(DeviceSectionViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
        _search = new ButtonSearch(SearchSlot, SearchField, Search, List);
        SearchButton.Command = new Command(OpenSearch);
#if IOS
		// Keep clear of the status bar only; the other edges are placed in PlaceControls.
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
		Header.BackInGlass = TabBarButtons.IsAvailable;

		// The footer keeps the last row clear of the tab bar; UIKit's own
		// inset for the safe area would add the bar's height again.
		List.HandlerChanged += (_, _) => StopInsetAdjustment();
		List.Loaded += (_, _) => StopInsetAdjustment();
#endif
		SectionBody.SizeChanged += (_, _) => Dispatcher.Dispatch(PlaceControls);
        SizeChanged += (_, _) => Dispatcher.Dispatch(PlaceControls);
        SectionBody.HandlerChanged += (_, _) => Dispatcher.Dispatch(PlaceControls);
        SectionBody.Loaded += (_, _) => Dispatcher.Dispatch(PlaceControls);
        FilterCard.SizeChanged += (_, _) => Dispatcher.Dispatch(PlaceControls);
        FilterScroll.SizeChanged += (_, _) => RevealFilterFocus();
        FilterCard.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(View.IsVisible)) Dispatcher.Dispatch(PlaceControls);
        };

		// An inventory's nesting reads down one column, so it never spreads across.
		_viewModel.Groups.CollectionChanged += (_, _) => Dispatcher.Dispatch(() =>
		{
            if (!_active) return;
			PlaceControls();
			UpdateNeighbourGraph();
		});
		NeighbourGraph.Picked += row =>
		{
			List.SelectedItem = row;
			var group = _viewModel.Groups.FirstOrDefault(g => g.Contains(row));
			if (group is not null) List.ScrollTo(row, group, ScrollToPosition.Start, animate: !ReducedMotion.IsOn);
		};

		_viewModel.PropertyChanged += (_, e) =>
		{
			switch (e.PropertyName)
			{
				case nameof(DeviceSectionViewModel.IsSearchOpen):
					Dispatcher.Dispatch(() => { if (!_active) return; if (_viewModel.IsSearchOpen) _search.Open(); else _search.Close(); });
					break;
				case nameof(DeviceSectionViewModel.IsFilterPanelOpen):
					Dispatcher.Dispatch(() =>
					{
                        if (!_active) return;
						if (_viewModel.IsPorts && _viewModel.IsFilterPanelOpen)
						{
							KeyboardDismiss.Close(Search);
							_viewModel.DismissSearch();
						}
						SlideCard(FilterCard, _viewModel.IsFilterPanelOpen);
						FadeDim(_viewModel.IsFilterPanelOpen);
					});
					break;
				case nameof(DeviceSectionViewModel.IsPorts) or nameof(DeviceSectionViewModel.HasPanelFilters) or nameof(DeviceSectionViewModel.SearchLabel):
					Dispatcher.Dispatch(UpdateGlass);
					break;
                case nameof(DeviceSectionViewModel.IsNeighbours):
                    Dispatcher.Dispatch(PlaceControls);
                    break;
			}
		};

		var tapOff = new TapGestureRecognizer();
		tapOff.Tapped += (_, _) => _viewModel.IsFilterPanelOpen = false;
		Dim.GestureRecognizers.Add(tapOff);

		// Filtering also raises Scrolled as rows are replaced. Only explicit
		// completion or tapping away from the field should put search away.
		Search.Unfocused += (_, _) => _viewModel.DismissSearch();
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

		// Search and Sort & filter are glass by the tab bar instead.
		if (TabBarButtons.IsAvailable)
		{
			HeaderActions.IsVisible = false;
		}
#endif
	}

	/// <summary>
	/// The glass search button and ⌘F: opens search, or puts the cursor back
	/// in the field if it's open.
	/// </summary>
	public void OpenSearch()
	{
        if (!_active) return;
        _search.Open();
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
        Activate();
    }

    public void Activate()
    {
        if (_placementWindow is not null) _placementWindow.SizeChanged -= OnWindowSizeChanged;
        _placementWindow = Window;
        if (_placementWindow is not null) _placementWindow.SizeChanged += OnWindowSizeChanged;
        if (!_active)
        {
            _active = true;
#if IOS
            _keyboardObservers.Add(UIKit.UIKeyboard.Notifications.ObserveWillChangeFrame((_, args) =>
            {
                _keyboardScreen = args.FrameEnd;
                PlaceControls();
                RevealFilterFocus();
            }));
            _keyboardObservers.Add(UIKit.UIKeyboard.Notifications.ObserveWillHide((_, _) =>
            {
                _keyboardScreen = null;
                PlaceControls();
            }));
#endif
        }
		Header.Apply(this);
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
#if IOS
		_showing = true;
        ManualKeyboardScroll.Acquire(this);
#endif

		// Stop transitions before restoring their final state on retained activation.
        FilterCard.AbortAnimation("CardSlide");
        Dim.AbortAnimation("ListDim");
		// As it was left, without sliding in again.
		FilterCard.IsVisible = _viewModel.IsFilterPanelOpen;
        FilterCard.TranslationY = 0;
        FilterCard.Opacity = 1;
		Dim.IsVisible = _viewModel.IsFilterPanelOpen;
		Dim.Opacity = _viewModel.IsFilterPanelOpen ? DimmedTo : 0;
#if IOS
		_glass = null;
		UpdateGlass();
		TabBarButtons.Moved -= OnTabBarButtonsMoved;
		TabBarButtons.Moved += OnTabBarButtonsMoved;
#endif
		PlaceControls();
		UpdateNeighbourGraph();
	}

	private void UpdateNeighbourGraph()
	{
		List.SelectionMode = _viewModel.IsNeighbours ? SelectionMode.Single : SelectionMode.None;
		if (_viewModel.IsNeighbours)
		{
			NeighbourGraph.Update(_viewModel.Groups.SelectMany(g => g).ToList(), _viewModel.GraphDeviceName);
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
        if (_placementWindow is not null) _placementWindow.SizeChanged -= OnWindowSizeChanged;
        _placementWindow = null;
        _active = false;
        FilterCard.AbortAnimation("CardSlide");
        Dim.AbortAnimation("ListDim");
        _search.Close();
        _edges?.Dispose();
        _edges = null;
#if IOS
		_showing = false;
        ManualKeyboardScroll.Release(this);
        foreach (var observer in _keyboardObservers) observer.Dispose();
        _keyboardObservers.Clear();
        _keyboardScreen = null;
#endif
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
		if (!_active || Width <= 0)
		{
			return;
		}

		var clear = CoveredEdges.ForScrolling(this);
		var left = 16 + clear.Left;
		var right = 16 + clear.Right;
        var bottom = 12 + clear.Bottom;
        var footer = 24d;
        var overlayBottom = bottom;
#if IOS
        StopInsetAdjustment();
        // Query every obstruction in the body host's coordinates. Its bottom
        // is the viewport base in narrow and split modes alike.
        if (SectionBody.Handler?.PlatformView is UIKit.UIView host && host.Bounds.Height > 0)
        {
            var height = (double)host.Bounds.Height;
            var nativePill = TabBarButtons.PillIn(host);
            OccupiedArea? pill = nativePill is { } bar ? new(bar.Left, bar.Top, bar.Right, bar.Bottom) : null;
            var glass = TabBarButtons.Occupied(host)
                .Select(rect => new OccupiedArea(rect.Left, rect.Top, rect.Right, rect.Bottom)).ToArray();
            overlayBottom = SetupActionPlacement.BottomGap(height,
                CoveredEdges.Of(this).Bottom + (pill is null ? TabBarButtons.CoveredBottom : 0), pill, glass);
            bottom = overlayBottom;
            if (nativePill is { } measured && (measured.Height > measured.Width || measured.Top > height / 2))
            {
                bottom = Math.Max(0, height - measured.Bottom);
                footer += Math.Max(0, overlayBottom - bottom);
            }
            if (_keyboardScreen is { } screen && host.Window is { } window)
            {
                var keyboard = host.ConvertRectFromView(window.ConvertRectFromCoordinateSpace(screen, window.Screen.CoordinateSpace), window);
                if (keyboard.Width >= host.Bounds.Width * .8 && keyboard.Bottom >= height)
                {
                    var keyboardGap = height - Math.Max(0, (double)keyboard.Top) + 12;
                    bottom = Math.Max(bottom, keyboardGap);
                    overlayBottom = Math.Max(overlayBottom, keyboardGap);
                }
            }
        }
#endif
        if (FilterCard.IsVisible && FilterCard.Height > 0)
            footer = Math.Max(footer, FilterCard.Height + overlayBottom + 12 - bottom);

		var wideNeighbours = _viewModel.IsNeighbours && Width - left - right >= 800;
		var columns = wideNeighbours ? 2 : 1;
		if (SectionBody.ColumnDefinitions.Count != columns)
		{
			SectionBody.ColumnDefinitions.Clear();
			for (var i = 0; i < columns; i++) SectionBody.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
		}
		Grid.SetRow(Refresh, wideNeighbours ? 0 : 1);
        Grid.SetRow(ListFade, wideNeighbours ? 0 : 1);
        Grid.SetColumn(ListFade, wideNeighbours ? 1 : 0);
		Grid.SetColumn(Refresh, wideNeighbours ? 1 : 0);
		Grid.SetRowSpan(Refresh, wideNeighbours ? 2 : 1);
        Grid.SetRowSpan(ListFade, wideNeighbours ? 2 : 1);
		Grid.SetRowSpan(NeighbourGraph, wideNeighbours ? 2 : 1);
		NeighbourGraph.VerticalOptions = wideNeighbours ? LayoutOptions.Fill : LayoutOptions.Start;
		// Keep a finite list viewport even in short landscape and above the keyboard.
		NeighbourGraph.HeightRequest = wideNeighbours ? -1 : Math.Min(280, Math.Max(0, SectionBody.Height * 0.35));
		NeighbourGraph.Margin = new Thickness(left, 8, wideNeighbours ? 8 : right, wideNeighbours ? overlayBottom : 8);

		Header.Margin = new Thickness(left, 0, right, 0);
		var field = new Thickness(left, 4, right, 8);
		if (SearchField.Margin != field)
		{
			SearchField.Margin = field;
		}

		FilterLine.Margin = new Thickness(left + 4, 4, right, 0);
		ErrorLine.Margin = new Thickness(left, 8, right, 0);
		FilterCard.Margin = new Thickness(left, 6, right, overlayBottom);
        // Root's star row is finite. Its native keyboard/glass clearance is
        // already expressed by overlayBottom; do not add safe area again.
        var panelHeight = Math.Max(0, SectionBody.Height - overlayBottom - 6);
        if (FilterCard.MaximumHeightRequest != panelHeight) FilterCard.MaximumHeightRequest = panelHeight;
		Busy.Margin = new Thickness(left, 8, right, 0);

		// The viewport reaches the pill base; content owns final-row clearance.
		var viewport = new Thickness(0, 0, 0, bottom);
		if (Refresh.Margin != viewport) Refresh.Margin = viewport;

		if (ListFoot.HeightRequest != footer)
		{
			ListFoot.HeightRequest = footer;
		}

		var span = _viewModel.IsPorts ? 2 : 1;
        var paneWidth = _viewModel.IsPorts && SectionBody.Width > 0 ? SectionBody.Width : Width;
		var width = paneWidth - left - right;
        var portStageWidth = width;
        if (_viewModel.IsPorts && width > 0 && Window is { Width: > 0, Height: > 0 } windowBounds)
        {
            // Live logical bounds, never remembered display/orientation metrics.
            // Split-pane and side-pill geometry is an approved rough estimate.
            var landscape = windowBounds.Width > windowBounds.Height;
            var paneShare = Math.Min(1, paneWidth / windowBounds.Width);
            // Current landscape camera/pill insets constrain available width,
            // but do not belong to the hypothetical portrait card width.
            var portraitAvailable = Math.Max(0, Math.Min(windowBounds.Width, windowBounds.Height) * paneShare - 32);
            var fit = PortColumns.Fit(width, portraitAvailable, landscape, ListLayout.HorizontalItemSpacing);
            span = fit.Count;
            portStageWidth = fit.Width;
        }
		else if (!_viewModel.IsPorts && width > 0 && !_viewModel.IsNeighbours && !_viewModel.IsInventory && !_viewModel.Groups.Any(g => g.Any(row => row.Depth > 0)))
		{
			span = Math.Clamp((int)((width + ListLayout.HorizontalItemSpacing) / (MinRowWidth + ListLayout.HorizontalItemSpacing)), 1, MaxColumns);
		}
        // Fixed landscape tracks leave remainder at the trailing edge rather
        // than stretching cards or a sparse final row.
        var trailing = _viewModel.IsPorts ? Math.Max(0, width - portStageWidth) : 0;
        var list = new Thickness(left, 0, right + trailing, 0);
        if (List.Margin != list) List.Margin = list;
        ListFade.Margin = List.Margin;

		if (ListLayout.Span != span)
		{
			ListLayout.Span = span;
		}
	}

    private void OnWindowSizeChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

	/// <summary>
	/// Back, search and - on Ports - Sort &amp; filter, filled while the ports
	/// are sorted or grouped, by the tab bar; shown again only when they change.
	/// </summary>
	private void UpdateGlass()
	{
#if IOS
		var state = (Search: _viewModel.SearchLabel, Filter: _viewModel.IsPorts, Filtered: _viewModel.HasPanelFilters);
		if (!_showing || !TabBarButtons.IsAvailable || _glass == state)
		{
			return;
		}

		_glass = state;
		List<GlassAction> actions =
		[
			new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => { if (_active) _ = Shell.Current.GoToAsync(".."); }, IsBack: true),
		];
		if (state.Filter)
		{
			actions.Add(new(
				state.Filtered ? "line.3.horizontal.decrease.circle.fill" : "line.3.horizontal.decrease.circle",
				state.Filtered ? "Sort and filter, sorted or grouped" : "Sort and filter",
				() => { if (_active) _viewModel.ToggleFilterPanelCommand.Execute(null); }));
		}

		TabBarButtons.Show(this, OpenSearch, state.Search, actions);
#endif
	}

	/// <summary>
	/// Slides <paramref name="card"/> up from the foot of the screen, or back
	/// down out of sight, as Devices' Sort &amp; filter. At once with Reduce Motion.
	/// </summary>
	private void SlideCard(View card, bool show)
	{
		const string name = "CardSlide";
		double Travel() => (card.Height > 0 ? card.Height : 240) + card.Margin.Bottom + 8;

		// Already up, or already gone: nothing to move.
		if (show == card.IsVisible && !card.AnimationIsRunning(name) && card.TranslationY == 0)
		{
			return;
		}

		card.AbortAnimation(name);
		if (ReducedMotion.IsOn || (!show && !card.IsVisible))
		{
			card.IsVisible = show;
			card.TranslationY = 0;
			card.Opacity = 1;
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
            if (!_active) return;
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
					if (!cancelled && !show)
					{
						card.IsVisible = false;
						card.TranslationY = 0;
						card.Opacity = 1;
					}
				});
		});
	}

	/// <summary>Fades the list back behind the Sort &amp; filter card, and brings it back the same way.</summary>
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

	/// <summary>Copies only the selected inventory serial, on an explicit tap.</summary>
	private async void OnCopySerial(object? sender, EventArgs e)
	{
		if (sender is Button { CommandParameter: string serial } && !string.IsNullOrWhiteSpace(serial))
		{
			await Clipboard.Default.SetTextAsync(serial);
			await DisplayAlertAsync("Serial copied", "The serial number is on the clipboard.", "OK");
		}
	}

	// The keyboard's search key: the list is narrowed, so the field goes.
    private void OnFilterPickerFocused(object? sender, FocusEventArgs e)
    {
        _filterFocus = sender as VisualElement;
        RevealFilterFocus();
    }

    /// <summary>Recycled Ports cards use their native button, with no enclosing gesture.</summary>
    private void OnRowBindingContextChanged(object? sender, EventArgs e)
    {
        if (sender is not Border border) return;
        border.GestureRecognizers.Clear();
        if (border.BindingContext is SectionRow { PortLayout: false } row)
            border.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = _viewModel.OpenLinkCommand,
                CommandParameter = row,
            });
    }

    private void RevealFilterFocus() => Dispatcher.Dispatch(() =>
    {
        if (_active && FilterCard.IsVisible && _filterFocus is { IsFocused: true } focus)
            _ = FilterScroll.ScrollToAsync(focus, ScrollToPosition.MakeVisible, !ReducedMotion.IsOn);
    });

	private void OnSearchCompleted(object? sender, EventArgs e) { _search.Close(); _viewModel.DismissSearch(); }

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
