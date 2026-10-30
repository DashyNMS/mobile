namespace DashyNMS.Mobile.Controls;

/// <summary>
/// One rule for the lines between rows in a card (#120): a hairline between
/// every pair of rows, none above the first or after the last, whatever kind
/// of row it is. Set <c>controls:RowLines.Between="True"</c> on the layout a
/// list's rows are bound into; each row's template starts with a
/// <see cref="RowDivider"/>, and this shows it on every row but the first.
/// </summary>
/// <remarks>
/// The row can't tell for itself whether it's first - the same
/// <c>SectionRow</c> is the first in one card and the fifth in another, which
/// is how the lines came to depend on what kind of row came next - so the
/// layout decides, whenever its rows change.
/// </remarks>
public static class RowLines
{
    public static readonly BindableProperty BetweenProperty = BindableProperty.CreateAttached(
        "Between", typeof(bool), typeof(RowLines), false, propertyChanged: OnBetweenChanged);

    public static bool GetBetween(BindableObject view) => (bool)view.GetValue(BetweenProperty);

    public static void SetBetween(BindableObject view, bool value) => view.SetValue(BetweenProperty, value);

    private static void OnBetweenChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not Layout layout)
        {
            return;
        }

        layout.ChildAdded -= OnChildrenChanged;
        layout.ChildRemoved -= OnChildrenChanged;
        if (newValue is true)
        {
            layout.ChildAdded += OnChildrenChanged;
            layout.ChildRemoved += OnChildrenChanged;
            Apply(layout);
        }
    }

    private static void OnChildrenChanged(object? sender, ElementEventArgs e)
    {
        if (sender is Layout layout)
        {
            Apply(layout);
        }
    }

    private static void Apply(Layout layout)
    {
        var first = true;
        foreach (var child in layout.Children)
        {
            if (Divider(child) is { } line)
            {
                line.IsVisible = !first;
            }

            // A row that isn't showing doesn't count as the first.
            if (child is VisualElement { IsVisible: true })
            {
                first = false;
            }
        }
    }

    /// <summary>The row's own leading line: the first child of its root layout, if it's one.</summary>
    private static RowDivider? Divider(IView row) =>
        row is Layout { Children.Count: > 0 } root && root.Children[0] is RowDivider line ? line : null;
}

/// <summary>A row's leading line in a card - the app's divider, hidden until <see cref="RowLines"/> says it isn't the first row.</summary>
public sealed class RowDivider : BoxView
{
    public RowDivider()
    {
        if (Application.Current?.Resources.TryGetValue("Divider", out var style) == true && style is Style divider)
        {
            Style = divider;
        }

        IsVisible = false;
    }
}
