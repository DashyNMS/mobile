using System.ComponentModel;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.DeviceGraphs"/>, with the device id and name.</summary>
public partial class DeviceGraphsPage : ContentPage, IQueryAttributable, IActivePage
{
	private readonly DeviceGraphsViewModel _viewModel;
	private IDisposable? _edges;
	private bool _active;
	private Application? _themeOwner;
#if IOS
	private bool? _glassShowsAll;
#endif

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
		ShowAllButton.Command = _viewModel.ShowAllCommand;
#if IOS
		ShowAllButton.IsVisible = !TabBarButtons.IsAvailable;
#endif

		// The page is only ever our own graph page: never follow a link or
		// redirect out of it, whatever the (untrusted) graph contains.
		GraphView.Navigating += (_, e) =>
		{
			if (!IsOwnPage(e.Url))
			{
				e.Cancel = true;
			}
		};

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
		if (e.PropertyName == nameof(DeviceGraphsViewModel.SomeHidden))
		{
#if IOS
			ShowGlass();
#endif
		}
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
		Activate();
	}

	/// <summary>Restores controls on retained return without fetching or changing selection.</summary>
	public void Activate()
	{
		_active = true;
		Header.Apply(this);
		_edges ??= CoveredEdges.Watch(this, PlaceControls);
		if (_themeOwner is null && Application.Current is { } app)
		{
			_themeOwner = app;
			app.RequestedThemeChanged += OnThemeChanged;
		}
		UpdateTheme();
#if IOS
		TabBarButtons.Moved -= OnTabBarButtonsMoved;
		TabBarButtons.Moved += OnTabBarButtonsMoved;
		ShowGlass(force: true);
		StopInsetAdjustment();
#endif
		PlaceControls();
	}

	private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => UpdateTheme();

	private void UpdateTheme()
	{
		var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
		if (_viewModel.DarkTheme == dark) return;
		_viewModel.DarkTheme = dark;
		_viewModel.RedrawTheme();
	}

	protected override void OnDisappearing()
	{
		_active = false;
		_edges?.Dispose();
		_edges = null;
		if (_themeOwner is { } app) app.RequestedThemeChanged -= OnThemeChanged;
		_themeOwner = null;
		base.OnDisappearing();
#if IOS
		TabBarButtons.Moved -= OnTabBarButtonsMoved;
		TabBarButtons.Hide(this);
		_glassShowsAll = null;
#endif
	}

#if IOS
	private void ShowGlass(bool force = false)
	{
		if (!_active || !TabBarButtons.IsAvailable) return;
		var showAll = _viewModel.SomeHidden;
		if (!force && _glassShowsAll == showAll) return;
		_glassShowsAll = showAll;
		List<GlassAction> actions =
		[
			new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
				() => { if (_active) _ = Shell.Current.GoToAsync(".."); }, IsBack: true),
		];
		if (showAll) actions.Add(new("eye", "Show all graph series",
			() => { if (_active) _viewModel.ShowAllCommand.Execute(null); }));
		TabBarButtons.Show(this, null, string.Empty, actions);
	}
#endif

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
		if (!_active || Shell.Current?.CurrentPage != this || Width <= 0) return;
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
