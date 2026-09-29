using Microsoft.Maui.Layouts;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Lays cards out in as many equal columns as fit (#65): one on a phone,
/// two or three on a tablet or in landscape - as desktop's Device View flows
/// its cards across a wide window. Each child's width is set from the
/// columns that fit <see cref="MinColumnWidth"/>, up to <see cref="MaxColumns"/>.
/// </summary>
public sealed class CardGrid : FlexLayout
{
    public static readonly BindableProperty MinColumnWidthProperty = BindableProperty.Create(
        nameof(MinColumnWidth), typeof(double), typeof(CardGrid), 340d, propertyChanged: (view, _, _) => ((CardGrid)view).Arrange());

    public static readonly BindableProperty MaxColumnsProperty = BindableProperty.Create(
        nameof(MaxColumns), typeof(int), typeof(CardGrid), 3, propertyChanged: (view, _, _) => ((CardGrid)view).Arrange());

    /// <summary>Gap between cards, across and down.</summary>
    public static readonly BindableProperty GapProperty = BindableProperty.Create(
        nameof(Gap), typeof(double), typeof(CardGrid), 8d, propertyChanged: (view, _, _) => ((CardGrid)view).Arrange());

    private int _columns = 1;

    public CardGrid()
    {
        Wrap = FlexWrap.Wrap;
        Direction = FlexDirection.Row;
        JustifyContent = FlexJustify.Start;
        AlignItems = FlexAlignItems.Stretch;
        AlignContent = FlexAlignContent.Start;
        ChildAdded += (_, e) =>
        {
            if (e.Element is View view)
            {
                view.PropertyChanged += OnChildChanged;
            }

            Arrange();
        };
        ChildRemoved += (_, e) =>
        {
            if (e.Element is View view)
            {
                view.PropertyChanged -= OnChildChanged;
            }

            Arrange();
        };
        SizeChanged += (_, _) => Arrange();
    }

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

    /// <summary>A card showing or hiding moves the ones after it along.</summary>
    private void OnChildChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsVisible))
        {
            Arrange();
        }
    }

    /// <summary>
    /// A fixed width per card rather than a percentage basis: a percentage
    /// can't take the gaps into account, so the last card in a row would
    /// wrap. Cards after the first in a row get the gap on their left; only
    /// visible cards count towards where a row starts.
    /// </summary>
    private void Arrange()
    {
        if (Width <= 0)
        {
            return;
        }

        _columns = Math.Clamp((int)((Width + Gap) / (MinColumnWidth + Gap)), 1, Math.Max(1, MaxColumns));
        var width = Math.Floor((Width - (Gap * (_columns - 1))) / _columns) - 0.5;
        var position = 0;

        foreach (var child in Children.OfType<View>())
        {
            SetGrow(child, 0);
            SetShrink(child, 0);
            child.WidthRequest = width;
            child.Margin = new Thickness(_columns > 1 && position % _columns != 0 ? Gap : 0, 0, 0, Gap);

            if (child.IsVisible)
            {
                position++;
            }
        }
    }
}
