using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Alerts;
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
#if IOS
		// The page or pane showing it keeps the column clear of the camera
		// and the glass (PlaceContent); left to themselves these would add
		// the safe area again, stopping the cards short of the screen's foot.
		var none = new SafeAreaEdges(SafeAreaRegions.None);
		Root.SafeAreaEdges = none;
		Refresh.SafeAreaEdges = none;
		Scroller.SafeAreaEdges = none;
		Readable.SafeAreaEdges = none;
		Column.SafeAreaEdges = none;
#endif
	}

	public AlertDetailViewModel ViewModel { get; }

	/// <summary>"‹ Alerts"; beside the list, its back button is hidden.</summary>
	public Controls.BackBar Bar => TopBar;

	/// <summary>
	/// Pads the cards' column clear of <paramref name="clear"/> - the camera,
	/// a side tab bar's column, and at the foot a bottom tab bar and its
	/// glass - so they scroll on underneath to the screen's edge and the last
	/// one still comes clear.
	/// </summary>
	public void PlaceContent(Thickness clear)
	{
		var padding = new Thickness(16 + clear.Left, TopBar.IsVisible ? 0 : 8, 16 + clear.Right, 24 + clear.Bottom);
		if (Column.Padding != padding)
		{
			Column.Padding = padding;
		}
	}

	/// <summary>Loads the alert <see cref="Routes.AlertDetail"/>'s parameters name (and its device, from a notification).</summary>
	public void Load(IDictionary<string, object>? parameters)
	{
		if (parameters?.TryGetValue(Routes.AlertKeyParameter, out var id) == true && AlertTargets.ParseKey(id.ToString()) is { } key)
		{
			int? deviceId = parameters.TryGetValue(Routes.DeviceIdParameter, out var device) && device is int value ? value : null;
			_ = ViewModel.LoadAsync(key, deviceId);
		}
        else
        {
            _ = ViewModel.OpenMovedAlertListAsync();
        }
	}
}
