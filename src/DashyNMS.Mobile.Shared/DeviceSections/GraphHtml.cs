using System.Globalization;
using System.Text.RegularExpressions;

namespace DashyNMS.Mobile.DeviceSections;

/// <summary>
/// Wraps a LibreNMS (rrdtool) SVG graph in a page a web view can show,
/// fitted to the screen's width and readable in dark mode.
/// </summary>
public static partial class GraphHtml
{
    /// <summary>rrdtool's black text and axes - what desktop's GraphSvgTheming recolours.</summary>
    private const string BlackFill = "fill=\"rgb(0%, 0%, 0%)\"";

    /// <summary>Desktop's dark-palette text colour, #E6EAF0, as rrdtool writes colours.</summary>
    private const string DarkText = "fill=\"rgb(90.2%, 91.8%, 94.1%)\"";

    public static string Build(string svg, bool dark)
    {
        ArgumentNullException.ThrowIfNull(svg);

        var themed = dark ? svg.Replace(BlackFill, DarkText, StringComparison.Ordinal) : svg;
        var scalable = MakeScalable(themed);
        var background = dark ? "#11141A" : "#FFFFFF";

        return $$"""
            <!DOCTYPE html>
            <html><head>
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <style>
              html, body { margin: 0; padding: 0; background: {{background}}; }
              svg { display: block; width: 100%; height: auto; }
            </style>
            </head><body>{{scalable}}</body></html>
            """;
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
}
