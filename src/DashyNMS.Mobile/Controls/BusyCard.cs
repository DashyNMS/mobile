namespace DashyNMS.Mobile.Controls;

/// <summary>
/// The logo's heartbeat (#100) over a line saying what's happening, on a
/// small card centred over whatever it covers - the network map's
/// "Laying out the map…" (#109), which was the platform's plain spinner,
/// off-centre above its caption. One unit, so the two always line up.
/// </summary>
public sealed class BusyCard : Border
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(BusyCard), "Loading…",
        propertyChanged: (view, _, value) => ((BusyCard)view).OnTextChanged((string)value));

    private readonly HeartbeatLogo _logo = new() { WidthRequest = 40, HeightRequest = 40, HorizontalOptions = LayoutOptions.Center };
    private readonly Label _label = new() { HorizontalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.Center };

    public BusyCard()
    {
        if (Application.Current?.Resources.TryGetValue("Card", out var card) == true && card is Style cardStyle)
        {
            Style = cardStyle;
        }

        if (Application.Current?.Resources.TryGetValue("Caption", out var caption) == true && caption is Style captionStyle)
        {
            _label.Style = captionStyle;
        }

        Padding = new Thickness(20, 16);
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;
        InputTransparent = true;
        _label.Text = Text;

        // It beats while it shows.
        _logo.SetBinding(HeartbeatLogo.IsBeatingProperty, new Binding(nameof(IsVisible), source: this));
        Content = new VerticalStackLayout { Spacing = 10, Children = { _logo, _label } };
        SemanticProperties.SetDescription(this, Text);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void OnTextChanged(string text)
    {
        _label.Text = text;
        SemanticProperties.SetDescription(this, text);
    }
}
