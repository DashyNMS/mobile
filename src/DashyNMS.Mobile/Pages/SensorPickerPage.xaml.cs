using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.PickSensors"/>.</summary>
public partial class SensorPickerPage : ContentPage
{
	public SensorPickerPage(SensorPickerViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		viewModel.LoadCommand.Execute(null);
	}
}
