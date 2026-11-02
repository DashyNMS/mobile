using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// One Settings section (#67), shown via <see cref="Routes.SettingsSection"/>
/// with the section as a query attribute. It shares Settings' view model, so
/// the summaries there are current when the user comes back.
/// </summary>
public partial class SettingsSectionPage : ContentPage, IQueryAttributable
{
	public SettingsSectionPage(SettingsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		_ = new SetupPageLayout(this, Setup, Header, FormScroll);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.SettingsSectionParameter, out var value) && value is SettingsSection section)
		{
			Title = Header.Title = SettingsViewModel.Title(section);
			Server.IsVisible = section == SettingsSection.Server;
			Appearance.IsVisible = section == SettingsSection.Appearance;
			Devices.IsVisible = section == SettingsSection.Devices;
			AlertChecks.IsVisible = section == SettingsSection.AlertChecks;
			Notifications.IsVisible = section == SettingsSection.Notifications;
			LockScreen.IsVisible = section == SettingsSection.LockScreen;
			Diagnostics.IsVisible = section == SettingsSection.Diagnostics;
		}
	}
}
