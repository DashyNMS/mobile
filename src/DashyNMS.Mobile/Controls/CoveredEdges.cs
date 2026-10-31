#if IOS
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
