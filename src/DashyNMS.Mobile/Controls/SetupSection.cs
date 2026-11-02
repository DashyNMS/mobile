namespace DashyNMS.Mobile.Controls;

/// <summary>Gives setup fields a consistent label and spacing without imposing a width cap.</summary>
[ContentProperty(nameof(Field))]
public sealed class SetupSection : VerticalStackLayout
{
    private readonly Label _label = new() { Style = (Style)Application.Current!.Resources["Caption"] };

    public SetupSection()
    {
        Spacing = 4;
        Margin = new Thickness(0, 0, 0, 8);
        Children.Add(_label);
    }

    public string Label { get => _label.Text; set => _label.Text = value; }
    public View? Field
    {
        get => Children.OfType<View>().FirstOrDefault(view => view != _label);
        set
        {
            if (Field is { } previous) Children.Remove(previous);
            if (value is not null) Children.Add(value);
        }
    }
}
