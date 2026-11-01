namespace DashyNMS.Mobile.Controls;

/// <summary>Matches the Alerts/Devices card's base to the native tab pill, with covered rows scrollable clear.</summary>
internal sealed class TabAlignedListLayout
{
#if IOS
    private readonly ContentPage _page;
    private readonly Layout _column;
    private readonly Border _card;
    private readonly View _foot;
    private readonly Action<Thickness> _detail;
#endif

    public TabAlignedListLayout(ContentPage page, ListDetail split, Layout column, Border card, CollectionView rows, View foot, Action<Thickness> detail)
    {
#if IOS
        _page = page;
        _column = column;
        _card = card;
        _foot = foot;
        _detail = detail;
        page.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        CoveredEdges.Watch(page, Place);
        split.SplitChanged += (_, _) => Place();
        column.SizeChanged += (_, _) => Place();
        rows.HandlerChanged += (_, _) => StopInsets();
        rows.Loaded += (_, _) => StopInsets();

        void StopInsets()
        {
            if (rows.Handler?.PlatformView is UIKit.UIView view && FindScroll(view) is { } scroll)
                scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
        }
#endif
    }

    public void Place()
    {
#if IOS
        if (_page.Width <= 0) return;
        var edges = CoveredEdges.Of(_page);
        double width = _page.Width, left = edges.Left, right = edges.Right;
        var covered = edges.Bottom + TabBarButtons.CoveredBottom;
        double? listFoot = null;
        var sideBar = false;
        if (_page.Handler?.PlatformView is UIKit.UIView page && TabBarButtons.PillIn(page) is { } pill)
        {
            width = page.Bounds.Width;
            sideBar = pill.Height > pill.Width;
            if (sideBar)
            {
                if (pill.Left + (pill.Width / 2) > width / 2) right = Math.Max(right, width - pill.Left + 12 - 16);
                else left = Math.Max(left, pill.Right + 12 - 16);
            }
            if (sideBar || pill.Top > page.Bounds.Height / 2)
                listFoot = Math.Max(0, page.Bounds.Height - pill.Bottom);
        }

        var columnWidth = _column.Width > 0 ? _column.Width : width;
        _column.Padding = new Thickness(16 + left, 0, 16 + Math.Max(0, right - Math.Max(0, width - columnWidth)), 0);
        var foot = listFoot ?? 12 + covered;
        _card.Margin = new Thickness(0, 0, 0, foot);
        var over = listFoot is not null && !sideBar ? covered : 0;
        var spacer = over > foot ? over - foot + 8 : 0;
        if (_foot.HeightRequest != spacer) _foot.HeightRequest = spacer;
        _detail(new Thickness(0, 0, right, covered));
#endif
    }

#if IOS
    private static UIKit.UIScrollView? FindScroll(UIKit.UIView view) =>
        view as UIKit.UIScrollView ?? view.Subviews.Select(FindScroll).FirstOrDefault(found => found is not null);
#endif
}
