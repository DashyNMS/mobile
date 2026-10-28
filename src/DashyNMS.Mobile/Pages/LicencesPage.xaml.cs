using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Licences"/>, from Settings › About (#164).</summary>
public partial class LicencesPage : ContentPage
{
	public LicencesPage(LicencesViewModel viewModel)
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
