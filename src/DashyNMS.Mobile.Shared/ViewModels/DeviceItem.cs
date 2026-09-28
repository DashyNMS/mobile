using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>One row of the device list.</summary>
public sealed class DeviceItem
{
    public DeviceItem(Device device) => Device = device;

    public Device Device { get; }

    public int DeviceId => Device.DeviceId;

    public string Name => Device.BestName;

    /// <summary>"10.0.0.1 · Cisco C9300 · London DC" - whichever of those are known.</summary>
    public string Details => string.Join(" · ", new[] { Device.Ip, Device.Hardware, Device.Location }
        .Where(s => !string.IsNullOrWhiteSpace(s)));

    public DeviceState State => Device.State;

    public string StateText => State.ToDisplayString();

    public string UptimeText => Formatting.Uptime(Device.Uptime);

    /// <summary>True when <paramref name="text"/> appears in any field someone would search a device by.</summary>
    public bool Matches(string text) =>
        Contains(Device.BestName, text)
        || Contains(Device.Hostname, text)
        || Contains(Device.SysName, text)
        || Contains(Device.Ip, text)
        || Contains(Device.Os, text)
        || Contains(Device.Hardware, text)
        || Contains(Device.Location, text);

    private static bool Contains(string? field, string text) =>
        field?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;
}
