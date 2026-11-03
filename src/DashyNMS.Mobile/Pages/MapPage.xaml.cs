using System.ComponentModel;
using System.Globalization;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Map;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Map"/>, from a pinned tab or More.</summary>
public partial class MapPage : ContentPage
{
	private readonly MapViewModel _viewModel;
	private bool _loaded;
	private IDisposable? _edges;
    private bool _visible;
    private bool? _chooserShown;
    private long _transition;
    private bool _placing;
    private Task _mapScript = Task.CompletedTask;

	public MapPage(MapViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_viewModel.PropertyChanged += OnViewModelChanged;

		LocationsButton.Command = _viewModel.ChooseLocationCommand;
        MapView.Navigated += (_, _) => { PlaceControls(); SyncSelection(); };
        Canvas.SizeChanged += (_, _) => PlaceControls();
        _viewModel.RefreshCommand.CanExecuteChanged += (_, _) => Dispatcher.Dispatch(UpdateActions);
        Header.SizeChanged += (_, _) => PlaceControls();
        Status.SizeChanged += (_, _) => PlaceControls();
        SelectionCard.SizeChanged += (_, _) => PlaceControls();
        CloseSelection.SizeChanged += (_, _) => PlaceControls();
        SelectionHeading.SizeChanged += (_, _) => PlaceControls();
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        Header.BackInGlass = TabBarButtons.IsAvailable;
        RefreshButton.IsVisible = LocationsButton.IsVisible = !TabBarButtons.IsAvailable;
        DeviceList.HandlerChanged += (_, _) => StopInsets();
        DeviceList.Loaded += (_, _) => StopInsets();
#endif

        // OpenStreetMap's tile policy asks apps to name themselves (#164).
		MapView.UserAgent = MapHtml.UserAgent(AppInfo.Current.VersionString);

		// A pin tap arrives as a dashynms-map:// navigation, and the
		// attribution's links open in the browser; nothing else may take the
		// page anywhere - tiles are images, not navigations.
		MapView.Navigating += (_, e) =>
		{
			if (MapHtml.PinsFrom(e.Url) is { } pins)
			{
				e.Cancel = true;
				MainThread.BeginInvokeOnMainThread(() => _viewModel.SelectPins(pins));
			}
			else if (IsOwnPage(e.Url))
			{
				return;
			}
			else if (MapHtml.ExternalLink(e.Url) is { } link)
			{
				e.Cancel = true;
				MainThread.BeginInvokeOnMainThread(async () => await Browser.Default.OpenAsync(link, BrowserLaunchMode.SystemPreferred));
			}
			else
			{
				e.Cancel = true;
			}
		};
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
        _visible = true;
        _chooserShown = null;
        UpdateActions();
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
#endif
        PlaceControls();
		if (!_loaded)
		{
			_loaded = true;
			_viewModel.DarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
			_viewModel.RefreshCommand.Execute(null);
		}
	}

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _visible = false;
        _edges?.Dispose();
        _edges = null;
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
    }

    private void OnCloseSelection(object? sender, EventArgs e) => AnimateSelection(false);

    private void AnimateSelection(bool show)
    {
        const string name = "MapSelection";
        var transition = ++_transition;
        var selection = _viewModel.SelectionVersion;
        if (show && SelectionCard.IsVisible && !SelectionCard.AnimationIsRunning(name) && SelectionCard.TranslationY == 0) return;
        SelectionCard.AbortAnimation(name);
        if (!show) SendSelection(false);
        double Travel() => (SelectionCard.Height > 0 ? SelectionCard.Height : Math.Max(SelectionCard.HeightRequest, 240)) + SelectionCard.Margin.Bottom + 8;
        if (show && !SelectionCard.IsVisible)
        {
            SelectionCard.Opacity = 0;
            SelectionCard.TranslationY = Travel();
            SelectionCard.IsVisible = true;
        }
        void Finish()
        {
            if (transition != _transition || selection != _viewModel.SelectionVersion) return;
            if (!show)
            {
                SelectionCard.IsVisible = false;
                _viewModel.ClearSelectionIfCurrent(selection);
                // Queue empty IDs now, before PlaceControls, even if notification dispatch is deferred.
                SendSelection(false);
            }
            SelectionCard.TranslationY = 0;
            SelectionCard.Opacity = 1;
            PlaceControls();
        }
        if (ReducedMotion.IsOn) { Finish(); return; }
        Dispatcher.Dispatch(() =>
        {
            if (transition != _transition) return;
            var fromY = show && SelectionCard.Opacity == 0 ? Travel() : SelectionCard.TranslationY;
            var fromOpacity = SelectionCard.Opacity;
            var toY = show ? 0 : Travel();
            SelectionCard.Animate(name, progress =>
            {
                SelectionCard.TranslationY = fromY + (toY - fromY) * progress;
                SelectionCard.Opacity = fromOpacity + ((show ? 1 : 0) - fromOpacity) * progress;
            }, length: 250, easing: show ? Easing.CubicOut : Easing.CubicIn,
            finished: (_, cancelled) => { if (!cancelled) Finish(); });
        });
    }

    private void PlaceControls()
    {
        if (_placing || Canvas.Width <= 0 || Canvas.Height <= 0) return;
        _placing = true;
        try
        {
            var clear = CoveredEdges.ForScrolling(this);
            Overlay.Margin = new Thickness(0, 0, 0, 12 + clear.Bottom);
            Header.Padding = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
            Status.Padding = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
            var foot = 12 + clear.Bottom;
#if IOS
            if (Handler?.PlatformView is UIKit.UIView page && TabBarButtons.PillIn(page) is { } pill && pill.Height > pill.Width)
                foot = Math.Max(0, page.Bounds.Height - pill.Bottom);
#endif
            SelectionCard.Margin = new Thickness(12 + clear.Left, 6, 12 + clear.Right, foot);
            var width = Math.Max(1, Canvas.Width - SelectionCard.Margin.HorizontalThickness - SelectionCard.Padding.HorizontalThickness - 2);
            var close = ((IView)CloseSelection).Measure(width, double.PositiveInfinity).Height;
            var title = ((IView)SelectionHeading).Measure(width, double.PositiveInfinity).Height;
            var padding = SelectionCard.Padding.VerticalThickness + 2;
            // Close stays outside the scrolling title and list, including at huge text sizes.
            var minimum = close + 44 + padding;
            var available = Math.Max(minimum, Canvas.Height - Header.Height - Status.Height - foot - 110);
            var cap = Math.Max(minimum, Math.Min(available, Canvas.Height < 740 ? 200 : 300));
            var titleHeight = Math.Min(title, Math.Max(44, (cap - close - padding) / 2));
            HeadingScroll.HeightRequest = titleHeight;
            var listCap = Math.Max(0, cap - close - titleHeight - padding);
            double natural = 0;
            foreach (var device in _viewModel.Devices)
            {
                var row = (View)DeviceList.ItemTemplate.CreateContent();
                row.BindingContext = device;
                RowMeasure.Content = row;
                natural += Math.Max(44, ((IView)row).Measure(width, double.PositiveInfinity).Height);
                if (natural >= listCap) break;
            }
            RowMeasure.Content = null;
            DeviceList.HeightRequest = Math.Min(listCap, natural);
            SelectionCard.HeightRequest = -1;
            UpdateMapClearance(clear, foot);
        }
        finally { _placing = false; }
    }

    private void SyncSelection() => SendSelection(true);

    private void SendSelection(bool dim, bool focus = false)
    {
        var ids = System.Text.Json.JsonSerializer.Serialize(_viewModel.SelectedPinIds);
        QueueScript($"window.setSelection && window.setSelection({ids}, {(ReducedMotion.IsOn ? "true" : "false")}, {(dim ? "true" : "false")}, {(focus ? "true" : "false")})");
    }

    private void UpdateActions()
    {
        LocationsButton.IsEnabled = _viewModel.CanChooseLocation;
#if IOS
        if (!_visible || _chooserShown == _viewModel.CanChooseLocation) return;
        _chooserShown = _viewModel.CanChooseLocation;
        List<GlassAction> actions = [];
        if (PageHeader.IsPushed(this))
            actions.Add(new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => _ = Shell.Current.GoToAsync(".."), IsBack: true));
        if (_viewModel.CanChooseLocation)
            actions.Add(new("mappin", "Choose a map location", () => _viewModel.ChooseLocationCommand.Execute(null)));
        actions.Add(new("arrow.clockwise", "Refresh the map", () => _viewModel.RefreshCommand.Execute(null)));
        TabBarButtons.Show(this, null, string.Empty, actions);
#endif
    }

    // Keep controls and the selected camera target inside the uncovered canvas.
    private void UpdateMapClearance(Thickness clear, double foot)
    {
        if (!_loaded) return;
        var top = Math.Max(0, Header.Height + Status.Height + 20);
        var bottom = SelectionCard.IsVisible ? Math.Max(0, SelectionCard.Height) + foot + 12 : foot;
        var args = string.Join(",", new[] { top, clear.Right + 12, bottom, clear.Left + 12 }
            .Select(n => Math.Max(0, n).ToString(CultureInfo.InvariantCulture)));
        QueueScript($"window.setMapClearance && window.setMapClearance({args})");
    }

    // Serialise bridge updates so empty selection precedes the closing clearance.
    private void QueueScript(string script) => _mapScript = EvaluateAfterAsync(_mapScript, script);

    private async Task EvaluateAfterAsync(Task previous, string script)
    {
        await previous;
        try { await MapView.EvaluateJavaScriptAsync(script); }
        catch (Exception) { /* Navigated reapplies state after document replacement. */ }
    }

#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);
    private void StopInsets()
    {
        if (DeviceList.Handler?.PlatformView is UIKit.UIView view && FindScroll(view) is { } scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }
    private static UIKit.UIScrollView? FindScroll(UIKit.UIView view) =>
        view as UIKit.UIScrollView ?? view.Subviews.Select(FindScroll).FirstOrDefault(found => found is not null);
#endif

	private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
	{
        if (e.PropertyName is nameof(MapViewModel.CanChooseLocation) or nameof(MapViewModel.IsBusy)) Dispatcher.Dispatch(UpdateActions);
        if (e.PropertyName == nameof(MapViewModel.SelectedPinIds)) Dispatcher.Dispatch(() =>
        {
            if (!_viewModel.HasSelection)
            {
                SendSelection(false);
                SelectionCard.IsVisible = false;
            }
            else AnimateSelection(true);
            PlaceControls();
            if (_viewModel.HasSelection) SendSelection(true, focus: true);
        });
		if (e.PropertyName == nameof(MapViewModel.MapPage))
		{
			// Loaded as the website's address, so tile requests carry a Referer (#164).
			MapView.Source = _viewModel.MapPage is { } html ? new HtmlWebViewSource { Html = html, BaseUrl = MapHtml.BaseUrl.AbsoluteUri } : null;
		}
	}

	/// <summary>
	/// Loading the page itself can raise Navigating, with a blank, data: or
	/// local file: address - or its base address.
	/// </summary>
	private static bool IsOwnPage(string? url) =>
		string.IsNullOrEmpty(url)
		|| url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
		|| url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
		|| url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
		|| (Uri.TryCreate(url, UriKind.Absolute, out var uri)
			&& Uri.Compare(uri, MapHtml.BaseUrl, UriComponents.HttpRequestUrl, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0);
}
