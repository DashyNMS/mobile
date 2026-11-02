namespace DashyNMS.Mobile.Services;

/// <summary>Reserves the base of a setup viewport without counting native safe areas twice.</summary>
public static class SetupActionPlacement
{
    /// <summary>
    /// Coordinates are relative to the layout, after its native top safe area.
    /// A side pill anchors the action; bottom glass obstructs it; top glass does neither.
    /// A docked keyboard takes precedence. Floating keyboards are handled by focused-field scrolling.
    /// </summary>
    public static double BottomGap(double height, double safeBottom, OccupiedArea? pill,
        IReadOnlyList<OccupiedArea> glass, double? keyboardTop = null)
    {
        var gap = 12 + Math.Max(0, safeBottom);
        if (pill is { } bar)
        {
            var vertical = bar.Bottom - bar.Top > bar.Right - bar.Left;
            if (vertical)
                gap = Math.Max(0, height - bar.Bottom);
            else if (bar.Top > height / 2)
                gap = Math.Max(gap, height - bar.Top + 12);

            // Side actions live in the excluded side column. Only bottom-bar
            // actions span the form's lane and need additional vertical clearance.
            if (!vertical && bar.Top > height / 2)
                foreach (var area in glass)
                    gap = Math.Max(gap, height - area.Top + 12);
        }

        if (keyboardTop is { } top && top < height)
            gap = Math.Max(gap, height - Math.Max(0, top) + 12);
        return Math.Clamp(gap, 0, Math.Max(0, height));
    }
}
