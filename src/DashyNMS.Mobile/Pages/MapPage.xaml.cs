using System.ComponentModel;
using DashyNMS.Mobile.Map;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Map"/>, from the Devices tab.</summary>
public partial class MapPage : ContentPage
{
	private readonly MapViewModel _viewModel;
	private bool _loaded;

	public MapPage(MapViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_viewModel.PropertyChanged += OnViewModelChanged;

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
		if (!_loaded)
		{
			_loaded = true;
			_viewModel.DarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
			_viewModel.RefreshCommand.Execute(null);
		}
	}

	private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
	{
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
