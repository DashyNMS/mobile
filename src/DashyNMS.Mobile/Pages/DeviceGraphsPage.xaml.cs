using System.ComponentModel;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.DeviceGraphs"/>, with the device id and name.</summary>
public partial class DeviceGraphsPage : ContentPage, IQueryAttributable
{
	/// <summary>LibreNMS's graphs are drawn at 800x400, so half as tall as wide, plus the legend.</summary>
	private const double AspectRatio = 0.62;

	private readonly DeviceGraphsViewModel _viewModel;

	public DeviceGraphsPage(DeviceGraphsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_viewModel.DarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
		_viewModel.PropertyChanged += OnViewModelChanged;
		GraphView.SizeChanged += (_, _) => FitHeight();

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
				_ = _viewModel.LoadPortAsync(deviceId, ifName, portName as string, name as string);
			}
			else
			{
				_ = _viewModel.LoadAsync(deviceId, name as string);
			}
		}
	}

	private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(DeviceGraphsViewModel.GraphPage))
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
			GraphView.HeightRequest = GraphView.Width * AspectRatio;
		}
	}
}
