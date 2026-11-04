using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;

namespace DashyNMS.Mobile.Views;

/// <summary>Snapshot detail shared by the pushed page and the Neighbours split host.</summary>
public sealed partial class NeighbourLinkView : ContentView
{
    private Thickness _clearance;

    public NeighbourLinkView(NeighbourLinkViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = ViewModel = viewModel;
        Scroller.HandlerChanged += (_, _) => PlaceContent(_clearance);
        Scroller.Loaded += (_, _) => PlaceContent(_clearance);
    }

    public NeighbourLinkViewModel ViewModel { get; }
    public BackBar Bar => TopBar;

    /// <summary>The host supplies reaching edges; padding belongs to content, never the finite pane.</summary>
    internal void PlaceContent(Thickness clearance)
    {
        _clearance = clearance;
#if IOS
        if (Scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
#endif
        TopBar.IsVisible = TopBar.ShowsBack;
        TopBar.Margin = new Thickness(16 + clearance.Left, 0, 16 + clearance.Right, 0);
        Column.Padding = new Thickness(16 + clearance.Left, 20, 16 + clearance.Right, 24 + clearance.Bottom);
        Fade.Margin = new Thickness(16 + clearance.Left, 0, 16 + clearance.Right, 0);
    }

    public void Load(IDictionary<string, object>? parameters)
    {
        if (parameters?.TryGetValue(Routes.NeighbourLinkParameter, out var value) == true && value is NeighbourLink link)
            ViewModel.Load(link);
    }
}
