using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Neighbours"/>, from the Devices tab.</summary>
public partial class NeighboursPage : ContentPage
{
	private readonly NeighboursViewModel _viewModel;

	public NeighboursPage(NeighboursViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		SearchReveal.Attach(List, SearchSlot, Search);
		viewModel.RefreshCommand.Execute(null);
	}

	/// <summary>Back from the groups: they may have changed (#98).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.GroupsChanged();
	}
}
