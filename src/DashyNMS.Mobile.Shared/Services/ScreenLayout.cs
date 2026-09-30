namespace DashyNMS.Mobile.Services;

/// <summary>How much room a window has across, for <see cref="ScreenLayout"/>.</summary>
public enum WidthClass
{
    /// <summary>A phone, or an iPad app in a narrow Split View or Slide Over.</summary>
    Compact,

    /// <summary>An iPad in portrait, the iPhone Duo open, a phone in landscape, a half-screen window.</summary>
    Regular,

    /// <summary>An iPad in landscape, or a large Mac window.</summary>
    Wide,
}

/// <summary>
/// The size breakpoints, in one place, so every page changes at the same
/// widths (#88). They follow the window's width, not the device: an iPad in
/// Split View or Stage Manager, a resized Mac window and an iPhone Duo
/// folding or unfolding all change it, and pages react as the width
/// changes, not only on rotation.
/// </summary>
public static class ScreenLayout
{
    /// <summary>
    /// Narrower than this is a phone's layout. 600 keeps an iPad app at a
    /// third of the screen (about 320-500 points) on it.
    /// </summary>
    public const double RegularFrom = 600;

    /// <summary>From an iPad in landscape (1024 points and more) up.</summary>
    public const double WideFrom = 1000;

    /// <summary>
    /// From here a list and its detail go side by side: room for the list
    /// and at least as much again for the detail - an iPad mini in portrait
    /// (744) is.
    /// </summary>
    public const double SplitFrom = 700;

    /// <summary>
    /// The widest text-heavy pages go (Settings, Alert detail, Sign in):
    /// longer lines are hard to read, and a stretched form looks empty.
    /// </summary>
    public const double ReadableWidth = 680;

    public static WidthClass Classify(double width) => width switch
    {
        >= WideFrom => WidthClass.Wide,
        >= RegularFrom => WidthClass.Regular,
        _ => WidthClass.Compact,
    };

    /// <summary>Whether a list page shows the item you tap beside itself rather than on a page of its own.</summary>
    public static bool SplitsListAndDetail(double width) => width >= SplitFrom;

    /// <summary>
    /// The list's width beside a detail pane: as wide as a phone's list, a
    /// little more when there's plenty of room; the detail takes the rest.
    /// </summary>
    public static double ListPaneWidth(double width) => width >= WideFrom ? 400 : 340;
}
