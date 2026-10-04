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

		// Also picks up Edit dashboard's changes, and the phone's theme for the graph.
		_viewModel.DarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
		_viewModel.RefreshCommand.Execute(null);

		// A card just added lands at the bottom: brought into view, outlined (#140).
		Dispatcher.Dispatch(() => _ = ShowHighlightedAsync());
	}

	private async Task ShowHighlightedAsync()
	{
		if (CardList.Children.OfType<View>().FirstOrDefault(v => v.BindingContext is DashboardCard { IsHighlighted: true }) is { } card)
		{
			await Scroller.ScrollToAsync(card, ScrollToPosition.Center, animated: true);
		}
	}
}
