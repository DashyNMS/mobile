using System.Collections;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;

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

    /// <summary>Hides a <see cref="CollectionView"/> row's leading hairline when its item is row 0.</summary>
    public static readonly BindableProperty HideOnFirstProperty = BindableProperty.CreateAttached(
        "HideOnFirst", typeof(bool), typeof(RowLines), false, propertyChanged: OnHideOnFirstChanged);

    public static bool GetHideOnFirst(BindableObject view) => (bool)view.GetValue(HideOnFirstProperty);

    public static void SetHideOnFirst(BindableObject view, bool value) => view.SetValue(HideOnFirstProperty, value);

    private static readonly ConditionalWeakTable<VisualElement, FirstRowWatcher> Watchers = new();

    private static void OnHideOnFirstChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is VisualElement line && newValue is true)
        {
            Watchers.GetValue(line, static v => new FirstRowWatcher(v));
        }
    }

    private sealed class FirstRowWatcher
    {
        private readonly VisualElement _line;
        private CollectionView? _list;
        private INotifyCollectionChanged? _source;

        public FirstRowWatcher(VisualElement line)
        {
            _line = line;
            line.Loaded += (_, _) => Attach();
            line.Unloaded += (_, _) => Detach();
            line.BindingContextChanged += (_, _) => Refresh();
            if (line.IsLoaded) Attach();
        }

        private void Attach()
        {
            var list = FindCollectionView(_line);
            if (!ReferenceEquals(_list, list))
            {
                if (_list is not null) _list.PropertyChanged -= OnListPropertyChanged;
                _list = list;
                if (_list is not null) _list.PropertyChanged += OnListPropertyChanged;
            }
            BindSource();
            Refresh();
        }

        private void Detach()
        {
            if (_source is not null) _source.CollectionChanged -= OnCollectionChanged;
            _source = null;
            if (_list is not null) _list.PropertyChanged -= OnListPropertyChanged;
            _list = null;
        }

        private void OnListPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == ItemsView.ItemsSourceProperty.PropertyName)
            {
                BindSource();
                Refresh();
            }
        }

        private void BindSource()
        {
            var next = _list?.ItemsSource as INotifyCollectionChanged;
            if (ReferenceEquals(_source, next)) return;
            if (_source is not null) _source.CollectionChanged -= OnCollectionChanged;
            _source = next;
            if (_source is not null) _source.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

        private void Refresh()
        {
            var item = _line.BindingContext;
            if (item is null || _list?.ItemsSource is null)
            {
                _line.IsVisible = true;
                return;
            }
            var first = FirstItem(_list.ItemsSource, _list.IsGrouped);
            var isFirst = first is not null && ReferenceEquals(first, item);
            _line.IsVisible = !isFirst;
        }

        private static CollectionView? FindCollectionView(Element element)
        {
            var current = element.Parent;
            for (var depth = 0; depth < 12 && current is not null; depth++)
            {
                if (current is CollectionView list) return list;
                current = current.Parent;
            }
            return null;
        }

        private static object? FirstItem(IEnumerable source, bool isGrouped)
        {
            var first = FirstOf(source);
            return isGrouped && first is IEnumerable group ? FirstOf(group) : first;
        }

        private static object? FirstOf(IEnumerable source)
        {
            if (source is IList list) return list.Count > 0 ? list[0] : null;
            var enumerator = source.GetEnumerator();
            try { return enumerator.MoveNext() ? enumerator.Current : null; }
            finally { (enumerator as IDisposable)?.Dispose(); }
        }
    }

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
