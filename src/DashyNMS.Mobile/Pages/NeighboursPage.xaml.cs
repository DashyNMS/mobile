using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Neighbours"/>, from the Devices tab.</summary>
public partial class NeighboursPage : ContentPage
{
	public NeighboursPage(NeighboursViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		viewModel.RefreshCommand.Execute(null);
	}
}
