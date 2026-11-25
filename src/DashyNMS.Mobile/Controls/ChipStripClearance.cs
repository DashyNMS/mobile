using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Ends a horizontal chip strip's viewport 12pt before row obstacles (clock, Wi-Fi,
/// camera, side pill) using pane-adjusted <see cref="CoveredEdges.Beside"/> so
/// resting chips never tuck under obstacles on iPhone Duo while running edge-to-edge
/// on standard iPhones and leaving split-view left panes unclipped.
/// </summary>
internal static class ChipStripPlacement
{
    public static void Apply(
        Page page,
        View strip,
        View? pane = null,
        double topMargin = 0,
        double bottomMargin = 0,
        double topPadding = 0,
        double bottomPadding = 0,
        double gutter = 16)
    {
#if IOS
        var beside = CoveredEdges.Beside(page, strip);
        double spaceLeft = 0, spaceRight = 0;
        if (page.Handler?.PlatformView is UIKit.UIView pageView && pageView.Bounds.Width > 0)
        {
            var host = pane ?? strip.Parent as View;
            if (host?.Handler?.PlatformView is UIKit.UIView hostView && hostView.Window is not null)
            {
                var frame = hostView.ConvertRectToView(hostView.Bounds, pageView);
                spaceLeft = Math.Max(0, (double)frame.Left);
                spaceRight = Math.Max(0, (double)(pageView.Bounds.Width - frame.Right));
            }
        }

        var clear = ChipStripClearance.Resolve(beside.Left, beside.Right, spaceLeft, spaceRight);
        var margin = new Thickness(clear.Left, topMargin, clear.Right, bottomMargin);
        if (Changed(strip.Margin, margin)) strip.Margin = margin;
        if (strip is ScrollView scroll)
        {
            var padLeft = clear.Left > 0 ? 0 : gutter;
            var padRight = clear.Right > 0 ? 0 : gutter;
            var padding = new Thickness(padLeft, topPadding, padRight, bottomPadding);
            if (Changed(scroll.Padding, padding)) scroll.Padding = padding;
        }
#endif
    }

#if IOS
    private static bool Changed(Thickness a, Thickness b) =>
        Math.Abs(a.Left - b.Left) > 0.5
        || Math.Abs(a.Top - b.Top) > 0.5
        || Math.Abs(a.Right - b.Right) > 0.5
        || Math.Abs(a.Bottom - b.Bottom) > 0.5;
#endif
}
