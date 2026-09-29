using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>The More tab: every page, and which are pinned to the tab bar (#68).</summary>
public partial class MorePage : ContentPage
{
	private readonly MoreViewModel _viewModel;

	public MorePage(MoreViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.Update();
	}
}
