using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

public partial class SettingsPage : ContentPage
{
	private readonly SettingsViewModel _viewModel;
	private readonly SecretTaps _secretTaps = new(TimeProvider.System);

	public SettingsPage(SettingsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		Header.TitleTapped += OnSecretTap;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
		_viewModel.Refresh();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		Penguins.Dismiss();
	}

	/// <summary>
	/// Three quick taps on the title, or on the version line when the title
	/// is the navigation bar's: smile and wave (#99) - desktop's three
	/// Shift+clicks on its About logo. A light tick on the third, on iPhone.
	/// </summary>
	private void OnSecretTap(object? sender, EventArgs e)
	{
		try
		{
			if (!_secretTaps.Tap())
			{
				return;
			}

#if IOS
			// Android's haptics need the vibrate permission, which an easter egg doesn't justify.
			HapticFeedback.Default.Perform(HapticFeedbackType.Click);
#endif
			Penguins.Play();
		}
		catch (Exception)
		{
			// Cosmetic only - never let it affect the page.
		}
	}
}
