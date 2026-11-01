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
#if IOS
		// The page or pane showing it keeps the column clear of the camera
		// and the glass (PlaceContent); left to themselves these would add
		// the safe area again, stopping the cards short of the screen's foot.
		var none = new SafeAreaEdges(SafeAreaRegions.None);
		Refresh.SafeAreaEdges = none;
		Scroller.SafeAreaEdges = none;
		Column.SafeAreaEdges = none;
#endif
	}

	public DeviceDetailViewModel ViewModel { get; }

	/// <summary>"‹ Devices" and the pin; beside the list, its back button is hidden.</summary>
	public Controls.BackBar Bar => TopBar;

	/// <summary>
	/// Back and the pin are glass by the tab bar (iOS 26 and later), so the
	/// top bar goes and the device has the whole height.
	/// </summary>
	public void UseGlass() => TopBar.IsVisible = false;

	/// <summary>
	/// Pads the cards' column clear of <paramref name="clear"/> - the camera,
	/// a side tab bar's column, and at the foot a bottom tab bar and its
	/// glass - so they scroll on underneath to the screen's edge and the last
	/// one still comes clear.
	/// </summary>
	public void PlaceContent(Thickness clear)
	{
		var padding = new Thickness(16 + clear.Left, TopBar.IsVisible ? 0 : 8, 16 + clear.Right, 16 + clear.Bottom);
		if (Column.Padding != padding)
		{
			Column.Padding = padding;
		}
	}

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
