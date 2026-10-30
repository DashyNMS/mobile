namespace DashyNMS.Mobile.Services;

/// <summary>
/// Part of the screen the system draws over - the status bar, a camera -
/// in the page's points, measured from its top-left corner.
/// </summary>
public readonly record struct OccupiedArea(double Left, double Top, double Right, double Bottom);

/// <summary>
/// Where the top line goes when it moves up beside the status bar: its top,
/// from the top of the page, and how far in from each end it keeps.
/// </summary>
public readonly record struct TopLinePlacement(double Top, double InsetStart, double InsetEnd);

/// <summary>
/// Decides whether the line every page shares (#253) can move up into the
/// strip the status bar sits in (#278). iPhone Duo's inner display has a
/// status bar along only part of the top, and an app built for iOS 27.1 may
/// use the rest; our bars are our own, so they must find that room
/// themselves rather than inherit it from UIKit's standard bars.
/// </summary>
/// <remarks>
/// Pure arithmetic, so it's tested here; the iOS head reads the occupied
/// areas from UIKit's reserved regions and applies the result.
/// </remarks>
public static class BesideStatusBar
{
    /// <summary>
    /// The least of the line's width that must be clear. An ordinary
    /// iPhone's clock, camera and signal leave only slivers, well short of
    /// this, so its pages keep their line below the status bar.
    /// </summary>
    public const double MinimumShare = 0.5;

    /// <summary>
    /// The placement for a line running from <paramref name="lineLeft"/> to
    /// <paramref name="lineRight"/>, or null to leave it below the status bar
    /// as it is today: no strip, nothing reported in it, or too little room.
    /// </summary>
    /// <param name="safeTop">The height of the strip: the page's top safe-area inset.</param>
    /// <param name="lineHeight">The line's own height, <c>TopBarHeight</c>.</param>
    /// <param name="occupied">What the system draws over, horizontal margins included.</param>
    public static TopLinePlacement? Place(
        double safeTop, double lineLeft, double lineRight, double lineHeight, IReadOnlyList<OccupiedArea> occupied)
    {
        var width = lineRight - lineLeft;
        if (safeTop <= 0 || width <= 0 || lineHeight <= 0)
        {
            return null;
        }

        // Only what's in the strip and across the line matters. Nothing there
        // means iOS hasn't said where the status bar is, so keep clear of it.
        var inStrip = occupied
            .Where(a => a.Right > a.Left && a.Bottom > a.Top)
            .Where(a => a.Top < safeTop && a.Right > lineLeft && a.Left < lineRight)
            .OrderBy(a => a.Left)
            .ToList();
        if (inStrip.Count == 0)
        {
            return null;
        }

        // The widest clear stretch across the line.
        double gapStart = lineLeft, gapEnd = lineLeft, cursor = lineLeft;
        foreach (var area in inStrip)
        {
            var end = Math.Min(area.Left, lineRight);
            if (end - cursor > gapEnd - gapStart)
            {
                (gapStart, gapEnd) = (cursor, end);
            }

            cursor = Math.Max(cursor, area.Right);
        }

        if (lineRight - cursor > gapEnd - gapStart)
        {
            (gapStart, gapEnd) = (cursor, lineRight);
        }

        if (gapEnd - gapStart < width * MinimumShare)
        {
            return null;
        }

        // Level with the status bar's middle, so the title lines up with the
        // clock as standard bars do. Holding the line down to the status bar's
        // foot instead left it visibly lower than the clock (#278).
        var top = inStrip.Min(a => a.Top);
        var bottom = inStrip.Max(a => a.Bottom);
        var lineTop = Math.Max(0, (top + bottom - lineHeight) / 2);

        // No higher than it already is: nothing to gain.
        if (lineTop >= safeTop)
        {
            return null;
        }

        return new TopLinePlacement(lineTop, gapStart - lineLeft, lineRight - gapEnd);
    }
}
