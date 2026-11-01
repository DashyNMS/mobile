namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Places a pushed list page clear of the camera and glass while its rows
/// scroll underneath the bottom bar. Used by all three config backup pages
/// so their Back ownership and safe areas stay consistent.
/// </summary>
internal sealed class PushedListLayout
{
    private readonly ContentPage _page;
    private readonly PageHeader _header;
    private readonly Layout _body;
#if IOS
    private readonly CollectionView _rows;
#endif
    private readonly View _foot;
    private readonly View? _bottomAction;

    public PushedListLayout(ContentPage page, PageHeader header, Layout body, CollectionView rows, View foot, View? bottomAction = null)
    {
        _page = page;
        _header = header;
        _body = body;
#if IOS
        _rows = rows;
#endif
        _foot = foot;
        _bottomAction = bottomAction;
#if IOS
        page.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        header.BackInGlass = TabBarButtons.IsAvailable;
        rows.HandlerChanged += (_, _) => StopInsetAdjustment();
        rows.Loaded += (_, _) => StopInsetAdjustment();
#endif
        CoveredEdges.Watch(page, Place);
        if (bottomAction is not null)
        {
            bottomAction.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(View.IsVisible)) Place();
            };
        }
    }

    public void Show()
    {
        _header.Apply(_page);
#if IOS
        if (TabBarButtons.IsAvailable)
        {
            TabBarButtons.Show(_page, null, string.Empty,
            [
                new("chevron.backward", "Back to " + BackBar.PreviousTitle(_page), () => _ = Shell.Current.GoToAsync("..")),
            ]);
        }
        TabBarButtons.Moved += OnMoved;
#endif
        Place();
    }

    public void Hide()
    {
#if IOS
        TabBarButtons.Moved -= OnMoved;
        TabBarButtons.Hide(_page);
#endif
    }

    private void Place()
    {
        var clear = CoveredEdges.ForScrolling(_page);
        _body.Padding = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
        // When Compare is visible it already lifts the whole list clear of the glass.
        var hasAction = _bottomAction?.IsVisible == true;
        _foot.HeightRequest = 24 + (hasAction ? 0 : clear.Bottom);
        if (_bottomAction is not null) _bottomAction.Margin = new Thickness(0, 8, 0, 12 + clear.Bottom);
    }

#if IOS
    private void OnMoved(object? sender, EventArgs e) => _page.Dispatcher.Dispatch(Place);

    private void StopInsetAdjustment()
    {
        if (_rows.Handler?.PlatformView is UIKit.UIView view && FindScroll(view) is { } scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }

    private static UIKit.UIScrollView? FindScroll(UIKit.UIView view) =>
        view as UIKit.UIScrollView ?? view.Subviews.Select(FindScroll).FirstOrDefault(found => found is not null);
#endif
}
