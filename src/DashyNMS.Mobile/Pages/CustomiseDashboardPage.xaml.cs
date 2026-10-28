using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.CustomiseDashboard"/>, from the Dashboard's pencil.</summary>
public partial class CustomiseDashboardPage : ContentPage
{
	private readonly CustomiseDashboardViewModel _viewModel;

	public CustomiseDashboardPage(CustomiseDashboardViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Back from a card's set-up or the card picker: the cards may have changed (#87, #140).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		TopBar.Apply(this);
		_viewModel.Attach();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.Detach();
	}

	private void OnReorderCompleted(object? sender, EventArgs e) => _viewModel.SaveOrderCommand.Execute(null);
}
