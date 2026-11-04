using System.Windows.Input;

namespace DashyNMS.Mobile.Controls;

/// <summary>A wrapping Settings row with one native, whole-row accessible action.</summary>
public sealed class SettingsOverviewRow : Grid
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(SettingsOverviewRow), string.Empty, propertyChanged: Changed);
    public static readonly BindableProperty SummaryProperty = BindableProperty.Create(nameof(Summary), typeof(string), typeof(SettingsOverviewRow), string.Empty, propertyChanged: Changed);
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SettingsOverviewRow), null, propertyChanged: Changed);
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SettingsOverviewRow), null, propertyChanged: Changed);
    public static readonly BindableProperty SummaryTextColorProperty = BindableProperty.Create(nameof(SummaryTextColor), typeof(Color), typeof(SettingsOverviewRow), null, propertyChanged: Changed);
    public static readonly BindableProperty ShowLogoProperty = BindableProperty.Create(nameof(ShowLogo), typeof(bool), typeof(SettingsOverviewRow), false, propertyChanged: Changed);
    public static readonly BindableProperty ShowChevronProperty = BindableProperty.Create(nameof(ShowChevron), typeof(bool), typeof(SettingsOverviewRow), true, propertyChanged: Changed);
    private readonly Image _logo = new() { Source = "dashynms_logo.png", WidthRequest = 54, HeightRequest = 54, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center, InputTransparent = true, IsVisible = false };
    private readonly Label _chevron = new() { Style = (Style)Application.Current!.Resources["Chevron"], InputTransparent = true };
    private readonly Label _title = new() { FontSize = 15, FontFamily = "BodySemibold", LineBreakMode = LineBreakMode.WordWrap };
    private readonly Label _summary = new() { Style = (Style)Application.Current!.Resources["Caption"], LineBreakMode = LineBreakMode.WordWrap };
    private readonly Button _action = new() { Style = new Style(typeof(Button)), BackgroundColor = Colors.Transparent, BorderWidth = 0, Text = string.Empty, MinimumHeightRequest = 52, Padding = 0 };

    public SettingsOverviewRow()
    {
        MinimumHeightRequest = 60;
        ColumnDefinitions = [new(new GridLength(0)), new(GridLength.Star), new(GridLength.Auto)];
        ColumnSpacing = 0;
        _chevron.Margin = new Thickness(12, 0, 0, 0);
        var text = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Padding = new Thickness(0, 14), Spacing = 4, InputTransparent = true };
        text.Add(_title);
        text.Add(_summary);
        AutomationProperties.SetIsInAccessibleTree(text, false);
        AutomationProperties.SetExcludedWithChildren(text, true);
        SetColumn((IView)text, 1);
        Add(text);
        AutomationProperties.SetIsInAccessibleTree(_logo, false);
        Add(_logo);
        AutomationProperties.SetIsInAccessibleTree(_chevron, false);
        SetColumn((IView)_chevron, 2);
        Add(_chevron);
        SetColumnSpan((IView)_action, 3);
        Add(_action);
        // The native action supplies keyboard focus and press feedback without a second gesture target.
        var states = new VisualStateGroup { Name = "CommonStates" };
        foreach (var name in new[] { "Normal", "Pressed", "Focused", "Disabled" })
        {
            var state = new VisualState { Name = name };
            state.Setters.Add(new Setter { Property = Button.BackgroundColorProperty,
                Value = name is "Pressed" or "Focused" ? (Color)Application.Current!.Resources["PrimaryTint"] : Colors.Transparent });
            states.States.Add(state);
        }
        VisualStateManager.GetVisualStateGroups(_action).Add(states);
        _action.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IsEnabled))
            {
                _title.Opacity = _summary.Opacity = _action.IsEnabled ? 1 : .6;
            }
        };
    }

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>Restore the repository mark only for the active server identity.</summary>
    public bool ShowLogo { get => (bool)GetValue(ShowLogoProperty); set => SetValue(ShowLogoProperty, value); }
    /// <summary>Explanations and immediate actions do not imply navigation.</summary>
    public bool ShowChevron { get => (bool)GetValue(ShowChevronProperty); set => SetValue(ShowChevronProperty, value); }
    public string Summary { get => (string)GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }
    public Color? SummaryTextColor { get => (Color?)GetValue(SummaryTextColorProperty); set => SetValue(SummaryTextColorProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    private static void Changed(BindableObject view, object oldValue, object newValue) => ((SettingsOverviewRow)view).Update();

    private void Update()
    {
        _title.Text = Title;
        _logo.IsVisible = ShowLogo;
        _title.LineBreakMode = ShowLogo ? LineBreakMode.CharacterWrap : LineBreakMode.WordWrap;
        ColumnDefinitions[0].Width = new GridLength(ShowLogo ? 68 : 0);
        _chevron.IsVisible = ShowChevron;
        _summary.Text = Summary;
        if (SummaryTextColor is { } colour) _summary.TextColor = colour;
        else _summary.ClearValue(Label.TextColorProperty);
        _summary.IsVisible = !string.IsNullOrWhiteSpace(Summary);
        SemanticProperties.SetDescription(_action, string.Join(". ", new[] { Title, Summary }.Where(value => !string.IsNullOrWhiteSpace(value))));
        _action.CommandParameter = CommandParameter;
        _action.Command = Command;
    }
}
