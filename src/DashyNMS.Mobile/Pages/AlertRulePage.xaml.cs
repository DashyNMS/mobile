using DashyNMS.Mobile.Rules;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.AlertRule"/>: one alert rule, read-only (#22).</summary>
public partial class AlertRulePage : ContentPage, IQueryAttributable
{
	private readonly AlertRuleViewModel _viewModel;

	public AlertRulePage(AlertRuleViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		Controls.PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading the rule…", "Refreshing the rule…");
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.RuleIdParameter, out var id) && id is int ruleId)
		{
			_viewModel.RuleId = ruleId;
			_viewModel.RefreshCommand.Execute(null);
		}
	}
}
