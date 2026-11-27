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
        nameof(Html), typeof(string), typeof(GraphView), propertyChanged: (view, oldValue, value) =>
        {
            if (string.Equals(oldValue as string, value as string, StringComparison.Ordinal))
            {
                return;
            }

            ((GraphView)view).ApplyHtml(value as string);
        });

    public GraphView()
    {
        BackgroundColor = Colors.Transparent;
        VerticalOptions = LayoutOptions.Start;

        // A height before the first layout: a web view with none measures as
        // nothing, and in Device View's card grid it stayed that way - the
        // ping graph drew blank. The width sets the real height from here.
        HeightRequest = 200;
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
#if IOS
        HandlerChanged += (_, _) => ConfigurePlatformView();
        Loaded += (_, _) => ConfigurePlatformView();
#endif
    }

    public string? Html
    {
        get => (string?)GetValue(HtmlProperty);
        set => SetValue(HtmlProperty, value);
    }

    private void ApplyHtml(string? html)
    {
#if IOS
        ConfigurePlatformView();
#endif
        Source = html is not null ? new HtmlWebViewSource { Html = html } : null;
    }

#if IOS
    /// <summary>
    /// Keeps the native <see cref="WebKit.WKWebView"/> transparent and non-opaque before
    /// first navigation and across theme reloads so Dark mode never flashes white (#703).
    /// </summary>
    internal static void ConfigureNative(WebView webView)
    {
        if (webView.Handler?.PlatformView is not WebKit.WKWebView web)
        {
            return;
        }

        web.Opaque = false;
        web.BackgroundColor = UIKit.UIColor.Clear;
        web.ScrollView.BackgroundColor = UIKit.UIColor.Clear;
        web.ScrollView.ScrollEnabled = false;
        web.ScrollView.Bounces = false;
    }

    private void ConfigurePlatformView() => ConfigureNative(this);
#endif

    /// <summary>
    /// Loading an <see cref="HtmlWebViewSource"/> can itself raise Navigating,
    /// with a blank, data: or local file: address. Anything else - http(s),
    /// tel:, a custom scheme - is a way out of the page.
    /// </summary>
    internal static bool IsOwnPage(string? url) =>
        string.IsNullOrEmpty(url)
        || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
}
