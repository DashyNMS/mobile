namespace DashyNMS.Mobile.Controls;

/// <summary>
/// A device's logo beside its name (#348): a small web view for
/// <see cref="ViewModels.DeviceLogoHtml"/>'s page, since MAUI's Image can't
/// draw an SVG at run time. It never navigates away from its own page and
/// takes no touches, so taps fall through to the header beneath.
/// </summary>
public sealed class LogoView : WebView
{
    public static readonly BindableProperty HtmlProperty = BindableProperty.Create(
        nameof(Html), typeof(string), typeof(LogoView), propertyChanged: (view, _, value) =>
            ((LogoView)view).Source = value is string html ? new HtmlWebViewSource { Html = html } : null);

    public LogoView()
    {
        BackgroundColor = Colors.Transparent;
        InputTransparent = true;
        Navigating += (_, e) =>
        {
            if (!GraphView.IsOwnPage(e.Url))
            {
                e.Cancel = true;
            }
        };
    }

    public string? Html
    {
        get => (string?)GetValue(HtmlProperty);
        set => SetValue(HtmlProperty, value);
    }
}
