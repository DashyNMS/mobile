using DashyNMS.Mobile.Rules;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Views;

/// <summary>
/// One alert rule, as its own page (<see cref="Pages.AlertRulePage"/>) or
/// beside the Alert rules list on a larger screen (#88, #122).
/// </summary>
public partial class AlertRuleView : ContentView
{
	public AlertRuleView(AlertRuleViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = ViewModel = viewModel;
		Controls.PullToRefresh.ShowBusyCard(PullRefresh, Busy, viewModel, "Loading the rule…", "Refreshing the rule…");
	}

	public AlertRuleViewModel ViewModel { get; }

	/// <summary>"‹ Alert rules"; beside the list, its back button is hidden.</summary>
	public Controls.BackBar Bar => TopBar;

	/// <summary>Loads the rule <see cref="Routes.AlertRule"/>'s parameters name.</summary>
	public void Load(IDictionary<string, object>? parameters)
	{
		if (parameters?.TryGetValue(Routes.RuleIdParameter, out var id) == true && id is int ruleId)
		{
			ViewModel.RuleId = ruleId;
			ViewModel.RefreshCommand.Execute(null);
		}
	}
}
