using DashyNMS.Mobile.Services;
using Microsoft.Maui.Layouts;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Keeps a text-heavy page - Settings, Alert detail, Sign in - to a
/// readable width, centred, on an iPad or a Mac (#88); on a phone it's the
/// full width as before. Its one child is the page's content.
/// </summary>
/// <remarks>
/// Its own layout so the content is the full width up to the limit and
/// centred beyond it: MaximumWidthRequest caps the width but doesn't centre,
/// and centring a stack sizes it to its content instead.
/// </remarks>
public sealed class ReadableColumn : Layout
{
    public static readonly BindableProperty MaxContentWidthProperty = BindableProperty.Create(
        nameof(MaxContentWidth), typeof(double), typeof(ReadableColumn), ScreenLayout.ReadableWidth,
        propertyChanged: (view, _, _) => ((ReadableColumn)view).InvalidateMeasure());

    public double MaxContentWidth
    {
        get => (double)GetValue(MaxContentWidthProperty);
        set => SetValue(MaxContentWidthProperty, value);
    }

    protected override ILayoutManager CreateLayoutManager() => new ReadableColumnManager(this);

    private sealed class ReadableColumnManager(ReadableColumn column) : LayoutManager(column)
    {
        public override Size Measure(double widthConstraint, double heightConstraint)
        {
            var padding = column.Padding;
            var available = widthConstraint - padding.HorizontalThickness;
            var width = Math.Min(available, column.MaxContentWidth);
            var height = 0d;
            foreach (var child in Visible())
            {
                height = Math.Max(height, child.Measure(width, heightConstraint - padding.VerticalThickness).Height);
            }

            var measuredWidth = double.IsInfinity(widthConstraint) ? width + padding.HorizontalThickness : widthConstraint;
            return new Size(measuredWidth, height + padding.VerticalThickness);
        }

        public override Size ArrangeChildren(Rect bounds)
        {
            var padding = column.Padding;
            var available = bounds.Width - padding.HorizontalThickness;
            var width = Math.Min(available, column.MaxContentWidth);
            var x = bounds.Left + padding.Left + ((available - width) / 2);
            foreach (var child in Visible())
            {
                child.Arrange(new Rect(x, bounds.Top + padding.Top, width, bounds.Height - padding.VerticalThickness));
            }

            return bounds.Size;
        }

        private IEnumerable<IView> Visible() => column.Where(child => child.Visibility != Visibility.Collapsed);
    }
}
