using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;

namespace DashyNMS.Mobile.Views;

/// <summary>
/// One neighbour link, as its own page (<see cref="Pages.NeighbourLinkPage"/>)
/// or beside the Neighbours list on a larger screen (#88, #121).
/// </summary>
public partial class NeighbourLinkView : ContentView
{
	public NeighbourLinkView(NeighbourLinkViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = ViewModel = viewModel;
	}

	public NeighbourLinkViewModel ViewModel { get; }

	/// <summary>"‹ Neighbours"; beside the list, its back button is hidden.</summary>
	public Controls.BackBar Bar => TopBar;

	/// <summary>Shows the link <see cref="Routes.NeighbourLink"/>'s parameters carry.</summary>
	public void Load(IDictionary<string, object>? parameters)
	{
		if (parameters?.TryGetValue(Routes.NeighbourLinkParameter, out var value) == true && value is NeighbourLink link)
		{
			ViewModel.Load(link);
		}
	}
}
