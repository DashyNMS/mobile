using System.Globalization;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Topology;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// The network map (#86). The view model builds and lays out the graph; this
/// draws it and turns fingers into moves: drag to pan, pinch to zoom around
/// the pinch, tap to select, double-tap to zoom in - and drag the selected
/// device to move it, as desktop's drag does, remembered for next time. The
/// zoom and position are remembered too, per server and filter.
/// </summary>
/// <remarks>
/// One finger comes from the graphics view's own touch events rather than
/// pan and tap gestures, which don't say where a touch started - needed to
/// tell dragging the selected device from panning the map.
/// <para>On iOS the map fills the screen under the camera and the Liquid
/// Glass tab bar, and the chips scroll in from under the camera, as they
/// would on a display that size; what can be tapped is placed clear of
/// them here (see <see cref="CoveredEdges"/>).</para>
/// </remarks>
public partial class NetworkMapPage : ContentPage
{
	/// <summary>How far (points) a touch must move before it's a drag rather than a tap.</summary>
	private const float DragThreshold = 8;

	private static readonly TimeSpan DoubleTapWithin = TimeSpan.FromMilliseconds(300);

	private readonly NetworkMapViewModel _viewModel;
	private readonly NetworkMapDrawable _drawable;
	private bool _loaded;
	private bool _fitted;

	/// <summary>The filter the current zoom and position belong to.</summary>
	private string? _viewKey;

	private PointF _pressAt;
	private PointF _lastAt;
	private bool _moved;
	private bool _multiTouch;
	private NetworkNode? _dragging;
	private DateTime _lastTapAt;
	private PointF _lastTapPoint;
	private IDispatcherTimer? _jiggleTimer;
	private DateTime _lastFrame;

	public NetworkMapPage(NetworkMapViewModel viewModel)
	{
		InitializeComponent();
#if IOS
		// Keep clear of the status bar only; the other edges are placed in PlaceControls.
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
#endif
		BindingContext = _viewModel = viewModel;
		_drawable = new NetworkMapDrawable(viewModel);
		MapCanvas.Drawable = _drawable;

		_viewModel.LayoutChanged += (_, _) => Dispatcher.Dispatch(OnLayoutChanged);
		_viewModel.RedrawRequested += (_, _) => Dispatcher.Dispatch(MapCanvas.Invalidate);
		_viewModel.CenterOnRequested += (_, node) => Dispatcher.Dispatch(() =>
		{
			_drawable.CenterOn(node, VisibleArea());
			MapCanvas.Invalidate();
		});

		// Laid out before the view had a size: place it once it has one - and
		// once the part of it that's visible has, which Fit aims for.
		EventHandler sized = (_, _) =>
		{
			if (!_fitted)
			{
				_fitted = ShowView();
			}

			MapCanvas.Invalidate();
		};
		MapCanvas.SizeChanged += sized;
		MapControls.SizeChanged += sized;

		CoveredEdges.Watch(this, PlaceControls);
		SelectionCard.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(IsVisible))
			{
				PlaceControls();
			}
		};

		// Once shown and laid out, it can be fitted to its row.
		SearchField.SizeChanged += (_, _) => PlaceControls();

		MapCanvas.StartInteraction += OnTouchStart;
		MapCanvas.DragInteraction += OnTouchDrag;
		MapCanvas.EndInteraction += OnTouchEnd;
		MapCanvas.CancelInteraction += (_, _) => ResetTouch();

		var pinch = new PinchGestureRecognizer();
		pinch.PinchUpdated += OnPinch;
		MapCanvas.GestureRecognizers.Add(pinch);

		// The box slides down from under the chips as search opens, straight
		// into typing, and back up as it closes, taking the keyboard with it.
		_viewModel.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(NetworkMapViewModel.IsSearchOpen))
			{
				Dispatcher.Dispatch(() => SlideSearch(_viewModel.IsSearchOpen));
			}
			else if (e.PropertyName == nameof(NetworkMapViewModel.IsSelectionExpanded))
			{
				Dispatcher.Dispatch(SizeCard);
			}
		};

		// Done with the keyboard - put away, or the map touched: done searching.
		Search.Unfocused += (_, _) => _viewModel.DismissSearch();

		FitButton.Command = new Command(Fit);
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

		// Search, Refresh, Fit and Reset layout are Liquid Glass by the tab bar instead.
		if (TabBarButtons.IsAvailable)
		{
			SearchButton.IsVisible = false;
			RefreshButton.IsVisible = false;
			FitButton.IsVisible = false;
			ResetLayoutChip.IsVisible = false;
		}
#endif
	}

	/// <summary>How long the search box takes to slide in or out.</summary>
	private const uint SearchSlide = 220;

	/// <summary>
	/// Slides the search box down from under the chips, as if from behind the
	/// fade, and into typing - or back up out of sight, closing the keyboard.
	/// Shown or hidden at once with Reduce Motion.
	/// </summary>
	private void SlideSearch(bool open)
	{
		const string name = "SearchSlide";
		var travel = -(SearchField.HeightRequest + SearchField.Margin.Top + 4);
		SearchField.AbortAnimation(name);
		if (open)
		{
			// Out of sight before it shows, or it flashes in place first.
			if (!SearchField.IsVisible && !ReducedMotion.IsOn)
			{
				SearchField.TranslationY = travel;
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
			if (!_viewModel.IsSearchOpen)
			{
				SearchField.IsVisible = false;
			}

			SearchField.TranslationY = 0;
			SearchField.Opacity = 1;
		}

		if (ReducedMotion.IsOn || (!open && !SearchField.IsVisible))
		{
			Finish();
			return;
		}

		// From wherever it is now, so a quick open-close doesn't jump.
		var fromY = SearchField.TranslationY;
		var fromOpacity = SearchField.Opacity;
		var toY = open ? 0 : travel;
		var toOpacity = open ? 1.0 : 0;
		SearchField.Animate(
			name,
			v =>
			{
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


	/// <summary>Shorter than this, the closed device card is capped lower, leaving the map most of the screen (#296).</summary>
	private const double ShortScreen = 740;

	/// <summary>How much of the map the device card takes with its connections open.</summary>
	private const double OpenCardShare = 0.6;

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		SizeCard();
	}

	/// <summary>
	/// Closed, the device card is as small as its name and state allow,
	/// capped on a short screen. Open, it takes most of the map so its
	/// connections can be read: capped as the closed card is, the list was
	/// cut to a line or two on iPhone Duo's outer display.
	/// </summary>
	private void SizeCard()
	{
		var height = Height;
		if (_viewModel.IsSelectionExpanded && MapArea.Height > 0)
		{
			SelectionCard.MaximumHeightRequest = double.PositiveInfinity;
			SelectionCard.HeightRequest = Math.Max(240, MapArea.Height * OpenCardShare);
		}
		else
		{
			SelectionCard.HeightRequest = -1;
			SelectionCard.MaximumHeightRequest = height > 0 && height < ShortScreen ? 200 : 300;
		}
	}

	/// <summary>
	/// Keeps the header, the chips' ends, Fit and the device card clear of
	/// the camera and tab bar while the map runs on underneath them. The
	/// chips' strip itself reaches the screen's edge, so a chip scrolls in
	/// from under the camera, but its padding lets the last one scroll clear.
	/// </summary>
	private void PlaceControls()
	{
		var edges = CoveredEdges.Of(this);
#if IOS
		// Above a bottom tab bar, the search button floats over its right end.
		edges = new Thickness(edges.Left, edges.Top, edges.Right, edges.Bottom + TabBarButtons.CoveredBottom);
#endif
		Header.Margin = new Thickness(16 + edges.Left, 0, 16 + edges.Right, 0);
		SearchPanel.Padding = new Thickness(16 + edges.Left, 0, 16 + edges.Right, 0);
		ChipStrip.Padding = new Thickness(16 + edges.Left, 4, 16 + edges.Right, 8);

		// The card has the foot when it shows, and keeps clear itself - its
		// foot level with a side tab bar's, as one row of glass and card.
		MapControls.Margin = new Thickness(edges.Left, 0, edges.Right, SelectionCard.IsVisible ? 0 : edges.Bottom);
		var cardFoot = 12 + edges.Bottom;
#if IOS
		if (Handler?.PlatformView is UIKit.UIView page && TabBarButtons.PillIn(page) is { } pill && pill.Height > pill.Width)
		{
			cardFoot = Math.Max(0, page.Bounds.Height - pill.Bottom);
		}
#endif
		SelectionCard.Margin = new Thickness(12 + edges.Left, 6, 12 + edges.Right, cardFoot);

		// The search field goes as wide as its own row allows: past the
		// column the tab bar sits at the foot of, short of the camera. Its
		// strip runs the screen's width.
		if (SearchField.IsVisible && SearchField.Height > 0)
		{
			var beside = CoveredEdges.Beside(this, SearchField);
			var margin = new Thickness(12 + beside.Left, 8, 12 + beside.Right, 8);
			if (SearchField.Margin != margin)
			{
				SearchField.Margin = margin;
			}
		}
	}

#if IOS
	private void OnTabBarButtonsMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

	// The glass search button: opens search, or puts the cursor back in the
	// box if it's open - closing is tapping off it or pressing Search.
	private void OnGlassSearch()
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
#endif

	/// <summary>
	/// The part of the map nothing covers, in the map's own points: where
	/// Fit and centring aim, so nodes don't land under the tab bar or card.
	/// </summary>
	private RectF VisibleArea()
	{
		if (MapControls.Width <= 0 || MapControls.Height <= 0)
		{
			return new RectF(0, 0, (float)MapCanvas.Width, (float)MapCanvas.Height);
		}

		return new RectF(
			(float)(MapControls.X - MapArea.X), (float)(MapControls.Y - MapArea.Y), (float)MapControls.Width, (float)MapControls.Height);
	}

	/// <summary>
	/// Steps the wobble (#106) each frame while anything is settling, then
	/// stops - an idle map runs no timer.
	/// </summary>
	private void StartJiggling()
	{
		if (!_viewModel.Jiggle.IsMoving || _jiggleTimer is { IsRunning: true })
		{
			return;
		}

		_jiggleTimer ??= CreateJiggleTimer();
		_lastFrame = DateTime.UtcNow;
		_jiggleTimer.Start();
	}

	private IDispatcherTimer CreateJiggleTimer()
	{
		var timer = Dispatcher.CreateTimer();
		timer.Interval = TimeSpan.FromSeconds(1.0 / 60);
		timer.Tick += (_, _) =>
		{
			var now = DateTime.UtcNow;
			_viewModel.Jiggle.Step((now - _lastFrame).TotalSeconds);
			_lastFrame = now;
			MapCanvas.Invalidate();
			if (!_viewModel.Jiggle.IsMoving)
			{
				timer.Stop();
			}
		};
		return timer;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
#if IOS
		TabBarButtons.Show(this, OnGlassSearch, "Find a device",
		[
			new GlassAction("arrow.clockwise", "Refresh the map", () => _viewModel.RefreshCommand.Execute(null)),
			new GlassAction("viewfinder", "Fit the whole map", Fit),
			new GlassAction("arrow.uturn.backward", "Reset layout", () => _viewModel.ResetLayoutCommand.Execute(null)),
		]);
		TabBarButtons.Moved += OnTabBarButtonsMoved;
#endif
		PlaceControls();

		// Back from Settings, the jiggle may have been switched; never with Reduce Motion.
		_viewModel.UpdateJiggle(!ReducedMotion.IsOn);
		if (!_loaded)
		{
			_loaded = true;
			_viewModel.RefreshCommand.Execute(null);
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		KeyboardDismiss.Close(Search);
#if IOS
		TabBarButtons.Moved -= OnTabBarButtonsMoved;
		TabBarButtons.Hide(this);
#endif
		SaveView();
		_jiggleTimer?.Stop();
		_viewModel.Jiggle.Clear();
	}

	/// <summary>
	/// A new graph. The same filter as before (Neighbours or Unlinked turned
	/// on, a reset, a refresh) keeps the view where it is; another filter
	/// goes back to where it was left, or fits.
	/// </summary>
	private void OnLayoutChanged()
	{
		if (_viewModel.ScopeKey != _viewKey || !_fitted)
		{
			SaveView();
			_fitted = ShowView();
		}

		MapCanvas.Invalidate();
	}

	/// <summary>The remembered zoom and position for this filter, or the whole map fitted.</summary>
	private bool ShowView()
	{
		if (MapCanvas.Width <= 0 || _viewModel.Nodes.Count == 0)
		{
			return false;
		}

		_viewKey = _viewModel.ScopeKey;
		var saved = Preferences.Default.Get<string?>(ViewPreference(_viewKey), null)?.Split('|');
		if (saved is { Length: 3 }
			&& double.TryParse(saved[0], CultureInfo.InvariantCulture, out var scale)
			&& float.TryParse(saved[1], CultureInfo.InvariantCulture, out var x)
			&& float.TryParse(saved[2], CultureInfo.InvariantCulture, out var y))
		{
			_drawable.Scale = Math.Clamp(scale, NetworkMapDrawable.MinScale, NetworkMapDrawable.MaxScale);
			_drawable.Offset = new PointF(x, y);
		}
		else
		{
			_drawable.FitTo(VisibleArea());
		}

		return true;
	}

	private void SaveView()
	{
		if (_viewKey is null)
		{
			return;
		}

		Preferences.Default.Set(
			ViewPreference(_viewKey),
			string.Join('|', _drawable.Scale.ToString(CultureInfo.InvariantCulture), _drawable.Offset.X.ToString(CultureInfo.InvariantCulture), _drawable.Offset.Y.ToString(CultureInfo.InvariantCulture)));
	}

	private static string ViewPreference(string scopeKey) => "networkmap.view." + scopeKey;

	// The keyboard's search key: the map has found the device, so it goes.
	private void OnSearchCompleted(object? sender, EventArgs e) => _viewModel.DismissSearch();

	private void Fit()
	{
		_drawable.FitTo(VisibleArea());
		MapCanvas.Invalidate();
	}

	private void OnTouchStart(object? sender, TouchEventArgs e)
	{
		if (e.Touches.Length != 1)
		{
			// A second finger: a pinch, which the pinch gesture handles.
			_multiTouch = true;
			_dragging = null;
			return;
		}

		_multiTouch = false;
		_moved = false;
		_pressAt = _lastAt = e.Touches[0];

		// Back to the map: the keyboard goes, what was typed stays.
		if (Search.IsFocused)
		{
			KeyboardDismiss.Close(Search);
		}

		// Only the selected device moves, so a pan that starts on a device
		// doesn't drag it by mistake: tap it first, then drag it.
		_dragging = _viewModel.SelectedNode is { } selected && ReferenceEquals(_drawable.HitTest(_pressAt), selected) ? selected : null;
	}

	private void OnTouchDrag(object? sender, TouchEventArgs e)
	{
		if (_multiTouch || e.Touches.Length != 1)
		{
			_multiTouch = true;
			return;
		}

		var point = e.Touches[0];
		if (!_moved && point.Distance(_pressAt) < DragThreshold)
		{
			return;
		}

		_moved = true;
		if (_dragging is { } node)
		{
			var map = _drawable.ToMap(point);
			_viewModel.Dragging(node, map.X - node.X, map.Y - node.Y);
			node.X = map.X;
			node.Y = map.Y;
			StartJiggling();
		}
		else
		{
			_drawable.Offset = new PointF(_drawable.Offset.X + point.X - _lastAt.X, _drawable.Offset.Y + point.Y - _lastAt.Y);
		}

		_lastAt = point;
		MapCanvas.Invalidate();
	}

	private void OnTouchEnd(object? sender, TouchEventArgs e)
	{
		if (_multiTouch)
		{
			ResetTouch();
			return;
		}

		if (_dragging is { } dragged && _moved)
		{
			_viewModel.NodeMoved(dragged);
		}
		else if (!_moved)
		{
			var now = DateTime.UtcNow;
			if (now - _lastTapAt < DoubleTapWithin && _pressAt.Distance(_lastTapPoint) < 30)
			{
				_drawable.ZoomAround(_pressAt, 2);
				_lastTapAt = DateTime.MinValue;
				MapCanvas.Invalidate();
			}
			else
			{
				// A tap on empty space clears the selection, as a click does on desktop.
				_viewModel.Select(_drawable.HitTest(_pressAt));
				_lastTapAt = now;
				_lastTapPoint = _pressAt;
			}
		}

		ResetTouch();
	}

	private void ResetTouch()
	{
		_dragging = null;
		_moved = false;
		_multiTouch = false;
	}

	private void OnPinch(object? sender, PinchGestureUpdatedEventArgs e)
	{
		if (e.Status != GestureStatus.Running)
		{
			return;
		}

		// ScaleOrigin is a fraction of the view; the pinch's own step is Scale.
		var origin = new PointF((float)(e.ScaleOrigin.X * MapCanvas.Width), (float)(e.ScaleOrigin.Y * MapCanvas.Height));
		_drawable.ZoomAround(origin, e.Scale);
		MapCanvas.Invalidate();
	}
}
