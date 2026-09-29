using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.DeviceDetail"/>, with the device id as a query attribute.</summary>
public partial class DeviceDetailPage : ContentPage, IQueryAttributable
{
	private readonly DeviceDetailViewModel _viewModel;

	public DeviceDetailPage(DeviceDetailViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		TopBar.Apply(this);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.DeviceIdParameter, out var value) && value is int deviceId)
		{
			// The ping graph is drawn for the phone's theme, as the dashboard's.
			_viewModel.DarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
			_ = _viewModel.LoadAsync(deviceId);
		}
	}
}
