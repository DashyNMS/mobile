using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

public partial class SignInPage : ContentPage
{
	private readonly SignInViewModel _viewModel;

	public SignInPage(SignInViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;

		// Copied text to offer as the token (#161): looked for on focusing the
		// token box, as well as on showing and coming back to the app.
		TokenEntry.Focused += (_, _) => _viewModel.CheckClipboard();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.AppearingCommand.Execute(null);
		_viewModel.CheckClipboard();

		if (Window is { } window)
		{
			window.Resumed += OnResumed;
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (Window is { } window)
		{
			window.Resumed -= OnResumed;
		}
	}

	/// <summary>Back from the browser, where a token was likely just copied.</summary>
	private void OnResumed(object? sender, EventArgs e) => _viewModel.CheckClipboard();
}
