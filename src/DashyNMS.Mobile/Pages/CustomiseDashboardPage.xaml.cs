using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.CustomiseDashboard"/>, from the Dashboard.</summary>
public partial class CustomiseDashboardPage : ContentPage
{
	public CustomiseDashboardPage(CustomiseDashboardViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
