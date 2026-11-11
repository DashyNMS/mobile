using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.IntelligenceCheck"/>, from Settings › Diagnostics (#570).
/// Measurements remain available after enabling Intelligence in Settings.
/// </summary>
public partial class IntelligenceCheckPage : ContentPage, IActivePage
{
	private readonly IntelligenceCheckViewModel _viewModel;
	private readonly SetupPageLayout _layout;

	public IntelligenceCheckPage(IntelligenceCheckViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_layout = new SetupPageLayout(this, Setup, Header, FormScroll);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.Refresh();
	}

	public void Activate()
	{
		_viewModel.Refresh();
		_layout.Activate();
	}

	/// <summary>An answer nobody is watching shouldn't keep the model running.</summary>
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (_viewModel.StopCommand.CanExecute(null))
			_viewModel.StopCommand.Execute(null);
	}
}
