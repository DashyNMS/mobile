namespace DashyNMS.Mobile.Logs;

/// <summary>How the filter controls on <c>LogsPage</c> are arranged for the current pane size (#718).</summary>
public enum LogFilterMode
{
    /// <summary>Every filter chip fits on one line without horizontal clipping.</summary>
    Chips,

    /// <summary>One labelled Filters control showing the current time range and active filters, opening a sheet of choices.</summary>
    CompactButton,
}

/// <summary>
/// Chooses a discoverable filter layout for Event log and Alert log from the measured list pane width
/// and Dynamic Type scale, and builds concise summaries that keep the current time range visible at rest (#718).
/// </summary>
public static class LogFilterLayout
{
    /// <summary>Minimum content width (at 1x text scale) for the four default log chips to sit on one line.</summary>
    public const double DefaultSingleRowMinWidth = 580;

    /// <summary>Extra width required when the Clear device chip is shown alongside the default chips.</summary>
    public const double ClearDeviceChipWidth = 110;

    /// <summary>Extra width required when the Auto-refresh interval chip is shown.</summary>
    public const double IntervalChipWidth = 120;

    /// <summary>Baseline character count for the default "All devices" chip label.</summary>
    public const int DefaultDeviceLabelLength = 11;

    /// <summary>Estimated extra width (pt at 1x scale) per character beyond the default device chip label.</summary>
    public const double ExtraDeviceCharacterWidth = 8.5;

    /// <summary>Text scale at or above which chips collapse to the labelled Filters control (iOS AX1+).</summary>
    public const double AccessibilityScaleThreshold = 1.45;

    /// <summary>
    /// Resolves whether the log filter controls fit in one row of chips or should present the labelled Filters button.
    /// Accounts for <paramref name="deviceLabel"/> length so long hostnames do not push the time range offscreen.
    /// </summary>
    public static LogFilterMode Resolve(
        double availableWidth,
        double fontScale = 1.0,
        bool hasClearDevice = false,
        bool autoRefreshOn = false,
        string? deviceLabel = null)
    {
        var scale = double.IsFinite(fontScale) ? Math.Max(0.8, fontScale) : 1.0;
        if (scale >= AccessibilityScaleThreshold)
        {
            return LogFilterMode.CompactButton;
        }

        if (!double.IsFinite(availableWidth) || availableWidth <= 0)
        {
            return LogFilterMode.CompactButton;
        }

        var extraChars = string.IsNullOrWhiteSpace(deviceLabel)
            ? 0
            : Math.Max(0, deviceLabel.Trim().Length - DefaultDeviceLabelLength);

        var requiredWidth = DefaultSingleRowMinWidth
            + (extraChars * ExtraDeviceCharacterWidth)
            + (hasClearDevice ? ClearDeviceChipWidth : 0)
            + (autoRefreshOn ? IntervalChipWidth : 0);

        return availableWidth >= requiredWidth * scale
            ? LogFilterMode.Chips
            : LogFilterMode.CompactButton;
    }

    /// <summary>
    /// Summarises the current time range and any active filters for the labelled Filters control.
    /// Always leads with <paramref name="rangeLabel"/> so the time range stays discoverable at rest.
    /// </summary>
    public static string Summarize(
        string rangeLabel,
        string? deviceName,
        bool isDeviceFixed,
        string? kindLabel,
        string? searchText,
        bool autoRefreshOn)
    {
        var parts = new List<string>(5)
        {
            string.IsNullOrWhiteSpace(rangeLabel) ? "Any time" : rangeLabel.Trim(),
        };

        if (!isDeviceFixed && !string.IsNullOrWhiteSpace(deviceName))
        {
            parts.Add(deviceName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(kindLabel))
        {
            parts.Add(kindLabel.Trim());
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            parts.Add($"“{searchText.Trim()}”");
        }

        if (autoRefreshOn)
        {
            parts.Add("Auto-refresh");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>Visible label for the compact Filters button (for example, "Filters · Any time").</summary>
    public static string ButtonLabel(string summary) => $"Filters · {summary}";

    /// <summary>Spoken accessible description for the compact Filters button.</summary>
    public static string ButtonDescription(string summary) => $"Filters, {summary}";
}
