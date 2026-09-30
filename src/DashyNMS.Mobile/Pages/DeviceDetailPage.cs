using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Views;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.DeviceDetail"/>, with the device id as a
/// query attribute. The device itself is <see cref="DeviceDetailView"/>,
/// which the Devices list also shows beside itself on a larger screen (#88).
/// </summary>
public sealed class DeviceDetailPage : ContentPage, IQueryAttributable
{
	private readonly DeviceDetailView _view;

	public DeviceDetailPage(DeviceDetailViewModel viewModel)
	{
		BindingContext = viewModel;
		SetBinding(TitleProperty, new Binding(nameof(DeviceDetailViewModel.Title)));
		On<iOS>().SetUseSafeArea(true);
		Content = _view = new DeviceDetailView(viewModel);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_view.Bar.Apply(this);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);
}
