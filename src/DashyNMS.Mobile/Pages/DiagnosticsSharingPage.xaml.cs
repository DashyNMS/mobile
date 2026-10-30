using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.DiagnosticsSharing"/>: once after sign-in, until the user chooses (#216).</summary>
public partial class DiagnosticsSharingPage : ContentPage
{
	public DiagnosticsSharingPage(DiagnosticsSharingViewModel viewModel)
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
