using System.Runtime.Versioning;
using DashyNMS.Mobile.Services;
using UIKit;

namespace DashyNMS.Mobile;

/// <summary>
/// What the system draws over a view - the status bar, a camera - from
/// UIKit's occlusion reserved regions (iOS 27.1), for <see cref="BesideStatusBar"/>.
/// Our bars are our own, so unlike UIKit's they don't avoid these by themselves.
/// </summary>
internal static class ReservedRegions
{
    /// <summary>
    /// The active regions in <paramref name="view"/>'s own points, widened
    /// by the margins UIKit asks to keep beside them.
    /// </summary>
    [SupportedOSPlatform("ios27.1")]
    public static IReadOnlyList<OccupiedArea> Occupied(UIView view)
    {
        var regions = view.GetReservedRegions(UIViewReservedRegionKind.CreateOcclusion());
        if (regions is null)
        {
            return [];
        }

        var areas = new List<OccupiedArea>(regions.Length);
        foreach (var region in regions)
        {
            if (!region.Active)
            {
                continue;
            }

            var frame = region.Frame;
            var margins = region.Margins;
            areas.Add(new OccupiedArea(
                frame.Left - margins.Left, frame.Top, frame.Right + margins.Right, frame.Bottom));
        }

        return areas;
    }
}
