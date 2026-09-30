using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Views;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.AlertDetail"/>, with the alert id (and device
/// id, from a notification). The alert itself is <see cref="AlertDetailView"/>,
/// which the Alerts list also shows beside itself on a larger screen (#88).
/// </summary>
public sealed class AlertDetailPage : ContentPage, IQueryAttributable
{
	private readonly AlertDetailView _view;

	public AlertDetailPage(AlertDetailViewModel viewModel)
	{
		BindingContext = viewModel;
		SetBinding(TitleProperty, static (AlertDetailViewModel vm) => vm.Title);
		On<iOS>().SetUseSafeArea(true);
		Content = _view = new AlertDetailView(viewModel);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_view.Bar.Apply(this);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);
}
