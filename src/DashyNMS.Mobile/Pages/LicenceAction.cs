using System.Windows.Input;

namespace DashyNMS.Mobile.Pages;

/// <summary>Measures wrapping text separately from its whole-row native action.</summary>
public sealed class LicenceAction : Grid
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(LicenceAction), string.Empty, propertyChanged: Changed);
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(LicenceAction), null, propertyChanged: Changed);
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(LicenceAction), null, propertyChanged: Changed);
    private readonly Label _text = new() { Style = (Style)Application.Current!.Resources["Caption"], LineBreakMode = LineBreakMode.CharacterWrap, InputTransparent = true, Padding = new Thickness(0, 12) };
    private readonly Button _action = new() { Style = new Style(typeof(Button)), Text = string.Empty, BackgroundColor = Colors.Transparent, Padding = 0, MinimumHeightRequest = 44 };

    public LicenceAction()
    {
        MinimumHeightRequest = 44;
        AutomationProperties.SetIsInAccessibleTree(_text, false);
        Add(_text);
        Add(_action);
    }

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    private static void Changed(BindableObject view, object oldValue, object newValue)
    {
        var row = (LicenceAction)view;
        row._text.Text = row.Text;
        SemanticProperties.SetDescription(row._action, row.Text);
        row._action.CommandParameter = row.CommandParameter;
        row._action.Command = row.Command;
    }
}
