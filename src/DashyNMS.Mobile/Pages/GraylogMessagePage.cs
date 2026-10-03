using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.GraylogMessage"/>, with the message and its
/// list (#117). The message itself is <see cref="GraylogMessageView"/>, which
/// the Graylog list also shows beside itself on a larger screen (#88).
/// </summary>
public sealed class GraylogMessagePage : ContentPage, IQueryAttributable
{
	private readonly GraylogMessageView _view;

	public GraylogMessagePage(GraylogMessageViewModel viewModel)
	{
		BindingContext = viewModel;
		Title = "Message";
		On<iOS>().SetUseSafeArea(true);
		Content = _view = new GraylogMessageView(viewModel);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_view.Bar.Apply(this);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);
}
