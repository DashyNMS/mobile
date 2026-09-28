using System.Globalization;
using DashyNMS.Mobile.Alerts;

namespace DashyNMS.Mobile.Widgets;

/// <summary>
/// The dashynms:// links an iOS widget opens the app with: <c>dashynms://alerts</c>
/// for the alert list, <c>dashynms://alert/5?device=3</c> for one alert,
/// <c>dashynms://device/3</c> for a device (the pinned devices and sensors widgets).
/// </summary>
/// <remarks>
/// Any app or web page can open a custom scheme, so a link is only ever
/// read as somewhere to navigate to - the same places a notification tap
/// can go - and anything else is ignored.
/// </remarks>
public static class WidgetLink
{
    public const string Scheme = "dashynms";

    public static NotificationTarget? TryParse(Uri? uri)
    {
        if (uri is null || !uri.IsAbsoluteUri || !string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        switch (uri.Host.ToLowerInvariant())
        {
            case "alerts":
                return new NotificationTarget(DeviceId: null);

            case "alert":
                var segment = uri.AbsolutePath.Trim('/');
                if (!TryParseId(segment, out var alertId))
                {
                    return null;
                }

                int? deviceId = TryParseId(QueryValue(uri, "device"), out var device) ? device : null;
                return new NotificationTarget(deviceId, alertId);

            case "device":
                return TryParseId(uri.AbsolutePath.Trim('/'), out var id) ? new NotificationTarget(id) : null;

            default:
                return null;
        }
    }

    private static bool TryParseId(string? text, out int id) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;

    private static string? QueryValue(Uri uri, string name)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0], name, StringComparison.OrdinalIgnoreCase))
            {
                return parts[1];
            }
        }

        return null;
    }
}
