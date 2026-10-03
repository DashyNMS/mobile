using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Graylog;

/// <summary>
/// The Graylog page's Device chip (#117): a LibreNMS device, searched for by
/// the same addresses as its own Graylog page, or a sender LibreNMS doesn't
/// know, searched for by that one address.
/// </summary>
/// <param name="Name">The device's name as the app shows it, or the address.</param>
/// <param name="DeviceId">The LibreNMS device, or null for an unknown sender.</param>
/// <param name="Address">The sender's address, when <paramref name="DeviceId"/> is null.</param>
public sealed record GraylogDeviceFilter(string Name, int? DeviceId, string? Address)
{
    public static GraylogDeviceFilter ForDevice(int deviceId, string name) => new(name, deviceId, null);

    public static GraylogDeviceFilter ForAddress(string address) => new(address, null, address);

    /// <summary>The same device or address - the name can differ with the device name setting.</summary>
    public bool SameAs(GraylogDeviceFilter? other) =>
        other is not null && (DeviceId is { } id
            ? other.DeviceId == id
            : other.DeviceId is null && string.Equals(Address, other.Address, StringComparison.OrdinalIgnoreCase));
}

/// <summary>One row in the Device chooser.</summary>
public sealed class GraylogDeviceChoice
{
    public GraylogDeviceChoice(GraylogDeviceFilter filter, string details, object status, int count = 0)
    {
        Filter = filter;
        Details = details;
        Status = status;
        Count = count;
    }

    public GraylogDeviceFilter Filter { get; }

    public string Name => Filter.Name;

    /// <summary>The device's address and hardware, or "Not in LibreNMS".</summary>
    public string Details { get; }

    public bool HasDetails => Details.Length > 0;

    /// <summary>A <see cref="DeviceState"/>, or <c>RowStatus.Inactive</c> for a sender LibreNMS doesn't know.</summary>
    public object Status { get; }

    /// <summary>How many of the messages on screen it sent; 0 outside "In these messages".</summary>
    public int Count { get; }

    public string CountText => Count > 0 ? Count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture) : string.Empty;
}

/// <summary>A heading in the Device chooser - "In these messages", "Pinned" - and its rows.</summary>
public sealed class GraylogDeviceGroup : List<GraylogDeviceChoice>
{
    public GraylogDeviceGroup(string name, IEnumerable<GraylogDeviceChoice> choices)
        : base(choices)
    {
        Name = name;
    }

    public string Name { get; }
}
