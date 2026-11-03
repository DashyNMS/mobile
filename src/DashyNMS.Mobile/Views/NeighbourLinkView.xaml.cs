using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
#if IOS
using UIKit;
#endif

namespace DashyNMS.Mobile.Views;

/// <summary>Snapshot detail shared by the pushed page and the Neighbours split host.</summary>
public sealed partial class NeighbourLinkView : ContentView
{
    private IDisposable? _edges;
    private Page? _page;

    public NeighbourLinkView(NeighbourLinkViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = ViewModel = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnChanged;
        Scroller.HandlerChanged += OnChanged;
        Scroller.Loaded += OnChanged;
    }

    public NeighbourLinkViewModel ViewModel { get; }
    public BackBar Bar => TopBar;

    private void OnLoaded(object? sender, EventArgs e)
    {
        OnUnloaded(sender, e);
        for (Element? parent = Parent; parent is not null; parent = parent.Parent)
            if (parent is Page page) { _page = page; break; }
        if (_page is not null) _edges = CoveredEdges.Watch(_page, Place);
#if IOS
        TabBarButtons.Moved -= OnChanged;
        TabBarButtons.Moved += OnChanged;
#endif
        Place();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        _edges?.Dispose();
        _edges = null;
        _page = null;
#if IOS
        TabBarButtons.Moved -= OnChanged;
#endif
    }

    private void OnChanged(object? sender, EventArgs e) => Place();

    internal void Place()
    {
#if IOS
        if (Scroller.Handler?.PlatformView is UIScrollView scroll)
            scroll.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
#endif
        if (_page is null) return;
        var edges = CoveredEdges.ForScrolling(_page);
        double left = edges.Left, right = edges.Right;
#if IOS
        // Insets belong only to the part of the host that reaches this column.
        if (_page.Handler?.PlatformView is UIView host && Handler?.PlatformView is UIView detail)
        {
            var bounds = detail.ConvertRectToView(detail.Bounds, host);
            left = Math.Max(0, left - bounds.Left);
            right = Math.Max(0, right - (host.Bounds.Width - bounds.Right));
        }
#endif
        TopBar.IsVisible = TopBar.ShowsBack;
        TopBar.Margin = new Thickness(16 + left, 0, 16 + right, 0);
        Viewport.Margin = new Thickness(16 + left, 0, 16 + right, 0);
        Column.Padding = new Thickness(0, 20, 0, 24 + edges.Bottom);
    }

    public void Load(IDictionary<string, object>? parameters)
    {
        if (parameters?.TryGetValue(Routes.NeighbourLinkParameter, out var value) == true && value is NeighbourLink link)
            ViewModel.Load(link);
    }
}
