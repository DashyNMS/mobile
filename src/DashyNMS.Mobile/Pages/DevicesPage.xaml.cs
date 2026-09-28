using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

public partial class DevicesPage : ContentPage
{
	private readonly DevicesViewModel _viewModel;

	public DevicesPage(DevicesViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

		// Every time, like the other tabs: after a sign-out and sign-in the
		// page is reused, and a once-only load would keep the old server's list.
		_viewModel.RefreshCommand.Execute(null);
	}
}
