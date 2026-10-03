using DashyNMS.Mobile.Rules;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.AlertRule"/>: one alert rule, read-only (#22).
/// The rule itself is <see cref="AlertRuleView"/>, which the Alert rules list
/// also shows beside itself on a larger screen (#88, #122).
/// </summary>
public sealed class AlertRulePage : ContentPage, IQueryAttributable
{
	private readonly AlertRuleView _view;

	public AlertRulePage(AlertRuleViewModel viewModel)
	{
		BindingContext = viewModel;
		SetBinding(TitleProperty, new Binding(nameof(AlertRuleViewModel.Title)));
		On<iOS>().SetUseSafeArea(true);
		Content = _view = new AlertRuleView(viewModel);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_view.Bar.Apply(this);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);
}
