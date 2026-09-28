using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Maintenance"/>, with the device id and name.</summary>
public partial class MaintenancePage : ContentPage, IQueryAttributable
{
	private readonly MaintenanceViewModel _viewModel;

	public MaintenancePage(MaintenanceViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.DeviceIdParameter, out var id) && id is int deviceId)
		{
			query.TryGetValue(Routes.DeviceNameParameter, out var name);
			_viewModel.Initialize(deviceId, name as string ?? $"device {deviceId}");
		}
	}
}
