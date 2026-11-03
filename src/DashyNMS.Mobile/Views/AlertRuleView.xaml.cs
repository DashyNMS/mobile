using DashyNMS.Mobile.Rules;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Views;

/// <summary>
/// One alert rule, as its own page (<see cref="Pages.AlertRulePage"/>) or
/// beside the Alert rules list on a larger screen (#88, #122).
/// </summary>
public partial class AlertRuleView : ContentView
{
#if IOS
    private bool _embedded;
#endif
	public AlertRuleView(AlertRuleViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = ViewModel = viewModel;
#if IOS
        Scroller.HandlerChanged += (_, _) => StopEmbeddedInsets();
        Scroller.Loaded += (_, _) => StopEmbeddedInsets();
#endif
		Controls.PullToRefresh.ShowBusyCard(PullRefresh, Busy, viewModel, "Loading the rule…", "Refreshing the rule…");
	}

	public AlertRuleViewModel ViewModel { get; }

    /// <summary>The embedded host owns measured occlusion; this view owns scrolling padding.
    /// Pushed wrappers retain their existing native safe areas until #447.</summary>
    public void PlaceContent(Thickness clearance)
    {
#if IOS
        _embedded = true;
        StopEmbeddedInsets();
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
        Root.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
        ReadingColumn.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
        PullRefresh.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
        Scroller.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
        ContentColumn.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
#endif
        ContentColumn.Padding = new Thickness(16 + clearance.Left, 0, 16 + clearance.Right, 24 + clearance.Bottom);
    }

#if IOS
    private void StopEmbeddedInsets()
    {
        if (_embedded && Scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }
#endif

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
