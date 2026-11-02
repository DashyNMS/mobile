using System.ComponentModel;
using DashyNMS.Mobile.Controls;
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
		_ = new SetupPageLayout(this, Setup, Header, FormScroll);
	}

	/// <summary>The app's own header, as every page (#142); Core's progress followed while it shows.</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.PropertyChanged += OnViewModelChanged;
		_viewModel.Attach();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.Detach();
		_viewModel.PropertyChanged -= OnViewModelChanged;
	}

	private async void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(_viewModel.ErrorMessage) && _viewModel.HasError)
		{
			await FormScroll.ScrollToAsync(0, 0, false);
			SemanticScreenReader.Default.Announce(_viewModel.ErrorMessage!);
		}
	}
}
