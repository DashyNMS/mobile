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

	/// <summary>The app's own header, as every page (#142).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
	}
}
