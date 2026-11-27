namespace DashyNMS.Mobile.Dashboard;

/// <summary>One visible card's grid slot in visual reading order.</summary>
public readonly record struct DashboardGridSlot(int Index, int Column, int Row, int RowSpan);

/// <summary>Measured position and size of one visible card inside the grid's content box.</summary>
public readonly record struct DashboardCardFrame(int Index, double X, double Y, double Width, double Height);

/// <summary>Measured geometry of every visible card in a dashboard grid.</summary>
public sealed record DashboardGridArrangement(
    int Columns,
    double ColumnWidth,
    double TotalHeight,
    bool SpansLeadingTriage,
    IReadOnlyList<DashboardCardFrame> Frames);

/// <summary>
/// Places dashboard cards across measured columns, keeping Alerts and Devices
/// compact beside a two-row Needs attention card on three-column layouts (#709).
/// </summary>
/// <remarks>
/// The arrangement depends only on measured width and card types, with no
/// device-name checks, and never reorders the saved card list.
/// </remarks>
public static class DashboardGridLayout
{
    /// <summary>
    /// True when three columns fit, at least one card follows the leading trio,
    /// and the first three visible cards are Alerts, Devices and Needs attention.
    /// </summary>
    public static bool CanSpanLeadingTriage(int columns, IReadOnlyList<string> cardTypes) =>
        columns == 3
        && cardTypes.Count > 3
        && cardTypes[0] == DashboardLayout.AlertsGauge
        && cardTypes[1] == DashboardLayout.DeviceStatus
        && cardTypes[2] == DashboardLayout.Alerts;

    /// <summary>How many columns fit <paramref name="width"/> for <paramref name="cards"/> cards, and how wide each is.</summary>
    public static (int Columns, double ColumnWidth) ResolveColumns(
        double width,
        int cards,
        double minColumnWidth = 340,
        double gap = 12,
        int maxColumns = 3,
        bool evenRows = false)
    {
        var columns = DashboardColumns.Count(width, minColumnWidth, gap, maxColumns);
        if (evenRows && cards > columns)
        {
            while (columns > 1 && cards % columns != 0)
            {
                columns--;
            }
        }

        return (columns, Math.Max(0, (width - (gap * (columns - 1))) / columns));
    }

    /// <summary>Plans grid slots for visible cards in their saved order.</summary>
    public static IReadOnlyList<DashboardGridSlot> PlanSlots(
        int columns,
        IReadOnlyList<string> cardTypes,
        bool allowLeadingTriageSpan)
    {
        var count = cardTypes.Count;
        var slots = new DashboardGridSlot[count];
        if (count == 0)
        {
            return slots;
        }

        var safeColumns = Math.Max(1, columns);
        if (allowLeadingTriageSpan && CanSpanLeadingTriage(safeColumns, cardTypes))
        {
            slots[0] = new DashboardGridSlot(0, Column: 0, Row: 0, RowSpan: 1);
            slots[1] = new DashboardGridSlot(1, Column: 1, Row: 0, RowSpan: 1);
            slots[2] = new DashboardGridSlot(2, Column: 2, Row: 0, RowSpan: 2);
            slots[3] = new DashboardGridSlot(3, Column: 0, Row: 1, RowSpan: 1);
            if (count > 4)
            {
                slots[4] = new DashboardGridSlot(4, Column: 1, Row: 1, RowSpan: 1);
            }

            for (var index = 5; index < count; index++)
            {
                var after = index - 5;
                slots[index] = new DashboardGridSlot(index, Column: after % safeColumns, Row: 2 + (after / safeColumns), RowSpan: 1);
            }

            return slots;
        }

        for (var index = 0; index < count; index++)
        {
            slots[index] = new DashboardGridSlot(index, Column: index % safeColumns, Row: index / safeColumns, RowSpan: 1);
        }

        return slots;
    }

    /// <summary>
    /// Computes card frames from measured natural heights. When Needs attention
    /// spans two rows, Row 0 stays at the taller summary height and Row 1 absorbs
    /// any extra height needed by Needs attention so nothing clips or overlaps.
    /// </summary>
    public static DashboardGridArrangement Arrange(
        double availableWidth,
        IReadOnlyList<string> cardTypes,
        IReadOnlyList<double> naturalHeights,
        double minColumnWidth = 340,
        double gap = 12,
        int maxColumns = 3,
        bool evenRows = false,
        bool allowLeadingTriageSpan = false)
    {
        var count = naturalHeights.Count;
        var (columns, columnWidth) = ResolveColumns(availableWidth, count, minColumnWidth, gap, maxColumns, evenRows);
        if (count == 0)
        {
            return new DashboardGridArrangement(columns, columnWidth, 0, false, []);
        }

        var spansLeadingTriage = allowLeadingTriageSpan && CanSpanLeadingTriage(columns, cardTypes);
        var slots = PlanSlots(columns, cardTypes, spansLeadingTriage);
        var rowCount = slots.Max(s => s.Row + s.RowSpan);
        var rowHeights = new double[rowCount];

        // First measure every single-row slot so Row 0 stays at the compact summary height.
        foreach (var slot in slots)
        {
            if (slot.RowSpan == 1)
            {
                rowHeights[slot.Row] = Math.Max(rowHeights[slot.Row], Math.Max(0, naturalHeights[slot.Index]));
            }
        }

        // Let the last row of any spanning slot absorb excess height so the summary row stays compact.
        foreach (var slot in slots)
        {
            if (slot.RowSpan <= 1)
            {
                continue;
            }

            var spanned = SumRows(rowHeights, slot.Row, slot.RowSpan, gap);
            var needed = Math.Max(0, naturalHeights[slot.Index]);
            if (needed > spanned)
            {
                rowHeights[slot.Row + slot.RowSpan - 1] += needed - spanned;
            }
        }

        var rowTops = new double[rowCount];
        for (var row = 1; row < rowCount; row++)
        {
            rowTops[row] = rowTops[row - 1] + rowHeights[row - 1] + gap;
        }

        var frames = new DashboardCardFrame[count];
        for (var i = 0; i < count; i++)
        {
            var slot = slots[i];
            var x = slot.Column * (columnWidth + gap);
            var y = rowTops[slot.Row];
            var height = SumRows(rowHeights, slot.Row, slot.RowSpan, gap);
            frames[i] = new DashboardCardFrame(slot.Index, x, y, columnWidth, height);
        }

        var totalHeight = rowTops[^1] + rowHeights[^1];
        return new DashboardGridArrangement(columns, columnWidth, totalHeight, spansLeadingTriage, frames);
    }

    private static double SumRows(double[] rowHeights, int startRow, int rowSpan, double gap)
    {
        double total = 0;
        for (var offset = 0; offset < rowSpan; offset++)
        {
            if (offset > 0)
            {
                total += gap;
            }

            total += rowHeights[startRow + offset];
        }

        return total;
    }
}
