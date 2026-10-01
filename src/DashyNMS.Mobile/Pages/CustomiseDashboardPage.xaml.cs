using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.CustomiseDashboard"/>, from the Dashboard.</summary>
public partial class CustomiseDashboardPage : ContentPage
{
	private readonly CustomiseDashboardViewModel _viewModel;

	public CustomiseDashboardPage(CustomiseDashboardViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Back from a card's set-up: its title may be new (#87).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.Refresh();
	}
}
