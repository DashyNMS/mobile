using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.AddCard"/>, from Edit dashboard or the welcome card (#140).</summary>
public partial class AddCardPage : ContentPage
{
	private readonly AddCardViewModel _viewModel;

	public AddCardPage(AddCardViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>What's on the dashboard may have changed since it was made.</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		TopBar.Apply(this);
		_viewModel.Load();
	}
}
