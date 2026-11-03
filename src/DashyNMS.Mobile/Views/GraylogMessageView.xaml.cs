using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Views;

/// <summary>Complete plain-text message content in a pushed or list-owned host.</summary>
public partial class GraylogMessageView : ContentView
{
    public GraylogMessageView(GraylogMessageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = ViewModel = viewModel;
#if IOS
        SafeAreaEdges = SafeAreaEdges.None;
        Scroller.SafeAreaEdges = SafeAreaEdges.None;
#endif
        Scroller.HandlerChanged += OnScrollAttached;
        Scroller.Loaded += OnScrollAttached;
    }

    public GraylogMessageViewModel ViewModel { get; }
    public Controls.BackBar Bar => TopBar;

    private void OnScrollAttached(object? sender, EventArgs e)
    {
#if IOS
        if (Scroller.Handler?.PlatformView is UIKit.UIView native && FindScroll(native) is { } scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
#endif
    }
#if IOS
    private static UIKit.UIScrollView? FindScroll(UIKit.UIView view) =>
        view as UIKit.UIScrollView ?? view.Subviews.Select(FindScroll).FirstOrDefault(scroll => scroll is not null);
#endif

    /// <summary>The host passes only clearance not already owned by its layout.</summary>
    public void PlaceContent(Thickness clear)
    {
        var header = !ViewModel.IsBesideList && Bar.IsVisible;
        Bar.IsVisible = header;
        ContentColumn.Padding = new Thickness(16 + clear.Left, header ? 0 : 20, 16 + clear.Right, 24 + clear.Bottom);
        Fade.IsVisible = !header;
    }

    /// <summary>No detail fetch exists; absent route data must clear the previous record.</summary>
    public void Load(IDictionary<string, object>? parameters)
    {
        var message = parameters?.TryGetValue(Routes.GraylogMessageParameter, out var item) == true ? item as GraylogMessageItem : null;
        var list = parameters?.TryGetValue(Routes.GraylogListParameter, out var owner) == true ? owner as GraylogViewModel : null;
        ViewModel.Load(message, list);
        if (ViewModel.IsBesideList) PlaceContent(Thickness.Zero);
    }
}
