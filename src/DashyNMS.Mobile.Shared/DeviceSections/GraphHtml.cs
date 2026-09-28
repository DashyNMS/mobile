using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DashyNMS.Mobile.DeviceSections;

/// <summary>
/// Wraps a LibreNMS (rrdtool) SVG graph in a page a web view can show,
/// fitted to the screen's width and readable in dark mode.
/// </summary>
/// <remarks>
/// The SVG is whatever the server sent, so it's treated as untrusted: it's
/// shown as an image (a data: URI in an &lt;img&gt;), never inlined as markup.
/// Browsers run no script and fetch nothing for an SVG shown as an image, so
/// a hostile or tampered graph can't script the web view or load anything.
/// The page's Content-Security-Policy backs that up by allowing nothing but
/// data: images and its own inline style.
/// </remarks>
public static partial class GraphHtml
{
    /// <summary>rrdtool's black text and axes - what desktop's GraphSvgTheming recolours.</summary>
    private const string BlackFill = "fill=\"rgb(0%, 0%, 0%)\"";

    /// <summary>Desktop's dark-palette text colour, #E6EAF0, as rrdtool writes colours.</summary>
    private const string DarkText = "fill=\"rgb(90.2%, 91.8%, 94.1%)\"";

    private const string SvgNamespace = "http://www.w3.org/2000/svg";

    /// <summary>Nothing but data: images and the page's own style - no script, no network.</summary>
    internal const string ContentSecurityPolicy = "default-src 'none'; img-src data:; style-src 'unsafe-inline'";

    public static string Build(string svg, bool dark)
    {
        ArgumentNullException.ThrowIfNull(svg);

        var themed = dark ? svg.Replace(BlackFill, DarkText, StringComparison.Ordinal) : svg;
        var image = Convert.ToBase64String(Encoding.UTF8.GetBytes(WithNamespace(MakeScalable(themed))));
        var background = dark ? "#11141A" : "#FFFFFF";

        return $$"""
            <!DOCTYPE html>
            <html><head>
            <meta http-equiv="Content-Security-Policy" content="{{ContentSecurityPolicy}}">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <style>
              html, body { margin: 0; padding: 0; background: {{background}}; }
              img { display: block; width: 100%; height: auto; }
            </style>
            </head><body><img alt="" src="data:image/svg+xml;base64,{{image}}"></body></html>
            """;
    }

    /// <summary>The SVG inside a page from <see cref="Build"/> - for tests.</summary>
    internal static string? ImageOf(string page)
    {
        var match = ImageSource().Match(page);
        return match.Success ? Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups[1].Value)) : null;
    }

    /// <summary>
    /// An SVG shown as an image must declare its namespace or it doesn't draw
    /// at all (inline, the HTML parser forgave it). rrdtool always writes it;
    /// this covers anything that doesn't.
    /// </summary>
    internal static string WithNamespace(string svg)
    {
        var root = RootTag().Match(svg);
        if (!root.Success || root.Value.Contains("xmlns=", StringComparison.Ordinal))
        {
            return svg;
        }

        var tag = root.Value.Insert(4, $" xmlns=\"{SvgNamespace}\"");
        return svg[..root.Index] + tag + svg[(root.Index + root.Length)..];
    }

    /// <summary>
    /// Gives the root &lt;svg&gt; a viewBox from its width and height (rrdtool
    /// writes sizes in points, and no viewBox), so CSS can scale it to fit
    /// rather than cropping it.
    /// </summary>
    internal static string MakeScalable(string svg)
    {
        var root = RootTag().Match(svg);
        if (!root.Success || root.Value.Contains("viewBox", StringComparison.OrdinalIgnoreCase))
        {
            return svg;
        }

        var width = Dimension(root.Value, "width");
        var height = Dimension(root.Value, "height");
        if (width is null || height is null)
        {
            return svg;
        }

        var viewBox = string.Create(CultureInfo.InvariantCulture, $" viewBox=\"0 0 {width} {height}\"");
        var tag = root.Value.Insert(root.Value.Length - 1, viewBox);
        return svg[..root.Index] + tag + svg[(root.Index + root.Length)..];
    }

    private static double? Dimension(string tag, string attribute)
    {
        var match = Regex.Match(tag, attribute + "=\"([0-9.]+)", RegexOptions.CultureInvariant);
        return match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    [GeneratedRegex("<svg\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RootTag();

    [GeneratedRegex("src=\"data:image/svg\\+xml;base64,([A-Za-z0-9+/=]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex ImageSource();
}
