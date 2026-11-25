namespace DashyNMS.Mobile.Services;

/// <summary>Where a cardless section list's viewport ends, and what its scrolling footer reserves.</summary>
/// <param name="Viewport">Bottom margin of the list viewport, from the base of its host.</param>
/// <param name="Overlay">Bottom margin for fixed overlays (filter panel, wide graph) resting above obstructions.</param>
/// <param name="Footer">Height of the list's footer, so the final row can scroll clear of every obstruction.</param>
public readonly record struct SectionListBase(double Viewport, double Overlay, double Footer);

/// <summary>
/// Device sections draw their rows as separate cards with no enclosing list
/// card, so the viewport's clip edge is the visible end of the content. Devices,
/// Alerts and the backup pages stop an enclosing card at the pill base; copying
/// that rule here clipped the rows above the screen's base on both iPhone Duo
/// displays (#469–#479). As on the Dashboard, the rows instead scroll to the
/// host's base, beneath the pill and its glass, and the footer alone carries the
/// clearance that keeps the final row reachable. A docked keyboard is the one
/// thing the viewport itself stops above, since rows beneath it can't be seen.
/// </summary>
public static class SectionListPlacement
{
    /// <summary>Space below the final row once it has scrolled clear of every obstruction.</summary>
    public const double FooterGap = 24;

    /// <summary>Space between a visible filter panel and the final row scrolled above it.</summary>
    public const double PanelGap = 12;

    /// <summary>Space kept between a docked keyboard top and the bottom of a scrolling list or form viewport.</summary>
    public const double KeyboardGap = 12;

    /// <summary>
    /// Converts a docked keyboard's top Y coordinate in the host into the bottom
    /// margin to apply to a list or form viewport (0 at rest or when the keyboard
    /// does not overlap the host).
    /// </summary>
    public static double KeyboardViewportMargin(double hostHeight, double? keyboardTop)
    {
        if (!double.IsFinite(hostHeight) || hostHeight <= 0
            || keyboardTop is not { } top || !double.IsFinite(top) || top >= hostHeight)
        {
            return 0;
        }

        var viewportBottom = Math.Clamp(top - KeyboardGap, 0, hostHeight);
        return hostHeight - viewportBottom;
    }

    /// <param name="obstruction">
    /// Bottom gap needed to clear the pill, its glass actions or native covered
    /// edges, measured in the list's host (<see cref="SetupActionPlacement.BottomGap"/>
    /// on iOS; 12 plus the covered bottom elsewhere).
    /// </param>
    /// <param name="keyboardGap">Bottom margin clearing a docked keyboard, when one overlaps the host.</param>
    /// <param name="panelHeight">Actual height of a visible (or exiting) filter panel resting above the obstruction.</param>
    /// <param name="actionHeight">Actual height of an overlaid fixed primary action (e.g. Save or Compare) resting above the obstruction.</param>
    public static SectionListBase Resolve(
        double obstruction,
        double? keyboardGap = null,
        double? panelHeight = null,
        double? actionHeight = null)
    {
        var viewport = keyboardGap is { } keyboard && double.IsFinite(keyboard) ? Math.Max(0, keyboard) : 0;
        var safeObstruction = double.IsFinite(obstruction) ? Math.Max(0, obstruction) : 0;
        var overlay = Math.Max(safeObstruction, viewport);
        var action = actionHeight is { } a && double.IsFinite(a) && a > 0 ? a : 0;
        var footer = FooterGap + (overlay - viewport) + action;
        if (panelHeight is { } panel && double.IsFinite(panel) && panel > 0)
            footer = Math.Max(footer, panel + overlay + PanelGap - viewport);
        return new(viewport, overlay, footer);
    }
}
