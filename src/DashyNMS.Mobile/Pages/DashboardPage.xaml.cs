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
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

		// Also picks up Customise's changes, and the phone's theme for the graph.
		_viewModel.DarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
		_viewModel.RefreshCommand.Execute(null);
	}
}
