using System.ComponentModel;
using System.Globalization;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Map;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Map"/>, from the Devices tab.</summary>
public partial class MapPage : ContentPage
{
	private readonly MapViewModel _viewModel;
	private bool _loaded;
	private IDisposable? _edges;

	public MapPage(MapViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_viewModel.PropertyChanged += OnViewModelChanged;

		LocationsButton.Command = new Command(async () => await ChooseLocationAsync());
        MapView.Navigated += (_, _) => PlaceControls();
        Header.SizeChanged += (_, _) => PlaceControls();
        Status.SizeChanged += (_, _) => PlaceControls();
        SelectionCard.SizeChanged += (_, _) => PlaceControls();
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
#if IOS
        List<GlassAction> actions = [];
        if (PageHeader.IsPushed(this))
            actions.Add(new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => _ = Shell.Current.GoToAsync(".."), IsBack: true));
        actions.Add(new("mappin", "Choose a map location", () => _ = ChooseLocationAsync()));
        actions.Add(new("arrow.clockwise", "Refresh the map", () => _viewModel.RefreshCommand.Execute(null)));
        TabBarButtons.Show(this, null, string.Empty, actions);
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
        SelectionCard.AbortAnimation(name);
        var travel = Math.Max(0, SelectionCard.Height) + SelectionCard.Margin.Bottom + 8;
        void Finish()
        {
            SelectionCard.TranslationY = 0;
            SelectionCard.Opacity = 1;
            if (!show) _viewModel.ClearSelectionCommand.Execute(null);
            PlaceControls();
        }
        if (ReducedMotion.IsOn) { Finish(); return; }
        SelectionCard.Animate(name, progress =>
        {
            SelectionCard.TranslationY = travel * (show ? 1 - progress : progress);
            SelectionCard.Opacity = show ? progress : 1 - progress;
        }, length: 250, easing: show ? Easing.CubicOut : Easing.CubicIn,
        finished: (_, cancelled) => { if (!cancelled) Finish(); });
    }

    private async Task ChooseLocationAsync()
    {
        var locations = _viewModel.Locations;
        if (locations.Count == 0) return;
        var names = locations.Select(p => $"{p.Id + 1}. {p.Name} ({p.Count})").ToArray();
        var chosen = await DisplayActionSheetAsync("Choose a map location", "Cancel", null, names);
        var index = Array.IndexOf(names, chosen);
        if (index >= 0) _viewModel.SelectPin(locations[index].Id);
    }

    private void PlaceControls()
    {
        var clear = CoveredEdges.ForScrolling(this);
        Overlay.Margin = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 12 + clear.Bottom);
        var foot = 12 + clear.Bottom;
#if IOS
        if (Handler?.PlatformView is UIKit.UIView page && TabBarButtons.PillIn(page) is { } pill && pill.Height > pill.Width)
            foot = Math.Max(0, page.Bounds.Height - pill.Bottom);
#endif
        SelectionCard.Margin = new Thickness(12 + clear.Left, 6, 12 + clear.Right, foot);
        var available = Math.Max(0, Canvas.Height - Header.Height - Status.Height - foot - 110);
        var height = Math.Min(available, Math.Max(240, Canvas.Height * 0.6));
        if (SelectionCard.HeightRequest != height) SelectionCard.HeightRequest = height;
        UpdateMapClearance(clear, foot);
    }

    // Move Leaflet's controls, never resize/reload the canvas or reset its camera.
    private async void UpdateMapClearance(Thickness clear, double foot)
    {
        if (!_loaded) return;
        var top = Math.Max(0, Header.Height + Status.Height + 12);
        var bottom = _viewModel.HasSelection ? SelectionCard.Height + foot + 12 : foot;
        var args = string.Join(",", new[] { top, clear.Right + 12, bottom, clear.Left + 12 }
            .Select(n => Math.Max(0, n).ToString(CultureInfo.InvariantCulture)));
        try { await MapView.EvaluateJavaScriptAsync($"window.setMapClearance && window.setMapClearance({args})"); }
        catch (Exception) { /* A replaced document may not yet have its script; Navigated retries. */ }
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
		if (e.PropertyName == nameof(MapViewModel.HasSelection)) Dispatcher.Dispatch(() =>
        {
            if (_viewModel.HasSelection && !SelectionCard.IsVisible && !ReducedMotion.IsOn)
            {
                SelectionCard.Opacity = 0;
                SelectionCard.TranslationY = Canvas.Height;
            }
            SelectionCard.IsVisible = _viewModel.HasSelection;
            PlaceControls();
            if (_viewModel.HasSelection) AnimateSelection(true);
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
