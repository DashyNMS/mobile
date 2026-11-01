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
		Root.SafeAreaEdges = none;
		Refresh.SafeAreaEdges = none;
		Scroller.SafeAreaEdges = none;
		Column.SafeAreaEdges = none;
#endif
		PropertyRows.SizeChanged += (_, _) => FitPropertyNames();
		PropertyRows.ChildAdded += (_, _) => FitPropertyNames();
	}

	public DeviceDetailViewModel ViewModel { get; }

	/// <summary>"‹ Devices" and the pin; beside the list, its back button is hidden.</summary>
	public Controls.BackBar Bar => TopBar;

	/// <summary>
	/// Back and the pin are glass by the tab bar (iOS 26 and later), so the
	/// top bar goes and the device has the whole height, the cards fading
	/// out as they scroll up under the status bar.
	/// </summary>
	public void UseGlass()
	{
		TopBar.IsVisible = false;
		TopFade.IsVisible = true;
	}

	/// <summary>
	/// Pads the cards' column clear of <paramref name="clear"/> - the camera,
	/// a side tab bar's column, and at the foot a bottom tab bar and its
	/// glass - so they scroll on underneath to the screen's edge and the last
	/// one still comes clear.
	/// </summary>
	public void PlaceContent(Thickness clear)
	{
		// Without the top bar, the header card keeps the bar's own gap below
		// the status bar: at 8 it sat almost against it (#344).
		var padding = new Thickness(16 + clear.Left, TopBar.IsVisible ? 0 : Controls.TopBar.Offset, 16 + clear.Right, 24 + clear.Bottom);
		if (Column.Padding != padding)
		{
			Column.Padding = padding;
		}
	}

	/// <summary>
	/// Sizes the Device card's names column to about a third of the card,
	/// within limits: a fixed 130 left a small phone's values a narrow strip
	/// to wrap in, and a tablet's a long way from their names (#344). Every
	/// row gets the same width, so the values still line up.
	/// </summary>
	private void FitPropertyNames()
	{
		if (PropertyRows.Width <= 0)
		{
			return;
		}

		var names = new GridLength(Math.Clamp(Math.Round(PropertyRows.Width * 0.36), 96, 160));
		foreach (var row in PropertyRows.Children.OfType<VerticalStackLayout>().SelectMany(r => r.Children.OfType<Grid>()))
		{
			if (row.ColumnDefinitions.Count > 0 && row.ColumnDefinitions[0].Width != names)
			{
				row.ColumnDefinitions[0].Width = names;
			}
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
