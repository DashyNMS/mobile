using DashyNMS.Mobile.DeviceSections;
using DesktopNMS.Core.Services;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// Wraps a device logo from Core's <see cref="IDeviceLogoCache"/> in a page a
/// small web view can show, centred and scaled to fit (#348).
/// </summary>
/// <remarks>
/// MAUI's Image only draws SVGs converted at build time, and LibreNMS's logos
/// are mostly SVG, so a web view shows them. The file is whatever the server
/// sent: as with <see cref="GraphHtml"/>, it goes in as a data: URI in an
/// &lt;img&gt;, which runs no script and fetches nothing, under the same
/// Content-Security-Policy.
/// </remarks>
public static class DeviceLogoHtml
{
    /// <summary>The page for <paramref name="logo"/>, or null when its file can't be read (evicted, or never written).</summary>
    public static string? Page(DeviceLogo logo)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(logo.FilePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (bytes.Length == 0)
        {
            return null;
        }

        var type = logo.Format == DeviceLogoFormat.Svg ? "image/svg+xml" : "image/png";
        return $$"""
            <!DOCTYPE html>
            <html><head>
            <meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="{{GraphHtml.ContentSecurityPolicy}}">
            <meta name="viewport" content="width=device-width, initial-scale=1, user-scalable=no">
            <style>html,body{margin:0;height:100%;background:transparent;overflow:hidden}
            body{display:flex;align-items:center;justify-content:center}
            img{max-width:100%;max-height:100%;object-fit:contain}</style>
            </head><body><img alt="" src="data:{{type}};base64,{{Convert.ToBase64String(bytes)}}"></body></html>
            """;
    }
}
