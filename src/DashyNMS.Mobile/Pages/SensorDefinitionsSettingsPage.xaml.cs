using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.SensorDefinitionsSettings"/>, from Settings (#209).</summary>
public partial class SensorDefinitionsSettingsPage : ContentPage
{
	private readonly SensorDefinitionsSettingsViewModel _viewModel;

	public SensorDefinitionsSettingsPage(SensorDefinitionsSettingsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>The app's own header, as every page (#142); Core's progress followed while it shows.</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
		_viewModel.Attach();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.Detach();
	}
}
