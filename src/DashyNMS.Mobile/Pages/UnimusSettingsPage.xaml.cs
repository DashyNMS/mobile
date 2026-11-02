using System.ComponentModel;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Unimus;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.UnimusSettings"/>, from Settings (#21).</summary>
public partial class UnimusSettingsPage : ContentPage
{
	private readonly UnimusSettingsViewModel _viewModel;

	public UnimusSettingsPage(UnimusSettingsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_ = new SetupPageLayout(this, Setup, Header, FormScroll);
	}

	/// <summary>Observe errors only while visible so a failed fixed Save can reveal its feedback.</summary>
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

	private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(UnimusSettingsViewModel.ErrorMessage) || !_viewModel.HasError) return;
		var message = _viewModel.ErrorMessage;
		if (string.IsNullOrEmpty(message)) return;
		Dispatcher.Dispatch(async () =>
		{
			await FormScroll.ScrollToAsync(0, 0, false);
			if (_viewModel.ErrorMessage == message)
				SemanticScreenReader.Default.Announce(message);
		});
	}
}
