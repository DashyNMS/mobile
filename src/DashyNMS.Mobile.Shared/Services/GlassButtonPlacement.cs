namespace DashyNMS.Mobile.Services;

/// <summary>How many glass targets fit in the tab bar's lane.</summary>
public enum GlassLaneMode
{
    /// <summary>Full expanded search and actions fit in the glass lane.</summary>
    Expanded,

    /// <summary>Two 44pt targets fit: Back stays separate beside the ellipsis overflow.</summary>
    TwoTargets,

    /// <summary>Only one 44pt target fits: ellipsis menu with Back first (or Back/overflow alone).</summary>
    OneTarget,

    /// <summary>Zero 44pt targets fit: glass hides and the active page restores inline header fallback.</summary>
    ZeroTargets,
}

/// <summary>The resolved glass lane mode and its compact action frame, if placed.</summary>
public readonly record struct GlassLaneResult(GlassLaneMode Mode, OccupiedArea? ActionsFrame);

/// <summary>Collision checks shared by expanded glass groups and their compact fallback.</summary>
public static class GlassButtonPlacement
{
    /// <summary>Extra clearance required before stepping back up to a wider glass mode, preventing layout loops.</summary>
    public const double HysteresisGap = 8;

    /// <summary>Minimum touch target size for a single glass button.</summary>
    public const double MinimumTarget = 44;

    public static bool IsClear(OccupiedArea area, OccupiedArea safe, IReadOnlyList<OccupiedArea> obstacles, double gap = 0)
    {
        if (area.Right <= area.Left || area.Bottom <= area.Top) return true;
        return area.Left >= safe.Left && area.Top >= safe.Top && area.Right <= safe.Right && area.Bottom <= safe.Bottom
            && !obstacles.Any(o => area.Left < o.Right + gap && area.Right > o.Left - gap
                && area.Top < o.Bottom + gap && area.Bottom > o.Top - gap);
    }

    /// <summary>Overflow stays in the tab column or bottom row, never among the page's content.</summary>
    public static OccupiedArea? FindByBar(OccupiedArea wanted, OccupiedArea safe, OccupiedArea bar, IReadOnlyList<OccupiedArea> obstacles, double gap = 0)
    {
        var vertical = bar.Bottom - bar.Top > bar.Right - bar.Left;
        var height = wanted.Bottom - wanted.Top;
        var lane = vertical
            ? new OccupiedArea(Math.Max(safe.Left, bar.Left), safe.Top, Math.Min(safe.Right, bar.Right), Math.Min(safe.Bottom, bar.Top - 12))
            : new OccupiedArea(safe.Left, Math.Max(safe.Top, bar.Top - 12 - height), safe.Right, Math.Min(safe.Bottom, bar.Bottom));
        return Find(wanted, lane, obstacles, gap);
    }

    /// <summary>
    /// Chooses between expanded glass, two targets (Back + ellipsis), one target
    /// (ellipsis with Back first, or the lone action), or zero targets (inline fallback),
    /// with 8pt hysteresis when stepping back up to a roomier mode.
    /// </summary>
    public static GlassLaneResult ResolveLane(
        OccupiedArea wantedSearch,
        OccupiedArea wantedActions,
        bool hasBack,
        bool hasOverflow,
        OccupiedArea safe,
        OccupiedArea bar,
        IReadOnlyList<OccupiedArea> obstacles,
        GlassLaneMode previousMode = GlassLaneMode.Expanded,
        double minTarget = MinimumTarget)
    {
        var target = Math.Max(MinimumTarget, minTarget);
        var vertical = bar.Bottom - bar.Top > bar.Right - bar.Left;
        var natural = Math.Max(target, vertical ? bar.Right - bar.Left : bar.Bottom - bar.Top);

        // 1. Expanded group (search + all actions).
        var expandGap = previousMode == GlassLaneMode.Expanded ? 0 : HysteresisGap;
        if (IsClear(wantedSearch, safe, obstacles, expandGap) && IsClear(wantedActions, safe, obstacles, expandGap))
        {
            return new(GlassLaneMode.Expanded, wantedActions);
        }

        // Nothing to place in compact mode.
        if (!hasBack && !hasOverflow)
        {
            return new(GlassLaneMode.Expanded, wantedActions);
        }

        // 2. Two targets (Back + ellipsis) when both exist.
        if (hasBack && hasOverflow)
        {
            var twoGap = previousMode is GlassLaneMode.OneTarget or GlassLaneMode.ZeroTargets ? HysteresisGap : 0;
            if (TryFitCompact(2, natural, target, vertical, wantedActions, safe, bar, obstacles, twoGap) is { } two)
            {
                return new(GlassLaneMode.TwoTargets, two);
            }
        }

        // 3. One target (ellipsis with Back first, or lone Back/overflow).
        var oneGap = previousMode == GlassLaneMode.ZeroTargets ? HysteresisGap : 0;
        if (TryFitCompact(1, natural, target, vertical, wantedActions, safe, bar, obstacles, oneGap) is { } one)
        {
            return new(GlassLaneMode.OneTarget, one);
        }

        // 4. Zero targets fit: active owner restores inline header fallback.
        return new(GlassLaneMode.ZeroTargets, null);
    }

    private static OccupiedArea? TryFitCompact(
        int count,
        double natural,
        double minTarget,
        bool vertical,
        OccupiedArea anchor,
        OccupiedArea safe,
        OccupiedArea bar,
        IReadOnlyList<OccupiedArea> obstacles,
        double gap)
    {
        foreach (var size in natural > minTarget ? new[] { natural, minTarget } : new[] { minTarget })
        {
            var width = vertical ? size : size * count;
            var height = vertical ? size * count : size;
            var left = vertical ? bar.Left + ((bar.Right - bar.Left - width) / 2) : anchor.Left;
            var top = anchor.Top;
            var candidate = new OccupiedArea(left, top, left + width, top + height);
            if (FindByBar(candidate, safe, bar, obstacles, gap) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Finds the nearest clear position at obstacle boundaries, without guessing a Live Activity's dimensions.</summary>
    public static OccupiedArea? Find(OccupiedArea wanted, OccupiedArea safe, IReadOnlyList<OccupiedArea> obstacles, double gap = 12)
    {
        var width = wanted.Right - wanted.Left;
        var height = wanted.Bottom - wanted.Top;
        if (width <= 0 || height <= 0 || width > safe.Right - safe.Left || height > safe.Bottom - safe.Top) return null;
        var xs = new[] { wanted.Left, safe.Left, safe.Right - width }
            .Concat(obstacles.SelectMany(o => new[] { o.Left - gap - width, o.Right + gap })).Distinct();
        var ys = new[] { wanted.Top, safe.Top, safe.Bottom - height }
            .Concat(obstacles.SelectMany(o => new[] { o.Top - gap - height, o.Bottom + gap })).Distinct();
        return xs.SelectMany(x => ys.Select(y => new OccupiedArea(x, y, x + width, y + height)))
            .Where(a => IsClear(a, safe, obstacles, gap))
            .OrderBy(a => Math.Pow(a.Left - wanted.Left, 2) + Math.Pow(a.Top - wanted.Top, 2))
            .Select(a => (OccupiedArea?)a).FirstOrDefault();
    }
}
