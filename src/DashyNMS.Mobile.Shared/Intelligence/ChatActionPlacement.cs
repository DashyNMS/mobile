using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>Where the chat's Saved chats and New chat actions go.</summary>
public enum ChatActionPlace
{
    /// <summary>Icon buttons beside the title: Android, and iOS before Liquid Glass.</summary>
    HeaderIcons,

    /// <summary>The shared glass by the tab bar, as every other page has its actions.</summary>
    TabBarGlass,

    /// <summary>A compact glass group beside the title, at the top of the page.</summary>
    HeaderGlass,
}

/// <summary>
/// Decides where the chat's actions sit (#594). A big screen in portrait
/// (an iPad, iPhone Duo's inner display) has the room to keep them at the
/// top, beside the title, out of the way of the transcript and composer;
/// a phone-sized window, including the Duo's outer display, and any
/// landscape window keep them in the tab bar's glass as before.
/// </summary>
/// <remarks>
/// An experiment awaiting installed-build approval, so the whole rule lives
/// here: keeping or reverting it is a change to this one method. It follows
/// the window, not the device, through the same breakpoint every page uses
/// (<see cref="ScreenLayout.RegularFrom"/>), so Split View, Stage Manager
/// and folding move the actions as the window changes.
/// </remarks>
public static class ChatActionPlacement
{
    /// <param name="glassAvailable">Whether Liquid Glass buttons can be shown (iOS 26 and later).</param>
    /// <param name="width">The page's width, in points.</param>
    /// <param name="height">The page's height, in points.</param>
    public static ChatActionPlace Resolve(bool glassAvailable, double width, double height)
    {
        if (!glassAvailable) return ChatActionPlace.HeaderIcons;
        // Not measured yet: keep the established placement until the page is.
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return ChatActionPlace.TabBarGlass;
        return width >= ScreenLayout.RegularFrom && height > width ? ChatActionPlace.HeaderGlass : ChatActionPlace.TabBarGlass;
    }
}
