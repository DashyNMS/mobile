using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

public partial class DashboardPage : ContentPage
{
	private readonly DashboardViewModel _viewModel;

	public DashboardPage(DashboardViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;

		// A pull starts the refresh (the view runs the command); its own
		// spinner is put away at once, as the logo's heartbeat shows the load.
		Refresh.Refreshing += (_, _) => Dispatcher.Dispatch(() => Refresh.IsRefreshing = false);

		// Nor is it seen while pulling: the pull just sets the heartbeat going.
		Refresh.RefreshColor = Colors.Transparent;
		Refresh.HandlerChanged += (_, _) => HideSpinner();
	}

	/// <summary>
	/// iOS's spinner goes with its colour, but Android's sits on a disc with a
	/// shadow, so there it's parked above the top edge instead: the pull still
	/// measures the same distance, the disc just never comes into view.
	/// </summary>
	private void HideSpinner()
	{
#if ANDROID
		if (Refresh.Handler?.PlatformView is AndroidX.SwipeRefreshLayout.Widget.SwipeRefreshLayout layout)
		{
			layout.SetProgressViewOffset(false, -2000, -1000);
		}
#endif
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

		// Also picks up Customise's changes, and the phone's theme for the graph.
		_viewModel.DarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
		_viewModel.RefreshCommand.Execute(null);
	}
}
