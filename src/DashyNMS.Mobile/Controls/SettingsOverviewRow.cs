using System.Windows.Input;

namespace DashyNMS.Mobile.Controls;

/// <summary>A wrapping Settings row with one native, whole-row accessible action.</summary>
public sealed class SettingsOverviewRow : Grid
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(SettingsOverviewRow), string.Empty, propertyChanged: Changed);
    public static readonly BindableProperty SummaryProperty = BindableProperty.Create(nameof(Summary), typeof(string), typeof(SettingsOverviewRow), string.Empty, propertyChanged: Changed);
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SettingsOverviewRow), null, propertyChanged: Changed);
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SettingsOverviewRow), null, propertyChanged: Changed);
    private readonly Label _title = new() { FontSize = 15, FontFamily = "BodySemibold", LineBreakMode = LineBreakMode.WordWrap };
    private readonly Label _summary = new() { Style = (Style)Application.Current!.Resources["Caption"], LineBreakMode = LineBreakMode.WordWrap };
    private readonly Button _action = new() { Style = new Style(typeof(Button)), BackgroundColor = Colors.Transparent, BorderWidth = 0, Text = string.Empty, MinimumHeightRequest = 52, Padding = 0 };

    public SettingsOverviewRow()
    {
        MinimumHeightRequest = 52;
        ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)];
        ColumnSpacing = 12;
        var text = new VerticalStackLayout { Padding = new Thickness(0, 10), Spacing = 4, InputTransparent = true };
        text.Add(_title);
        text.Add(_summary);
        AutomationProperties.SetIsInAccessibleTree(text, false);
        AutomationProperties.SetExcludedWithChildren(text, true);
        Add(text);
        var chevron = new Label { Style = (Style)Application.Current!.Resources["Chevron"], InputTransparent = true };
        AutomationProperties.SetIsInAccessibleTree(chevron, false);
        SetColumn(chevron, 1);
        Add(chevron);
        SetColumnSpan(_action, 2);
        Add(_action);
        _action.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IsEnabled))
            {
                _title.Opacity = _summary.Opacity = _action.IsEnabled ? 1 : .6;
            }
        };
    }

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Summary { get => (string)GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    private static void Changed(BindableObject view, object oldValue, object newValue) => ((SettingsOverviewRow)view).Update();

    private void Update()
    {
        _title.Text = Title;
        _summary.Text = Summary;
        _summary.IsVisible = !string.IsNullOrWhiteSpace(Summary);
        SemanticProperties.SetDescription(_action, string.Join(". ", new[] { Title, Summary }.Where(value => !string.IsNullOrWhiteSpace(value))));
        _action.CommandParameter = CommandParameter;
        _action.Command = Command;
    }
}
