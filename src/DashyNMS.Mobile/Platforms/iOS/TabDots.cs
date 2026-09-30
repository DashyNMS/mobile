using DashyNMS.Mobile.Alerts;
using UIKit;

namespace DashyNMS.Mobile;

/// <summary>
/// Puts the Alerts dot (#90) on a tab bar item. Shell has no badge API, so
/// this finds Shell's own UITabBarController and sets the item's badge
/// directly: an empty badge value draws UIKit's small dot, coloured by
/// severity, with the counts for VoiceOver.
/// </summary>
internal static class TabDots
{
    /// <param name="index">The tab to mark (Alerts, or More when Alerts isn't pinned); every other tab is cleared.</param>
    public static void Show(int index, AlertTabSeverity severity, string description)
    {
        if (FindTabBar() is not { Items: { } items })
        {
            return;
        }

        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (i == index && severity != AlertTabSeverity.None)
            {
                item.BadgeValue = string.Empty;
                item.BadgeColor = severity == AlertTabSeverity.Critical
                    ? UIColor.FromRGB(0xDA, 0x36, 0x33)
                    : UIColor.FromRGB(0xDB, 0x9A, 0x04);
                item.AccessibilityValue = description;
            }
            else
            {
                item.BadgeValue = null;
                item.AccessibilityValue = null;
            }
        }
    }

    private static UITabBar? FindTabBar()
    {
        var root = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(scene => scene.Windows)
            .FirstOrDefault(window => window.IsKeyWindow)?.RootViewController;
        return Find(root)?.TabBar;
    }

    private static UITabBarController? Find(UIViewController? controller)
    {
        if (controller is null)
        {
            return null;
        }

        if (controller is UITabBarController tabs)
        {
            return tabs;
        }

        foreach (var child in controller.ChildViewControllers)
        {
            if (Find(child) is { } found)
            {
                return found;
            }
        }

        return Find(controller.PresentedViewController);
    }
}
