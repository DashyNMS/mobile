using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Views;

/// <summary>
/// One alert, as its own page (<see cref="Pages.AlertDetailPage"/>) or
/// beside the Alerts list on a larger screen (#88).
/// </summary>
public partial class AlertDetailView : ContentView
{
	public AlertDetailView(AlertDetailViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = ViewModel = viewModel;
	}

	public AlertDetailViewModel ViewModel { get; }

	/// <summary>"‹ Alerts"; beside the list, its back button is hidden.</summary>
	public Controls.BackBar Bar => TopBar;

	/// <summary>Loads the alert <see cref="Routes.AlertDetail"/>'s parameters name (and its device, from a notification).</summary>
	public void Load(IDictionary<string, object>? parameters)
	{
		if (parameters?.TryGetValue(Routes.AlertIdParameter, out var id) == true && id is int alertId)
		{
			int? deviceId = parameters.TryGetValue(Routes.DeviceIdParameter, out var device) && device is int value ? value : null;
			_ = ViewModel.LoadAsync(alertId, deviceId);
		}
	}
}
