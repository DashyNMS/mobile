namespace DashyNMS.Mobile.Controls;

/// <summary>
/// A web view for one of <see cref="DeviceSections.GraphHtml"/>'s graph pages:
/// it takes the page as a string, keeps LibreNMS's shape as it scales to the
/// width, and never navigates away from its own page, whatever the
/// (untrusted) graph contains.
/// </summary>
public sealed class GraphView : WebView
{
    /// <summary>LibreNMS's graphs are drawn at 800x400, so half as tall as wide, plus the legend.</summary>
    private const double AspectRatio = 0.62;

    public static readonly BindableProperty HtmlProperty = BindableProperty.Create(
        nameof(Html), typeof(string), typeof(GraphView), propertyChanged: (view, _, value) =>
            ((GraphView)view).Source = value is string html ? new HtmlWebViewSource { Html = html } : null);

    public GraphView()
    {
        BackgroundColor = Colors.Transparent;
        VerticalOptions = LayoutOptions.Start;
        SizeChanged += (_, _) =>
        {
            if (Width > 0)
            {
                HeightRequest = Width * AspectRatio;
            }
        };
        Navigating += (_, e) =>
        {
            if (!IsOwnPage(e.Url))
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

    /// <summary>
    /// Loading an <see cref="HtmlWebViewSource"/> can itself raise Navigating,
    /// with a blank, data: or local file: address. Anything else - http(s),
    /// tel:, a custom scheme - is a way out of the page.
    /// </summary>
    private static bool IsOwnPage(string? url) =>
        string.IsNullOrEmpty(url)
        || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
}
