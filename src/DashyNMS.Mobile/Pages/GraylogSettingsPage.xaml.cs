using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.GraylogSettings"/>, from Settings or an unset-up Graylog page.</summary>
public partial class GraylogSettingsPage : ContentPage
{
	public GraylogSettingsPage(GraylogSettingsViewModel viewModel)
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
