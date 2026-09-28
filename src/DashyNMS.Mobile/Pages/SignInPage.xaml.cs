using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

public partial class SignInPage : ContentPage
{
	private readonly SignInViewModel _viewModel;

	public SignInPage(SignInViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.AppearingCommand.Execute(null);
	}
}
