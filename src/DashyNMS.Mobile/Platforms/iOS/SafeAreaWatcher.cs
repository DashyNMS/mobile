using UIKit;

namespace DashyNMS.Mobile;

/// <summary>
/// An empty, untouchable view laid over a page that reports when the page's
/// safe area changes, for <see cref="Controls.TopLineSpace"/>.
/// </summary>
/// <remarks>
/// Turning iPhone Duo's inner display to landscape and back changes where
/// the status bar is (#282), but not always at the same time as the page's
/// size, and MAUI tells a page only about its size. UIKit tells every view
/// about its safe area, so this view passes that on.
/// </remarks>
internal sealed class SafeAreaWatcher : UIView
{
    private readonly Action _changed;

    public SafeAreaWatcher(Action changed)
    {
        _changed = changed;
        UserInteractionEnabled = false;
        AccessibilityElementsHidden = true;
        BackgroundColor = UIColor.Clear;
        AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;
    }

    public override void SafeAreaInsetsDidChange()
    {
        base.SafeAreaInsetsDidChange();
        _changed();
    }
}
