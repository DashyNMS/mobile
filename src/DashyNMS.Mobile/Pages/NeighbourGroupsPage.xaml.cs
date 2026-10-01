using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.NeighbourGroups"/>, from Neighbours (#98).</summary>
public partial class NeighbourGroupsPage : ContentPage
{
	private readonly NeighbourGroupsViewModel _viewModel;

	public NeighbourGroupsPage(NeighbourGroupsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Back from the editor: a group may be new, renamed or gone.</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		TopBar.Apply(this);
		_viewModel.Refresh();
	}
}
