namespace DashyNMS.Mobile.Services;

/// <summary>How the four status filters on Alerts and Devices are arranged for the current pane size (#708).</summary>
public enum StatusFilterMode
{
    /// <summary>All four chips fit comfortably on one line without horizontal scrolling.</summary>
    SingleRow,

    /// <summary>Two rows of two chips so all four remain visible and operable at rest.</summary>
    TwoRows,

    /// <summary>One labelled Status control opening a sheet of choices when two rows would crowd the viewport.</summary>
    CompactPicker,
}

/// <summary>
/// Chooses a discoverable status filter layout from the measured list pane width,
/// viewport height and text scale, and builds accessible labels and summaries shared
/// by Alerts and Devices (#708).
/// </summary>
public static class StatusFilterLayout
{
    /// <summary>Minimum content width (at 1x text scale) before all four chips sit on one row.</summary>
    public const double DefaultSingleRowMinWidth = 480;

    /// <summary>Minimum content width (at 1x text scale) for a 2x2 chip grid to keep full names legible.</summary>
    public const double DefaultTwoRowMinWidth = 260;

    /// <summary>Text scale at or above which chips collapse to the labelled Status control (iOS AX1+).</summary>
    public const double AccessibilityScaleThreshold = 1.45;

    /// <summary>Maximum fraction of viewport height that a two-row chip grid may occupy.</summary>
    public const double MaxTwoRowHeightRatio = 0.24;

    /// <summary>
    /// Resolves the layout mode for a list pane's measured content width, viewport height and Dynamic Type scale.
    /// </summary>
    public static StatusFilterMode Resolve(
        double availableWidth,
        double viewportHeight,
        double fontScale = 1.0,
        double singleRowMinWidth = DefaultSingleRowMinWidth,
        double twoRowMinWidth = DefaultTwoRowMinWidth)
    {
        var scale = double.IsFinite(fontScale) ? Math.Max(0.8, fontScale) : 1.0;

        // Accessibility text sizes make a 2x2 grid wrap heavily and crowd the list;
        // collapse to one labelled Status control with a scrollable sheet of choices.
        if (scale >= AccessibilityScaleThreshold)
        {
            return StatusFilterMode.CompactPicker;
        }

        if (!double.IsFinite(availableWidth) || availableWidth <= 0)
        {
            return StatusFilterMode.TwoRows;
        }

        if (availableWidth >= singleRowMinWidth * scale)
        {
            return StatusFilterMode.SingleRow;
        }

        var minTwoRow = twoRowMinWidth * Math.Max(1.0, scale);
        var estimatedTwoRowHeight = (2 * (56 * scale)) + 8;
        var tooShortForTwoRows = double.IsFinite(viewportHeight)
            && viewportHeight > 0
            && estimatedTwoRowHeight > viewportHeight * MaxTwoRowHeightRatio;

        if (availableWidth < minTwoRow || tooShortForTwoRows)
        {
            return StatusFilterMode.CompactPicker;
        }

        return StatusFilterMode.TwoRows;
    }

    /// <summary>Summarises the active status selection for the compact Status control.</summary>
    public static string SummarizeSelection(
        IReadOnlyList<(string Name, bool Included)> items,
        string? overrideSummary = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideSummary))
        {
            return overrideSummary;
        }

        var included = items.Where(i => i.Included).Select(i => i.Name).ToList();
        if (included.Count == items.Count)
        {
            return "All";
        }

        if (included.Count == 0)
        {
            return "None";
        }

        if (included.Count == 1)
        {
            return $"{included[0]} only";
        }

        return string.Join(", ", included);
    }

    /// <summary>Spoken accessible description for a status filter chip or sheet row.</summary>
    public static string ChipDescription(string label, bool included) =>
        $"{label}, {(included ? "included" : "excluded")}";

    /// <summary>Visible secondary state text for a status filter chip or sheet row.</summary>
    public static string ChipStateText(bool included) =>
        included ? "Included" : "Excluded";
}
