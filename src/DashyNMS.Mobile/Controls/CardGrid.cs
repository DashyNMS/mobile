using Microsoft.Maui.Layouts;

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

    protected override ILayoutManager CreateLayoutManager() => new CardGridManager(this);

    private static void Invalidate(BindableObject view, object oldValue, object newValue) =>
        ((CardGrid)view).InvalidateMeasure();

    /// <summary>How many columns fit <paramref name="width"/>, and how wide each is.</summary>
    private (int Columns, double ColumnWidth) Columns(double width)
    {
        var columns = Math.Clamp((int)((width + Gap) / (MinColumnWidth + Gap)), 1, Math.Max(1, MaxColumns));
        return (columns, Math.Max(0, (width - (Gap * (columns - 1))) / columns));
    }

    /// <summary>Measures each row as its tallest card, then places the cards in them.</summary>
    private sealed class CardGridManager(CardGrid grid) : LayoutManager(grid)
    {
        private readonly List<double> _rowHeights = [];

        public override Size Measure(double widthConstraint, double heightConstraint)
        {
            var padding = grid.Padding;
            var width = double.IsInfinity(widthConstraint)
                ? grid.MinColumnWidth
                : widthConstraint - padding.HorizontalThickness;
            var (columns, columnWidth) = grid.Columns(width);

            _rowHeights.Clear();
            var index = 0;
            foreach (var child in Visible())
            {
                var size = child.Measure(columnWidth, double.PositiveInfinity);
                if (index % columns == 0)
                {
                    _rowHeights.Add(0);
                }

                _rowHeights[^1] = Math.Max(_rowHeights[^1], size.Height);
                index++;
            }

            var height = _rowHeights.Sum() + (grid.Gap * Math.Max(0, _rowHeights.Count - 1)) + padding.VerticalThickness;
            return new Size(width + padding.HorizontalThickness, height);
        }

        public override Size ArrangeChildren(Rect bounds)
        {
            var padding = grid.Padding;
            var (columns, columnWidth) = grid.Columns(bounds.Width - padding.HorizontalThickness);
            var y = bounds.Top + padding.Top;
            var index = 0;
            foreach (var child in Visible())
            {
                var row = index / columns;
                var column = index % columns;
                if (column == 0 && row > 0)
                {
                    y += RowHeight(row - 1) + grid.Gap;
                }

                var x = bounds.Left + padding.Left + (column * (columnWidth + grid.Gap));
                child.Arrange(new Rect(x, y, columnWidth, Math.Max(RowHeight(row), child.DesiredSize.Height)));
                index++;
            }

            return bounds.Size;
        }

        private double RowHeight(int row) => row < _rowHeights.Count ? _rowHeights[row] : 0;

        private IEnumerable<IView> Visible() => grid.Where(child => child.Visibility != Visibility.Collapsed);
    }
}
