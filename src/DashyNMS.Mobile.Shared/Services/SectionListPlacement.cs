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

    /// <param name="obstruction">
    /// Bottom gap needed to clear the pill, its glass actions or native covered
    /// edges, measured in the list's host (<see cref="SetupActionPlacement.BottomGap"/>
    /// on iOS; 12 plus the covered bottom elsewhere).
    /// </param>
    /// <param name="keyboardGap">Bottom gap clearing a docked keyboard, when one is showing.</param>
    /// <param name="panelHeight">Actual height of a visible filter panel resting above the obstruction.</param>
    public static SectionListBase Resolve(double obstruction, double? keyboardGap = null, double? panelHeight = null)
    {
        var viewport = keyboardGap is { } keyboard ? Math.Max(0, keyboard) : 0;
        var overlay = Math.Max(Math.Max(0, obstruction), viewport);
        var footer = FooterGap + (overlay - viewport);
        if (panelHeight is { } panel && panel > 0)
            footer = Math.Max(footer, panel + overlay + PanelGap - viewport);
        return new(viewport, overlay, footer);
    }
}
