using Microsoft.Maui.Layouts;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Lays cards out in as many equal columns as fit (#65): one on a phone,
/// two or three on a tablet or in landscape - as desktop's Device View flows
/// its cards across a wide window. Columns are as many as fit
/// <see cref="MinColumnWidth"/>, up to <see cref="MaxColumns"/>; each row is
/// as tall as its tallest card, and hidden cards take no place.
/// </summary>
/// <remarks>
/// Its own layout rather than a wrapping FlexLayout: inside the page's
/// scrolling stack, FlexLayout measured itself with no height, so on a
/// phone Device View showed none of its cards but the Device one (#69).
/// </remarks>
public sealed class CardGrid : Layout
{
    public static readonly BindableProperty MinColumnWidthProperty = BindableProperty.Create(
        nameof(MinColumnWidth), typeof(double), typeof(CardGrid), 340d, propertyChanged: Invalidate);

    public static readonly BindableProperty MaxColumnsProperty = BindableProperty.Create(
        nameof(MaxColumns), typeof(int), typeof(CardGrid), 3, propertyChanged: Invalidate);

    /// <summary>Gap between cards, across and down.</summary>
    public static readonly BindableProperty GapProperty = BindableProperty.Create(
        nameof(Gap), typeof(double), typeof(CardGrid), 8d, propertyChanged: Invalidate);

    /// <summary>
    /// Only as many columns as fill every row: four tiles go one, two or four
    /// across, never three with the last alone beneath them (#347).
    /// </summary>
    public static readonly BindableProperty EvenRowsProperty = BindableProperty.Create(
        nameof(EvenRows), typeof(bool), typeof(CardGrid), false, propertyChanged: Invalidate);

    /// <summary>
    /// On three-column layouts whose leading cards are Alerts, Devices and Needs
    /// attention, spans Needs attention across two rows so the next cards sit
    /// directly beneath the compact summaries (#709).
    /// </summary>
    public static readonly BindableProperty SpanLeadingTriageProperty = BindableProperty.Create(
        nameof(SpanLeadingTriage), typeof(bool), typeof(CardGrid), false, propertyChanged: Invalidate);

    public double MinColumnWidth
    {
        get => (double)GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    public int MaxColumns
    {
        get => (int)GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public bool EvenRows
    {
        get => (bool)GetValue(EvenRowsProperty);
        set => SetValue(EvenRowsProperty, value);
    }

    public bool SpanLeadingTriage
    {
        get => (bool)GetValue(SpanLeadingTriageProperty);
        set => SetValue(SpanLeadingTriageProperty, value);
    }

    protected override ILayoutManager CreateLayoutManager() => new CardGridManager(this);

    private static void Invalidate(BindableObject view, object oldValue, object newValue) =>
        ((CardGrid)view).InvalidateMeasure();

    /// <summary>Measures each row as its tallest card, then places the cards in them.</summary>
    private sealed class CardGridManager(CardGrid grid) : LayoutManager(grid)
    {
        private DashboardGridArrangement? _arrangement;
        private double _measuredWidth = double.NaN;

        public override Size Measure(double widthConstraint, double heightConstraint)
        {
            var padding = grid.Padding;
            var width = double.IsInfinity(widthConstraint)
                ? grid.MinColumnWidth
                : widthConstraint - padding.HorizontalThickness;
            var visible = Visible().ToList();
            var (_, columnWidth) = DashboardGridLayout.ResolveColumns(
                width, visible.Count, grid.MinColumnWidth, grid.Gap, grid.MaxColumns, grid.EvenRows);

            var cardTypes = new string[visible.Count];
            var naturalHeights = new double[visible.Count];
            for (var index = 0; index < visible.Count; index++)
            {
                cardTypes[index] = CardTypeOf(visible[index]);
                naturalHeights[index] = visible[index].Measure(columnWidth, double.PositiveInfinity).Height;
            }

            _measuredWidth = width;
            _arrangement = DashboardGridLayout.Arrange(
                width,
                cardTypes,
                naturalHeights,
                grid.MinColumnWidth,
                grid.Gap,
                grid.MaxColumns,
                grid.EvenRows,
                grid.SpanLeadingTriage);

            if (_arrangement.Columns > 1)
            {
                foreach (var frame in _arrangement.Frames)
                {
                    if (visible[frame.Index].DesiredSize.Height < frame.Height)
                    {
                        visible[frame.Index].Measure(frame.Width, frame.Height);
                    }
                }
            }

            return new Size(width + padding.HorizontalThickness, _arrangement.TotalHeight + padding.VerticalThickness);
        }

        public override Size ArrangeChildren(Rect bounds)
        {
            var padding = grid.Padding;
            var visible = Visible().ToList();
            var availableWidth = bounds.Width - padding.HorizontalThickness;
            var arrangement = _arrangement;
            if (arrangement is null
                || arrangement.Frames.Count != visible.Count
                || Math.Abs(availableWidth - _measuredWidth) > 0.5)
            {
                var cardTypes = visible.Select(CardTypeOf).ToArray();
                var heights = visible.Select(v => v.DesiredSize.Height).ToArray();
                arrangement = DashboardGridLayout.Arrange(
                    availableWidth,
                    cardTypes,
                    heights,
                    grid.MinColumnWidth,
                    grid.Gap,
                    grid.MaxColumns,
                    grid.EvenRows,
                    grid.SpanLeadingTriage);
            }

            foreach (var frame in arrangement.Frames)
            {
                var child = visible[frame.Index];
                var x = bounds.Left + padding.Left + frame.X;
                var y = bounds.Top + padding.Top + frame.Y;
                child.Arrange(new Rect(x, y, frame.Width, Math.Max(frame.Height, child.DesiredSize.Height)));
            }

#if IOS
            SyncAccessibilityGrouping(visible);
#endif
            return bounds.Size;
        }

        private static string CardTypeOf(IView view) =>
            view is BindableObject { BindingContext: DashboardCard card } ? card.Type : string.Empty;

        private IEnumerable<IView> Visible() => grid.Where(child => child.Visibility != Visibility.Collapsed);

#if IOS
        /// <summary>
        /// Groups each card's accessibility children so VoiceOver reads each card
        /// completely in rendered placement order instead of interleaving adjacent
        /// columns beside a two-row spanning card (#709).
        /// </summary>
        private void SyncAccessibilityGrouping(IReadOnlyList<IView> visible)
        {
            if (grid.Handler?.PlatformView is UIKit.UIView container)
            {
                container.ShouldGroupAccessibilityChildren = true;
            }

            foreach (var child in visible)
            {
                if (child.Handler?.PlatformView is UIKit.UIView cardView)
                {
                    cardView.ShouldGroupAccessibilityChildren = true;
                }
            }
        }
#endif
    }
}
