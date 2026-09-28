using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.AlertDetail"/>, with the alert id (and device id, from a notification).</summary>
public partial class AlertDetailPage : ContentPage, IQueryAttributable
{
	private readonly AlertDetailViewModel _viewModel;

	public AlertDetailPage(AlertDetailViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.AlertIdParameter, out var id) && id is int alertId)
		{
			int? deviceId = query.TryGetValue(Routes.DeviceIdParameter, out var device) && device is int value ? value : null;
			_ = _viewModel.LoadAsync(alertId, deviceId);
		}
	}
}
