namespace DashyNMS.Mobile.Services;

/// <summary>Collision checks shared by expanded glass groups and their compact fallback.</summary>
public static class GlassButtonPlacement
{
    public static bool IsClear(OccupiedArea area, OccupiedArea safe, IReadOnlyList<OccupiedArea> obstacles, double gap = 0)
    {
        if (area.Right <= area.Left || area.Bottom <= area.Top) return true;
        return area.Left >= safe.Left && area.Top >= safe.Top && area.Right <= safe.Right && area.Bottom <= safe.Bottom
            && !obstacles.Any(o => area.Left < o.Right + gap && area.Right > o.Left - gap
                && area.Top < o.Bottom + gap && area.Bottom > o.Top - gap);
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
