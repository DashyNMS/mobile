using DashyNMS.Mobile.Search;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// An address or MAC in a Network Search result (#129), with a 44pt button to
/// copy it and one to search for it, each named for its value.
/// </summary>
public sealed class CopyableRow : Grid
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(CopyableValueViewModel), typeof(CopyableRow), null, propertyChanged: Changed);
    public static readonly BindableProperty DetailProperty = BindableProperty.Create(nameof(Detail), typeof(string), typeof(CopyableRow), null, propertyChanged: Changed);
    public static readonly BindableProperty NoteProperty = BindableProperty.Create(nameof(Note), typeof(string), typeof(CopyableRow), null, propertyChanged: Changed);

    private readonly Label _value = new() { FontFamily = "Mono", FontSize = 14, LineBreakMode = LineBreakMode.CharacterWrap };
    private readonly Label _detail = new() { Style = (Style)Application.Current!.Resources["Caption"], LineBreakMode = LineBreakMode.WordWrap };
    private readonly Label _note = new() { Style = (Style)Application.Current!.Resources["Faint"], LineBreakMode = LineBreakMode.WordWrap };
    private readonly IconButton _copy = new() { Kind = IconButtonKind.Plain, Icon = Icons.Copy };
    private readonly IconButton _search = new() { Kind = IconButtonKind.Plain, Icon = Icons.Search };

    public CopyableRow()
    {
        ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto)];
        Padding = new Thickness(14, 8, 6, 8);
        MinimumHeightRequest = 56;
        var line = new BoxView { Style = (Style)Application.Current!.Resources["RowLine"], VerticalOptions = LayoutOptions.Start, Margin = new Thickness(-14, -8, -6, 0) };
        AutomationProperties.SetIsInAccessibleTree(line, false);
        SetColumnSpan((IView)line, 3);
        Add(line);
        var text = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { _value, _detail, _note } };
        Add(text);
        SetColumn((IView)_copy, 1);
        Add(_copy);
        SetColumn((IView)_search, 2);
        Add(_search);
    }

    public CopyableValueViewModel? Value { get => (CopyableValueViewModel?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>A MAC's vendor.</summary>
    public string? Detail { get => (string?)GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    /// <summary>When it was last seen, or that it's stale.</summary>
    public string? Note { get => (string?)GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    private static void Changed(BindableObject view, object oldValue, object newValue) => ((CopyableRow)view).Update();

    private void Update()
    {
        _value.Text = Value?.Value;
        _detail.Text = Detail;
        _detail.IsVisible = !string.IsNullOrEmpty(Detail);
        _note.Text = Note;
        _note.IsVisible = !string.IsNullOrEmpty(Note);
        _copy.Command = Value?.CopyCommand;
        _search.Command = Value?.SearchCommand;
        SemanticProperties.SetDescription(_copy, "Copy " + Value?.Value);
        SemanticProperties.SetDescription(_search, "Search for " + Value?.Value);
        SemanticProperties.SetDescription(_value, string.Join(", ", new[] { Value?.Value, Detail, Note }.Where(part => !string.IsNullOrEmpty(part))));
        AutomationProperties.SetIsInAccessibleTree(_detail, false);
        AutomationProperties.SetIsInAccessibleTree(_note, false);
    }
}
