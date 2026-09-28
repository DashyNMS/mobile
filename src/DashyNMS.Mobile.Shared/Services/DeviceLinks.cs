using System.Net;
using System.Net.Sockets;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// ssh:// and telnet:// links to a device, for whatever app on the phone
/// handles them - desktop's "Open in" SSH and Telnet, which hand the same
/// links to the OS rather than bundling a client.
/// </summary>
/// <remarks>
/// As desktop, the device's IP when it has one, else its hostname - never the
/// display name, which may not resolve. Unlike desktop, an IPv6 address goes
/// in brackets, without which the link doesn't parse at all.
/// </remarks>
public static class DeviceLinks
{
    public const string Ssh = "ssh";
    public const string Telnet = "telnet";

    public static Uri? For(string scheme, Device? device)
    {
        var target = !string.IsNullOrWhiteSpace(device?.Ip) ? device!.Ip!.Trim()
            : !string.IsNullOrWhiteSpace(device?.Hostname) ? device!.Hostname!.Trim()
            : null;
        if (target is null)
        {
            return null;
        }

        if (IPAddress.TryParse(target, out var address) && address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            target = $"[{address}]";
        }

        return Uri.TryCreate($"{scheme}://{target}", UriKind.Absolute, out var uri) ? uri : null;
    }
}
