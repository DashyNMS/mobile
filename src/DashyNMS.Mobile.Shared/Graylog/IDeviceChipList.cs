using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Graylog;

/// <summary>
/// A list with a Device chip that <see cref="GraylogDevicePickerViewModel"/>
/// sets - Graylog's (#117) and the Logs page's (#118). One chooser for both,
/// so picking a device works the same wherever the chip is.
/// </summary>
public interface IDeviceChipList
{
    /// <summary>Whether the chip is set - the chooser then offers "Any device".</summary>
    bool HasDeviceFilter { get; }

    /// <summary>The devices LibreNMS has, if the list has fetched them already - saves the chooser fetching them again.</summary>
    IReadOnlyList<Device> KnownDevices { get; }

    /// <summary>Who the rows on screen are from, busiest first - the chooser's first suggestions.</summary>
    IReadOnlyList<(GraylogDeviceFilter Filter, int Count)> Senders();

    /// <summary>The heading over <see cref="Senders"/>: "In these messages".</summary>
    string SendersHeading { get; }

    /// <summary>
    /// Whether an address no device has can be chosen. Graylog can search
    /// for any sender's address; LibreNMS's logs only know its own devices.
    /// </summary>
    bool SearchesAddresses { get; }

    /// <summary>Sets the chip - null for every device's.</summary>
    void ShowDevice(GraylogDeviceFilter? device);
}
