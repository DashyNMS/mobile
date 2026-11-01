using System.ComponentModel;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.DeviceGraphs"/>, with the device id and name.</summary>
public partial class DeviceGraphsPage : ContentPage, IQueryAttributable
{
	private readonly DeviceGraphsViewModel _viewModel;

	public DeviceGraphsPage(DeviceGraphsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_viewModel.DarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
		_viewModel.PropertyChanged += OnViewModelChanged;
		GraphView.SizeChanged += (_, _) => FitHeight();
#if IOS
		// Keep only the status bar's inset; place side and bottom clearances ourselves.
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
		Header.BackInGlass = TabBarButtons.IsAvailable;
		GraphScroll.HandlerChanged += (_, _) => StopInsetAdjustment();
		GraphScroll.Loaded += (_, _) => StopInsetAdjustment();
#endif
		CoveredEdges.Watch(this, PlaceControls);

		// The page is only ever our own graph page: never follow a link or
		// redirect out of it, whatever the (untrusted) graph contains.
		GraphView.Navigating += (_, e) =>
		{
			if (!IsOwnPage(e.Url))
			{
				e.Cancel = true;
			}
		};

		if (Application.Current is { } app)
		{
			app.RequestedThemeChanged += (_, e) =>
			{
				_viewModel.DarkTheme = e.RequestedTheme == AppTheme.Dark;
				_ = _viewModel.LoadGraphAsync();
			};
		}
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.DeviceIdParameter, out var id) && id is int deviceId)
		{
			query.TryGetValue(Routes.DeviceNameParameter, out var name);
			if (query.TryGetValue(Routes.PortParameter, out var port) && port is string ifName)
			{
				query.TryGetValue(Routes.PortNameParameter, out var portName);
				// A dashboard port graph (#172) opens on its graph and range.
				query.TryGetValue(Routes.GraphParameter, out var portGraph);
				var portRange = query.TryGetValue(Routes.GraphRangeParameter, out var pr) && pr is DesktopNMS.Core.Models.GraphTimeRangePreset portPreset ? portPreset : (DesktopNMS.Core.Models.GraphTimeRangePreset?)null;
				_ = _viewModel.LoadPortAsync(deviceId, ifName, portName as string, name as string, portGraph as string, portRange);
			}
			else
			{
				// The graph that was tapped, and its range, if the caller knows (#119).
				query.TryGetValue(Routes.GraphParameter, out var graph);
				var range = query.TryGetValue(Routes.GraphRangeParameter, out var r) && r is DesktopNMS.Core.Models.GraphTimeRangePreset preset ? preset : (DesktopNMS.Core.Models.GraphTimeRangePreset?)null;
				_ = _viewModel.LoadAsync(deviceId, name as string, graph as string, range);
			}
		}
	}

	private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(DeviceGraphsViewModel.HasSeries))
		{
			Dispatcher.Dispatch(PlaceControls);
		}
		else if (e.PropertyName == nameof(DeviceGraphsViewModel.GraphAspect))
		{
			FitHeight();
		}
		else if (e.PropertyName == nameof(DeviceGraphsViewModel.GraphPage))
		{
			GraphView.Source = _viewModel.GraphPage is { } html ? new HtmlWebViewSource { Html = html } : null;
		}
	}

	/// <summary>
	/// Loading an <see cref="HtmlWebViewSource"/> can itself raise Navigating,
	/// with a blank, data: or local file: address (iOS uses the app bundle as
	/// the base URL, Android file:///android_asset/). Anything else - http(s),
	/// tel:, a custom scheme - is a way out of the page.
	/// </summary>
	private static bool IsOwnPage(string? url) =>
		string.IsNullOrEmpty(url)
		|| url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
		|| url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
		|| url.StartsWith("file:", StringComparison.OrdinalIgnoreCase);

	private void FitHeight()
	{
		if (GraphView.Width > 0)
		{
			// The graph's own shape: shorter once its legend is cropped for ours (#158).
			GraphView.HeightRequest = GraphView.Width * _viewModel.GraphAspect;
		}
	}

	/// <summary>The app's own header, as every page (#142).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
#if IOS
		if (TabBarButtons.IsAvailable)
		{
			TabBarButtons.Show(this, null, string.Empty,
			[
				new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => _ = Shell.Current.GoToAsync(".."), IsBack: true),
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

#if IOS
	private void StopInsetAdjustment()
	{
		if (GraphScroll.Handler?.PlatformView is UIKit.UIScrollView scroll)
		{
			scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
		}
	}

	private void OnTabBarButtonsMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);
#endif

	/// <summary>Uses the whole usable width, with series beside the graph when both have room.</summary>
	private void PlaceControls()
	{
		if (Width <= 0) return;
		var clear = CoveredEdges.ForScrolling(this);
		var left = 16 + clear.Left;
		var right = 16 + clear.Right;
		Header.Margin = new Thickness(left, 0, right, 0);
		ContentColumn.Padding = new Thickness(left, 20, right, 24 + clear.Bottom);
		var wide = Width - left - right >= 800 && _viewModel.HasSeries;
		var columns = wide ? 2 : 1;
		if (GraphAndSeries.ColumnDefinitions.Count != columns)
		{
			GraphAndSeries.ColumnDefinitions.Clear();
			GraphAndSeries.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			if (wide) GraphAndSeries.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(300)));
		}
		Grid.SetColumn(SeriesPanel, wide ? 1 : 0);
		Grid.SetRow(SeriesPanel, wide ? 0 : 1);
	}
}
