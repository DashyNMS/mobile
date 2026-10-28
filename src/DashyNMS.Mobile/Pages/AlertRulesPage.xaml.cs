using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Rules;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Pages;

/// <summary>LibreNMS's alert rules, read-only (#22): from More, or pinned to the tab bar.</summary>
public partial class AlertRulesPage : ContentPage, IDetailHost
{
	private readonly AlertRulesViewModel _viewModel;

	public AlertRulesPage(AlertRulesViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading rules…", "Refreshing rules…");
		SearchReveal.Attach(List, SearchSlot, Search);
	}

	/// <summary>A rule tapped with room beside the list (#88, #122) opens there.</summary>
	public bool TryShowDetail(string route, IDictionary<string, object>? parameters)
	{
		if (!Split.IsSplit || route != Routes.AlertRule || Handler?.MauiContext?.Services is not { } services)
		{
			return false;
		}

		var view = new AlertRuleView(services.GetRequiredService<AlertRuleViewModel>());
		view.Bar.ShowsBack = false;
		view.Load(parameters);
		Split.Detail = view;
		return true;
	}

	// Every time: what's alerting changes while you're away.
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
		_viewModel.RefreshCommand.Execute(null);
	}
}
