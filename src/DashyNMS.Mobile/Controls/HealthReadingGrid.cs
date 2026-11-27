namespace DashyNMS.Mobile.Controls;

/// <summary>Shares column alignment between the Health headings and recycled rows.</summary>
public sealed class HealthReadingGrid : Grid
{
    public static readonly BindableProperty ShowsColumnsProperty = BindableProperty.Create(
        nameof(ShowsColumns), typeof(bool), typeof(HealthReadingGrid), false,
        propertyChanged: (view, _, _) => ((HealthReadingGrid)view).PlaceFields());

    public bool ShowsColumns
    {
        get => (bool)GetValue(ShowsColumnsProperty);
        set => SetValue(ShowsColumnsProperty, value);
    }

    protected override void OnChildAdded(Element child)
    {
        base.OnChildAdded(child);
        if (child is View field) PlaceField(field, Children.IndexOf(field));
    }

    private void PlaceFields()
    {
        ColumnDefinitions = ShowsColumns
            ? [new(new GridLength(40, GridUnitType.Star)), new(new GridLength(16, GridUnitType.Star)),
               new(new GridLength(18, GridUnitType.Star)), new(new GridLength(26, GridUnitType.Star))]
            : [new(GridLength.Star)];
        RowDefinitions = ShowsColumns
            ? [new(GridLength.Auto)]
            : [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto)];

        for (var index = 0; index < Children.Count; index++)
            PlaceField((View)Children[index], index);
    }

    private void PlaceField(View field, int index)
    {
        Grid.SetRow((BindableObject)field, ShowsColumns ? 0 : index);
        Grid.SetColumn((BindableObject)field, ShowsColumns ? index : 0);
    }

    public HealthReadingGrid()
    {
        SafeAreaEdges = SafeAreaEdges.None;
        ColumnSpacing = 12;
        RowSpacing = 4;
        PlaceFields();
    }
}
