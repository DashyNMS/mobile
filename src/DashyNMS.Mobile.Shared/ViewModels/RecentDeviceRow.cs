using DashyNMS.Mobile.DeviceSections;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// A row of the dashboard's Recently viewed card (#140), as desktop's widget
/// draws it: the device's state, its name, hardware and location, and when
/// it was opened - rather than the name alone, as it was.
/// </summary>
public sealed class RecentDeviceRow
{
    public RecentDeviceRow(RecentlyViewedDevice recent, DeviceItem? device, DateTimeOffset now)
    {
        DeviceId = recent.DeviceId;
        Name = device?.Name ?? recent.DisplayName ?? $"Device {recent.DeviceId}";
        Details = device?.Details ?? string.Empty;
        Status = device is null ? RowStatus.Inactive : device.State;
        var age = now - recent.ViewedAt;
        ViewedText = Formatting.Age(age < TimeSpan.Zero ? TimeSpan.Zero : age);
    }

    public int DeviceId { get; }

    public string Name { get; }

    /// <summary>Its address, hardware and location; empty for a device LibreNMS no longer has.</summary>
    public string Details { get; }

    public bool HasDetails => Details.Length > 0;

    /// <summary>A <c>DeviceState</c> for the dot - or inactive, for a device that has gone.</summary>
    public object Status { get; }

    /// <summary>"3m ago".</summary>
    public string ViewedText { get; }
}
