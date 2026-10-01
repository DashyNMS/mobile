using System.Windows.Input;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// The bar under a list in selection mode (#85): how many are ticked,
/// "Select all" and "Done", then the list's own actions (the child given in
/// XAML) - with "Acknowledging 3 of 14…" while one runs. Alerts and Devices
/// share it, so selecting looks the same in both.
/// </summary>
[ContentProperty(nameof(Actions))]
public sealed class BulkBar : Border
{
    public static readonly BindableProperty SummaryProperty = BindableProperty.Create(
        nameof(Summary), typeof(string), typeof(BulkBar), string.Empty,
        propertyChanged: (view, _, value) => ((BulkBar)view)._summary.Text = value as string);

    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress), typeof(string), typeof(BulkBar), null,
        propertyChanged: (view, _, value) => ((BulkBar)view).ShowProgress(value as string));

    public static readonly BindableProperty SelectAllTextProperty = BindableProperty.Create(
        nameof(SelectAllText), typeof(string), typeof(BulkBar), "Select all",
        propertyChanged: (view, _, value) => ((BulkBar)view)._selectAll.Text = value as string);

    public static readonly BindableProperty SelectAllCommandProperty = BindableProperty.Create(
        nameof(SelectAllCommand), typeof(ICommand), typeof(BulkBar), null,
        propertyChanged: (view, _, value) => ((BulkBar)view)._selectAll.Command = value as ICommand);

    public static readonly BindableProperty DoneCommandProperty = BindableProperty.Create(
        nameof(DoneCommand), typeof(ICommand), typeof(BulkBar), null,
        propertyChanged: (view, _, value) => ((BulkBar)view)._done.Command = value as ICommand);

    private readonly Label _summary = new() { FontFamily = "BodySemibold", VerticalOptions = LayoutOptions.Center };
    private readonly Label _progress = new() { IsVisible = false };
    private readonly Button _selectAll = new() { Text = "Select all" };
    private readonly Button _done = new() { Text = "Done" };
    private readonly VerticalStackLayout _stack = new() { Spacing = 8 };
    private View? _actions;

    public BulkBar()
    {
        if (Application.Current?.Resources is { } resources)
        {
            Style = resources.TryGetValue("Card", out var card) ? card as Style : null;
            var link = resources.TryGetValue("LinkButton", out var linkStyle) ? linkStyle as Style : null;
            _selectAll.Style = link;
            _done.Style = link;
            _progress.Style = resources.TryGetValue("Caption", out var caption) ? caption as Style : null;
        }

        Padding = new Thickness(12, 8, 12, 12);
        Margin = new Thickness(0, 0, 0, 12);

        var header = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
        };
        header.Add(_summary);
        header.Add(_selectAll, column: 1);
        header.Add(_done, column: 2);

        _stack.Add(header);
        _stack.Add(_progress);
        Content = _stack;
    }

    /// <summary>"3 selected".</summary>
    public string Summary
    {
        get => (string)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    /// <summary>"Acknowledging 3 of 14…" while an action runs; null otherwise.</summary>
    public string? Progress
    {
        get => (string?)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public string SelectAllText
    {
        get => (string)GetValue(SelectAllTextProperty);
        set => SetValue(SelectAllTextProperty, value);
    }

    public ICommand? SelectAllCommand
    {
        get => (ICommand?)GetValue(SelectAllCommandProperty);
        set => SetValue(SelectAllCommandProperty, value);
    }

    public ICommand? DoneCommand
    {
        get => (ICommand?)GetValue(DoneCommandProperty);
        set => SetValue(DoneCommandProperty, value);
    }

    /// <summary>The list's own buttons: Acknowledge, Pin, Maintenance.</summary>
    public View? Actions
    {
        get => _actions;
        set
        {
            if (_actions is not null)
            {
                _stack.Remove(_actions);
            }

            _actions = value;
            if (value is not null)
            {
                _stack.Add(value);
            }
        }
    }

    private void ShowProgress(string? text)
    {
        _progress.Text = text;
        _progress.IsVisible = !string.IsNullOrEmpty(text);
    }
}
