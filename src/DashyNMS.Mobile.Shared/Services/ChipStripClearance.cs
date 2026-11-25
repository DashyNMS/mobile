namespace DashyNMS.Mobile.Services;

/// <summary>Leading and trailing viewport margins that end a chip strip 12pt before row obstacles.</summary>
public readonly record struct HorizontalClearance(double Left, double Right);

/// <summary>
/// Computes the horizontal margins that end a horizontal chip strip's viewport
/// 12pt before any obstacle overlapping its row (clock, Wi-Fi, camera or side pill),
/// accounting for space outside the strip's own pane in split views.
/// </summary>
public static class ChipStripClearance
{
    /// <summary>Minimum gap kept between resting chips and any row obstacle.</summary>
    public const double Gap = 12;

    /// <param name="besideLeft">Leading obstacle width measured across the page (<c>CoveredEdges.Beside.Left</c>).</param>
    /// <param name="besideRight">Trailing obstacle width measured across the page (<c>CoveredEdges.Beside.Right</c>).</param>
    /// <param name="spaceLeftOfPane">Space between the page's left edge and the strip's pane.</param>
    /// <param name="spaceRightOfPane">Space between the strip's pane and the page's right edge.</param>
    public static HorizontalClearance Resolve(
        double besideLeft,
        double besideRight,
        double spaceLeftOfPane = 0,
        double spaceRightOfPane = 0)
    {
        var leftSpace = double.IsFinite(spaceLeftOfPane) ? Math.Max(0, spaceLeftOfPane) : 0;
        var rightSpace = double.IsFinite(spaceRightOfPane) ? Math.Max(0, spaceRightOfPane) : 0;
        var left = double.IsFinite(besideLeft) && besideLeft > 0 ? Math.Max(0, besideLeft + Gap - leftSpace) : 0;
        var right = double.IsFinite(besideRight) && besideRight > 0 ? Math.Max(0, besideRight + Gap - rightSpace) : 0;
        return new(left, right);
    }
}
