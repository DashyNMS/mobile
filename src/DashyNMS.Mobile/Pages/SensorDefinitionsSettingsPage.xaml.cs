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

	/// <summary>Follow catalogue progress only while this page is visible.</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.PropertyChanged -= OnViewModelChanged;
		_viewModel.PropertyChanged += OnViewModelChanged;
		_viewModel.Attach();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.Detach();
		_viewModel.PropertyChanged -= OnViewModelChanged;
	}

	/// <summary>Reveal validation feedback that can be far from the fixed Save.</summary>
	private async void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(_viewModel.ErrorMessage) && _viewModel.HasError)
		{
			var message = _viewModel.ErrorMessage;
			if (string.IsNullOrEmpty(message)) return;
			await FormScroll.ScrollToAsync(0, 0, false);
			if (_viewModel.ErrorMessage == message)
				SemanticScreenReader.Default.Announce(message);
		}
	}
}
