using DashyNMS.Mobile.Rules;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Views;

/// <summary>
/// One alert rule, as its own page (<see cref="Pages.AlertRulePage"/>) or
/// beside the Alert rules list on a larger screen (#88, #122).
/// </summary>
public partial class AlertRuleView : ContentView
{
    private Thickness _clearance;
	public AlertRuleView(AlertRuleViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = ViewModel = viewModel;
        UndoOverlay.SizeChanged += (_, _) => PlaceOverlay();
        UndoOverlay.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsVisible)) PlaceOverlay(); };
#if IOS
        Scroller.HandlerChanged += (_, _) => StopInsets();
        Scroller.Loaded += (_, _) => StopInsets();
#endif
		Controls.PullToRefresh.ShowBusyCard(PullRefresh, Busy, viewModel, "Loading the rule…", "Refreshing the rule…");
	}

	public AlertRuleViewModel ViewModel { get; }

    /// <summary>The embedded host owns measured occlusion; this view owns scrolling padding.
    /// Pushed pages supply their own covered edges through the same boundary.</summary>
    public void PlaceContent(Thickness clearance)
    {
#if IOS
        StopInsets();
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
        UndoOverlay.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
        Root.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
        ReadingColumn.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
        PullRefresh.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
        Scroller.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
        ContentColumn.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
#endif
        _clearance = clearance;
        PlaceOverlay();
    }

#if IOS
    private void StopInsets()
    {
        if (Scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }
#endif

    private void PlaceOverlay()
    {
        UndoOverlay.Margin = new Thickness(12 + _clearance.Left, 0, 12 + _clearance.Right, 16 + _clearance.Bottom);
        var overlay = UndoOverlay.IsVisible ? Math.Max(0, UndoOverlay.Height) + 28 : 0;
        ContentColumn.Padding = new Thickness(16 + _clearance.Left, 20, 16 + _clearance.Right, 24 + _clearance.Bottom + overlay);
    }

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
