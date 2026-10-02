using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

public partial class DashboardPage : ContentPage
{
	private readonly DashboardViewModel _viewModel;

	public DashboardPage(DashboardViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;

		// A pull starts the refresh, with no spinner: the logo's heartbeat shows the load.
		Controls.PullToRefresh.HideSpinner(Refresh);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

		// Also picks up Customise's changes, and the phone's theme for the graph.
		_viewModel.DarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
		_viewModel.RefreshCommand.Execute(null);
	}
}
