using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

public partial class AlertsPage : ContentPage
{
	private readonly AlertsViewModel _viewModel;

	public AlertsPage(AlertsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.RefreshCommand.Execute(null);
	}
}
