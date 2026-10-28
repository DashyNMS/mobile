using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Views;

/// <summary>
/// One device, as its own page (<see cref="Pages.DeviceDetailPage"/>) or
/// beside the Devices list on a larger screen (#88).
/// </summary>
public partial class DeviceDetailView : ContentView
{
	public DeviceDetailView(DeviceDetailViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = ViewModel = viewModel;
	}

	public DeviceDetailViewModel ViewModel { get; }

	/// <summary>"‹ Devices" and the pin; beside the list, its back button is hidden.</summary>
	public Controls.BackBar Bar => TopBar;

	/// <summary>Loads the device <see cref="Routes.DeviceDetail"/>'s parameters name.</summary>
	public void Load(IDictionary<string, object>? parameters)
	{
		if (parameters?.TryGetValue(Routes.DeviceIdParameter, out var value) == true && value is int deviceId)
		{
			// The ping graph is drawn for the phone's theme, as the dashboard's.
			ViewModel.DarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
			_ = ViewModel.LoadAsync(deviceId);
		}
	}
}
