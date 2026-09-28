using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Thresholds"/>, from Settings.</summary>
public partial class ThresholdsPage : ContentPage
{
	public ThresholdsPage(ThresholdsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
