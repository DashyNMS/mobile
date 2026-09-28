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

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.DeviceIdParameter, out var value) && value is int deviceId)
		{
			_ = _viewModel.LoadAsync(deviceId);
		}
	}
}
