using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.PickGraph"/>.</summary>
public partial class GraphPickerPage : ContentPage
{
	public GraphPickerPage(GraphPickerViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		viewModel.LoadCommand.Execute(null);
	}
}
