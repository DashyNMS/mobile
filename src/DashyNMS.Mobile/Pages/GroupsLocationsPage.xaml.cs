using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.GroupsLocations"/>, from the Devices tab.</summary>
public partial class GroupsLocationsPage : ContentPage
{
	private readonly GroupsLocationsViewModel _viewModel;
	private bool _loaded;

	public GroupsLocationsPage(GroupsLocationsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
		if (!_loaded)
		{
			_loaded = true;
			_viewModel.RefreshCommand.Execute(null);
		}
	}
}
