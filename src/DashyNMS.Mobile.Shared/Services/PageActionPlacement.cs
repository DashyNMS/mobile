namespace DashyNMS.Mobile.Services;

/// <summary>Where a page's frequent actions go.</summary>
public enum PageActionPlace
{
    /// <summary>Icon buttons beside the title: Android, and iOS before Liquid Glass.</summary>
    HeaderIcons,

    /// <summary>The shared glass by the tab bar, as compact and landscape windows use.</summary>
    TabBarGlass,

    /// <summary>A compact glass group beside the title on regular portrait windows when both fit the safe lane.</summary>
    HeaderGlass,
}

/// <summary>
/// Decides where a page's frequent actions sit. Regular portrait windows
/// (<c>&gt;= 600pt</c>, taller than wide — such as iPhone Duo's inner portrait
/// and portrait iPads) place <c>HeaderGlassGroup</c> beside the title when both
/// title and glass fit the safe lane, keeping Back and Search in <c>TabBarButtons</c>.
/// Compact windows, landscape windows and tight titles fall back to <c>TabBarButtons</c>.
/// </summary>
public static class PageActionPlacement
{
    /// <summary>Minimum readable width reserved for a wrapping title beside title-row glass.</summary>
    public const double MinimumTitleLane = 180;

    /// <summary>Gap kept between the title column and <c>HeaderGlassGroup</c>.</summary>
    public const double HeaderGap = 12;

    /// <param name="glassAvailable">Whether Liquid Glass buttons can be shown (iOS 26 and later).</param>
    /// <param name="width">The page's width, in points.</param>
    /// <param name="height">The page's height, in points.</param>
    /// <param name="headerLaneWidth">Available safe lane width for title plus glass, or <see cref="double.PositiveInfinity"/> if unmeasured.</param>
    /// <param name="titleWidth">Natural single-line width of the page title (0 if unmeasured).</param>
    /// <param name="glassWidth">Width of the <c>HeaderGlassGroup</c> (44pt per action).</param>
    public static PageActionPlace Resolve(
        bool glassAvailable,
        double width,
        double height,
        double headerLaneWidth = double.PositiveInfinity,
        double titleWidth = 0,
        double glassWidth = 0)
    {
        if (!glassAvailable) return PageActionPlace.HeaderIcons;
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return PageActionPlace.TabBarGlass;
        if (width < ScreenLayout.RegularFrom || height <= width) return PageActionPlace.TabBarGlass;

        if (double.IsFinite(headerLaneWidth) && headerLaneWidth > 0 && glassWidth > 0)
        {
            var remaining = headerLaneWidth - glassWidth - HeaderGap;
            var needed = titleWidth > 0 ? Math.Min(titleWidth, MinimumTitleLane) : MinimumTitleLane;
            if (remaining < needed) return PageActionPlace.TabBarGlass;
        }

        return PageActionPlace.HeaderGlass;
    }
}
