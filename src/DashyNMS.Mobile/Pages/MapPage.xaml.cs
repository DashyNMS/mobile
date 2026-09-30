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

		// A pin tap arrives as a dashynms-map:// navigation; nothing else may
		// take the page anywhere - tiles are images, not navigations.
		MapView.Navigating += (_, e) =>
		{
			if (MapHtml.PinsFrom(e.Url) is { } pins)
			{
				e.Cancel = true;
				MainThread.BeginInvokeOnMainThread(() => _viewModel.SelectPins(pins));
			}
			else if (!IsOwnPage(e.Url))
			{
				e.Cancel = true;
			}
		};
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
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
			MapView.Source = _viewModel.MapPage is { } html ? new HtmlWebViewSource { Html = html } : null;
		}
	}

	/// <summary>Loading the page itself can raise Navigating, with a blank, data: or local file: address.</summary>
	private static bool IsOwnPage(string? url) =>
		string.IsNullOrEmpty(url)
		|| url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
		|| url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
		|| url.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
}
