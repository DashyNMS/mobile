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
    private Thickness _clear;
	public AlertDetailView(AlertDetailViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = ViewModel = viewModel;
        // Detail has writes and can be reused for a new target after its first load.
        Controls.PullToRefresh.HideSpinner(Refresh);
        void UpdateBusy()
        {
            Busy.Text = viewModel.ActionBusyText ?? (viewModel.HasAlert ? "Updating the alert…" : "Loading the alert…");
            Busy.IsVisible = viewModel.IsBusy || viewModel.ActionBusyText is not null;
        }
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(viewModel.IsBusy) or nameof(viewModel.HasAlert) or nameof(viewModel.ActionBusyText)) UpdateBusy();
        };
        UpdateBusy();
        UndoOverlay.SizeChanged += (_, _) => PlaceContent(_clear);
        UndoOverlay.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsVisible)) PlaceContent(_clear); };
#if IOS
        Scroller.HandlerChanged += (_, _) => StopInsets();
        Scroller.Loaded += (_, _) => StopInsets();
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
        _clear = clear;
#if IOS
        StopInsets();
#endif
        UndoOverlay.Margin = new Thickness(12 + clear.Left, 0, 12 + clear.Right, 16 + clear.Bottom);
        var overlay = UndoOverlay.IsVisible ? Math.Max(0, UndoOverlay.Height) + 28 : 0;
        Fade.IsVisible = !TopBar.IsVisible;
        Fade.Margin = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
		var padding = new Thickness(16 + clear.Left, TopBar.IsVisible ? 0 : 20, 16 + clear.Right, 24 + clear.Bottom + overlay);
		if (Column.Padding != padding)
		{
			Column.Padding = padding;
		}
	}
#if IOS
    private void StopInsets()
    {
        if (Scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }
#endif

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
