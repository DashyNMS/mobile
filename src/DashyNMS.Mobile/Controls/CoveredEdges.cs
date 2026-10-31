#if IOS
using CoreGraphics;
using UIKit;
#endif

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// How far the system and the tab bar cover each edge of a page: the status
/// bar, iPhone Duo's camera pillar, the home indicator and the Liquid Glass
/// tab bar, wherever it is. For a page that runs its background content -
/// a map, a strip of chips - underneath them, so the screen feels as big as
/// it is, while keeping what can be tapped clear of them.
/// </summary>
/// <remarks>
/// The rule: what nobody is looking at (map nodes, chips scrolled out of
/// sight) may pass under the glass and the camera; cards, buttons and
/// anything else that can be tapped always sit inside these edges. MAUI's
/// <c>SafeAreaEdges</c> can only keep a whole layout clear or not, so a
/// page that opts out reads the edges here and places its controls itself.
/// Android's tab bar sits below the page rather than over it, so there it's
/// nothing and pages keep MAUI's own safe area.
/// </remarks>
internal static class CoveredEdges
{
    /// <summary>The covered part of each edge of <paramref name="page"/>, in its points.</summary>
    public static Thickness Of(Page page)
    {
#if IOS
        if (page.Handler?.PlatformView is UIView view && view.Window is not null)
        {
            var insets = view.SafeAreaInsets;
            return new Thickness(insets.Left, insets.Top, insets.Right, insets.Bottom);
        }
#endif
        return Thickness.Zero;
    }

    /// <summary>
    /// How far in from each side <paramref name="view"/> must keep across the
    /// row it sits in: only what is beside it there - the status bar, a
    /// camera, the tabs - rather than the whole edge's safe area, which on
    /// iPhone Duo's inner display covers a column the tab bar fills only at
    /// its foot. Top and bottom are nought.
    /// </summary>
    /// <remarks>
    /// Where iOS doesn't say what it draws where (before iOS 27.1), or the
    /// view isn't laid out yet, it's the safe area's sides, as <see cref="Of"/>.
    /// </remarks>
    public static Thickness Beside(Page page, View view)
    {
        var edges = Of(page);
        var fallback = new Thickness(edges.Left, 0, edges.Right, 0);
#if IOS
        if (!OperatingSystem.IsIOSVersionAtLeast(27, 1)
            || page.Handler?.PlatformView is not UIView pageView || pageView.Window is null
            || view.Handler?.PlatformView is not UIView row || row.Window is null || row.Bounds.Height <= 0)
        {
            return fallback;
        }

        var occupied = ReservedRegions.Occupied(pageView);
        if (occupied.Count == 0)
        {
            return fallback;
        }

        var obstacles = occupied
            .Select(a => new CGRect(a.Left, a.Top, a.Right - a.Left, a.Bottom - a.Top))
            .ToList();
        if (TabDots.FindTabs() is { TabBar: { Hidden: false } tabBar } controller)
        {
            // Each tab where UIKit's tabs are in use; otherwise the whole bar,
            // which may run the length of its edge.
            var tabs = controller.Tabs ?? [];
            foreach (var tab in tabs)
            {
                var frame = tab.GetFrame(pageView);
                if (!frame.IsNull() && frame.Width > 0 && frame.Height > 0)
                {
                    obstacles.Add(frame);
                }
            }

            if (tabs.Length == 0)
            {
                obstacles.Add(tabBar.ConvertRectToView(tabBar.Bounds, pageView));
            }
        }

        var line = row.ConvertRectToView(row.Bounds, pageView);
        double left = 0, right = 0;
        foreach (var obstacle in obstacles.Where(o => o.Top < line.Bottom && o.Bottom > line.Top))
        {
            if (obstacle.GetMidX() > line.GetMidX())
            {
                right = Math.Max(right, pageView.Bounds.Width - obstacle.Left);
            }
            else
            {
                left = Math.Max(left, obstacle.Right);
            }
        }

        return new Thickness(left, 0, right, 0);
#else
        return fallback;
#endif
    }

    /// <summary>
    /// Calls <paramref name="changed"/> whenever the edges may have moved:
    /// the page resizing, and on iOS UIKit's own safe-area change, which
    /// folding or turning iPhone Duo doesn't always pair with a size change
    /// (#282).
    /// </summary>
    public static void Watch(Page page, Action changed)
    {
        page.SizeChanged += (_, _) => changed();
#if IOS
        SafeAreaWatcher? watcher = null;
        void Attach()
        {
            if (page.Handler?.PlatformView is not UIView view || ReferenceEquals(watcher?.Superview, view))
            {
                return;
            }

            watcher?.RemoveFromSuperview();
            watcher = new SafeAreaWatcher(() => page.Dispatcher.Dispatch(changed)) { Frame = view.Bounds };
            view.AddSubview(watcher);
        }

        page.HandlerChanged += (_, _) => Attach();
        page.Loaded += (_, _) => Attach();
#endif
    }
}
