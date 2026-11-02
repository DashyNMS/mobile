namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Keeps dashboard editing and its pickers on the same glass Back control,
/// while leaving each editor's explicit Save action and save rules intact.
/// </summary>
internal static class DashboardBackNavigation
{
    public static void Attach(ContentPage page, BackBar bar, params View[] scrollers)
    {
#if IOS
        if (!TabBarButtons.IsAvailable)
        {
            return;
        }

        bar.ShowsBack = false;
        // Keep any remaining header actions; editor Save buttons belong below their forms.
        bar.IsVisible = bar.Actions is not null;
        // These form pages keep the native safe area. Only our extra glass
        // row needs additional clearance; adding the system insets again
        // lifted their lists and left a second bottom gap (#391).
        page.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
        foreach (var scroller in scrollers)
        {
            scroller.HandlerChanged += (_, _) => StopInsets(scroller);
            scroller.Loaded += (_, _) => StopInsets(scroller);
        }
        var padding = page.Padding;
        var showing = false;

        void PlaceControls()
        {
            if (!showing)
            {
                return;
            }

            page.Padding = new Thickness(padding.Left, padding.Top + 16,
                padding.Right, padding.Bottom + 12 + TabBarButtons.CoveredBottom);
        }

        void OnMoved(object? sender, EventArgs e) => page.Dispatcher.Dispatch(PlaceControls);

        CoveredEdges.Watch(page, PlaceControls);
        page.Appearing += (_, _) =>
        {
            showing = true;
            TabBarButtons.Show(page, null, string.Empty,
            [
                new GlassAction("chevron.backward", "Back to " + BackBar.PreviousTitle(page),
                    () => _ = Shell.Current.GoToAsync(".."), IsBack: true),
            ]);
            TabBarButtons.Moved += OnMoved;
            PlaceControls();
        };
        page.Disappearing += (_, _) =>
        {
            showing = false;
            TabBarButtons.Moved -= OnMoved;
            TabBarButtons.Hide(page);
        };
#endif
    }

#if IOS
    private static void StopInsets(View scroller)
    {
        if (scroller.Handler?.PlatformView is UIKit.UIView view && FindScroll(view) is { } scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }

    private static UIKit.UIScrollView? FindScroll(UIKit.UIView view) =>
        view as UIKit.UIScrollView ?? view.Subviews.Select(FindScroll).FirstOrDefault(found => found is not null);
#endif
}
