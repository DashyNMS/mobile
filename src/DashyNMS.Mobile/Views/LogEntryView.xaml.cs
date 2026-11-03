using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Views;

/// <summary>Complete, read-only log content in either a pushed or list-owned host.</summary>
public partial class LogEntryView : ContentView
{
    public LogEntryView(LogEntryViewModel viewModel)
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

    public LogEntryViewModel ViewModel { get; }
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

    /// <summary>The host supplies only clearance not already owned by its layout.</summary>
    public void PlaceContent(Thickness clear)
    {
        var header = !ViewModel.IsBesideList && Bar.IsVisible;
        Bar.IsVisible = header;
        ContentColumn.Padding = new Thickness(16 + clear.Left, header ? 0 : 20, 16 + clear.Right, 24 + clear.Bottom);
        Fade.IsVisible = !header;
        Fade.Margin = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
    }

    /// <summary>No detail fetch exists: invalid route data clears any previously shown record.</summary>
    public void Load(IDictionary<string, object>? parameters)
    {
        var entry = parameters?.TryGetValue(Routes.LogEntryParameter, out var item) == true ? item as LogEntryItem : null;
        var list = parameters?.TryGetValue(Routes.LogsListParameter, out var owner) == true ? owner as LogsViewModel : null;
        ViewModel.Load(entry, list);
        if (ViewModel.IsBesideList) PlaceContent(Thickness.Zero);
    }
}
