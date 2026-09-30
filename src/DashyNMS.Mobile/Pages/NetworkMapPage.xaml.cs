using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Topology;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// The network map (#86). The view model builds and lays out the graph; this
/// draws it and turns fingers into moves: drag to pan, pinch to zoom around
/// the pinch, tap to select, double-tap to zoom in.
/// </summary>
public partial class NetworkMapPage : ContentPage
{
	private readonly NetworkMapViewModel _viewModel;
	private readonly NetworkMapDrawable _drawable;
	private PointF _panStart;
	private bool _loaded;
	private bool _fitted;

	public NetworkMapPage(NetworkMapViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_drawable = new NetworkMapDrawable(viewModel);
		MapCanvas.Drawable = _drawable;

		_viewModel.LayoutChanged += (_, _) => Dispatcher.Dispatch(() =>
		{
			_fitted = Fit();
			MapCanvas.Invalidate();
		});
		_viewModel.RedrawRequested += (_, _) => Dispatcher.Dispatch(MapCanvas.Invalidate);
		_viewModel.CenterOnRequested += (_, node) => Dispatcher.Dispatch(() =>
		{
			_drawable.CenterOn(node, MapCanvas.Bounds.Size);
			MapCanvas.Invalidate();
		});

		// Laid out before the view had a size: fit once it has one.
		MapCanvas.SizeChanged += (_, _) =>
		{
			if (!_fitted)
			{
				_fitted = Fit();
			}

			MapCanvas.Invalidate();
		};

		var pan = new PanGestureRecognizer();
		pan.PanUpdated += OnPan;
		var pinch = new PinchGestureRecognizer();
		pinch.PinchUpdated += OnPinch;
		var tap = new TapGestureRecognizer();
		tap.Tapped += OnTap;
		var doubleTap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
		doubleTap.Tapped += OnDoubleTap;
		MapCanvas.GestureRecognizers.Add(pan);
		MapCanvas.GestureRecognizers.Add(pinch);
		MapCanvas.GestureRecognizers.Add(doubleTap);
		MapCanvas.GestureRecognizers.Add(tap);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
		if (!_loaded)
		{
			_loaded = true;
			_viewModel.RefreshCommand.Execute(null);
		}
	}

	private bool Fit()
	{
		if (MapCanvas.Width <= 0 || _viewModel.Nodes.Count == 0)
		{
			return false;
		}

		_drawable.FitTo(MapCanvas.Bounds.Size);
		return true;
	}

	private void OnFitClicked(object? sender, EventArgs e)
	{
		Fit();
		MapCanvas.Invalidate();
	}

	private void OnPan(object? sender, PanUpdatedEventArgs e)
	{
		switch (e.StatusType)
		{
			case GestureStatus.Started:
				_panStart = _drawable.Offset;
				break;
			case GestureStatus.Running:
				_drawable.Offset = new PointF((float)(_panStart.X + e.TotalX), (float)(_panStart.Y + e.TotalY));
				MapCanvas.Invalidate();
				break;
		}
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

	private void OnTap(object? sender, TappedEventArgs e)
	{
		if (e.GetPosition(MapCanvas) is not { } point)
		{
			return;
		}

		// A tap on empty space clears the selection, as a click does on desktop.
		_viewModel.Select(_drawable.HitTest(new PointF((float)point.X, (float)point.Y)));
	}

	private void OnDoubleTap(object? sender, TappedEventArgs e)
	{
		if (e.GetPosition(MapCanvas) is not { } point)
		{
			return;
		}

		_drawable.ZoomAround(new PointF((float)point.X, (float)point.Y), 2);
		MapCanvas.Invalidate();
	}
}
