using System.ComponentModel;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.GraylogSettings"/>, from Settings or an unset-up Graylog page.</summary>
public partial class GraylogSettingsPage : ContentPage
{
	private readonly GraylogSettingsViewModel _viewModel;

	public GraylogSettingsPage(GraylogSettingsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_ = new SetupPageLayout(this, Setup, Header, Form);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.PropertyChanged -= OnViewModelChanged;
		_viewModel.PropertyChanged += OnViewModelChanged;
	}

	protected override void OnDisappearing()
	{
		_viewModel.PropertyChanged -= OnViewModelChanged;
		base.OnDisappearing();
	}

	/// <summary>Fixed Save can be far from validation feedback, so bring failures into view.</summary>
	private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(GraylogSettingsViewModel.ErrorMessage) || !_viewModel.HasError)
			return;

		var message = _viewModel.ErrorMessage;
		if (string.IsNullOrEmpty(message)) return;
		Dispatcher.Dispatch(async () =>
		{
			if (_viewModel.ErrorMessage != message) return;
			await Form.ScrollToAsync(SaveError, ScrollToPosition.Start, false);
			if (_viewModel.ErrorMessage == message)
				SemanticScreenReader.Default.Announce(message);
		});
	}
}
