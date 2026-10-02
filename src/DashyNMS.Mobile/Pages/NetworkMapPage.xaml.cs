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
		BindingContext = _viewModel = viewModel;
		_drawable = new NetworkMapDrawable(viewModel);
		MapCanvas.Drawable = _drawable;

		_viewModel.LayoutChanged += (_, _) => Dispatcher.Dispatch(OnLayoutChanged);
		_viewModel.RedrawRequested += (_, _) => Dispatcher.Dispatch(MapCanvas.Invalidate);
		_viewModel.CenterOnRequested += (_, node) => Dispatcher.Dispatch(() =>
		{
			_drawable.CenterOn(node, MapCanvas.Bounds.Size);
			MapCanvas.Invalidate();
		});

		// Laid out before the view had a size: place it once it has one.
		MapCanvas.SizeChanged += (_, _) =>
		{
			if (!_fitted)
			{
				_fitted = ShowView();
			}

			MapCanvas.Invalidate();
		};

		MapCanvas.StartInteraction += OnTouchStart;
		MapCanvas.DragInteraction += OnTouchDrag;
		MapCanvas.EndInteraction += OnTouchEnd;
		MapCanvas.CancelInteraction += (_, _) => ResetTouch();

		var pinch = new PinchGestureRecognizer();
		pinch.PinchUpdated += OnPinch;
		MapCanvas.GestureRecognizers.Add(pinch);
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
			_drawable.FitTo(MapCanvas.Bounds.Size);
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

	private void OnFitClicked(object? sender, EventArgs e)
	{
		_drawable.FitTo(MapCanvas.Bounds.Size);
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
