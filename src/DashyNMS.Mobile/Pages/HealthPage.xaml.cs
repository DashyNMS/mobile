using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

public partial class HealthPage : ContentPage
{
	private readonly HealthViewModel _viewModel;

	public HealthPage(HealthViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_ = _viewModel.RefreshIfStaleAsync();
	}
}
