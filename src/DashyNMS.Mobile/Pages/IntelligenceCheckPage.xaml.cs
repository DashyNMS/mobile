using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.IntelligenceCheck"/>, from Settings › Diagnostics (#570).
/// Temporary: #571's Settings › Intelligence replaces it.
/// </summary>
public partial class IntelligenceCheckPage : ContentPage
{
	private readonly IntelligenceCheckViewModel _viewModel;

	public IntelligenceCheckPage(IntelligenceCheckViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_ = new SetupPageLayout(this, Setup, Header, FormScroll);
	}

	/// <summary>An answer nobody is watching shouldn't keep the model running.</summary>
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (_viewModel.StopCommand.CanExecute(null))
			_viewModel.StopCommand.Execute(null);
	}
}
