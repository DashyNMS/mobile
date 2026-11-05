using DashyNMS.Mobile.Dashboard;

namespace DashyNMS.Mobile.DeviceSections;

/// <summary>
/// Ports show one compact card per row, and two only when both keep the
/// Dashboard's comfortable 340pt minimum. The breakpoint is the shared card
/// width rather than orientation, so the Duo's outer display stays at one
/// and its inner display reaches two, without an orientation estimate.
/// </summary>
public static class PortColumns
{
    /// <summary>Matches <see cref="DashboardColumns"/>, so cards read alike across pages.</summary>
    public const double MinimumCardWidth = 340;

    /// <summary>More than two across made each port harder to scan, however wide the screen.</summary>
    public const int MaximumColumns = 2;

    public static int Count(double available, double gap) =>
        double.IsFinite(available) && double.IsFinite(gap)
            ? DashboardColumns.Count(available, MinimumCardWidth, Math.Max(0, gap), MaximumColumns)
            : 1;
}
