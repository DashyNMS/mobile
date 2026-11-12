using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// Where the chat's composer sits and how much of the transcript's foot stays clear (#594).
/// </summary>
/// <param name="Gap">From the composer's base to the page's base.</param>
/// <param name="Clearance">From the page's base to the top of the tab bar, its glass or a docked keyboard: the last message scrolls above this.</param>
/// <param name="LaneLeft">The composer lane's left edge, or null for the whole chat column.</param>
/// <param name="LaneRight">The composer lane's right edge, or null for the whole chat column.</param>
public readonly record struct ChatComposerLane(double Gap, double Clearance, double? LaneLeft, double? LaneRight);

/// <summary>
/// Puts the composer's base level with the tab bar's base, as list cards are
/// (#594). The transcript runs to the bottom of the screen behind it.
/// </summary>
/// <remarks>
/// A side pill already gives that alignment through <see cref="SetupActionPlacement.BottomGap"/>.
/// A bottom pill would push a full-width composer above it, so in a wide
/// window (landscape) the composer takes the free lane beside the pill and its
/// glass instead, keeping its base on the pill's. A lane narrower than
/// <see cref="MinimumLane"/> (a phone in portrait) keeps the composer above
/// the bar, and a docked keyboard always wins, since it covers the bar.
/// </remarks>
public static class ChatComposerPlacement
{
    /// <summary>Narrower than this, a typed question wraps too soon to be comfortable.</summary>
    public const double MinimumLane = 360;

    /// <param name="width">The layout's width.</param>
    /// <param name="height">The layout's height.</param>
    /// <param name="safeBottom">The native bottom inset, used without a measured pill.</param>
    /// <param name="columnLeft">The chat column's left edge after gutters.</param>
    /// <param name="columnRight">The chat column's right edge after gutters.</param>
    /// <param name="pill">The tab bar's pill, in layout coordinates.</param>
    /// <param name="glass">The glass buttons, in layout coordinates.</param>
    /// <param name="keyboardTop">The top of a docked keyboard, if one is up.</param>
    public static ChatComposerLane Resolve(double width, double height, double safeBottom, double columnLeft, double columnRight,
        OccupiedArea? pill, IReadOnlyList<OccupiedArea> glass, double? keyboardTop = null)
    {
        var stacked = SetupActionPlacement.BottomGap(height, safeBottom, pill, glass, keyboardTop);
        var whole = new ChatComposerLane(stacked, stacked, null, null);
        if (pill is not { } bar || width <= 0 || height <= 0) return whole;
        if (keyboardTop is { } top && top < height) return whole;
        var vertical = bar.Bottom - bar.Top > bar.Right - bar.Left;
        if (vertical || bar.Top <= height / 2) return whole;

        // Everything sharing the bar's row: the pill and any glass beside or above it.
        var row = glass.Where(area => area.Top > height / 2).Append(bar).ToArray();
        var left = row.Min(area => area.Left);
        var right = row.Max(area => area.Right);
        var leftLane = (Left: columnLeft, Right: Math.Min(columnRight, left - 12));
        var rightLane = (Left: Math.Max(columnLeft, right + 12), Right: columnRight);
        var lane = leftLane.Right - leftLane.Left >= rightLane.Right - rightLane.Left ? leftLane : rightLane;
        if (lane.Right - lane.Left < MinimumLane) return whole;
        return new ChatComposerLane(Math.Max(0, height - bar.Bottom), stacked, lane.Left, lane.Right);
    }
}
