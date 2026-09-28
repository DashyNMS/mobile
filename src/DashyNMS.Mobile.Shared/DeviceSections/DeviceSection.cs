namespace DashyNMS.Mobile.DeviceSections;

/// <summary>Device View's tabs, as desktop's (Unimus waits for that integration).</summary>
public enum DeviceSection
{
    /// <summary>The device's Graylog messages - its own page, and only listed once Graylog is set up.</summary>
    Graylog = -1,

    Availability,
    Sensors,
    Resources,
    Ports,
    Neighbours,
    Graphs,
    Vlans,
    Fdb,
    Arp,
    Routing,
    Wireless,
    Inventory,
    EventLog,
}

/// <summary>A section in Device detail's list.</summary>
public sealed record DeviceSectionInfo(DeviceSection Section, string Title, string Description)
{
    /// <summary>In desktop's tab order, with a line saying what each holds.</summary>
    public static IReadOnlyList<DeviceSectionInfo> All { get; } =
    [
        new(DeviceSection.Availability, "Availability", "Uptime percentages and recent outages"),
        new(DeviceSection.Sensors, "Sensors", "Temperatures, optics, fans, power, grouped by kind"),
        new(DeviceSection.Graphs, "Graphs", "The device's own LibreNMS graphs"),
        new(DeviceSection.Resources, "Resources", "CPU, memory and storage"),
        new(DeviceSection.Ports, "Ports", "Interfaces, their state and traffic"),
        new(DeviceSection.Neighbours, "Neighbours", "What's connected, from LLDP/CDP both ways"),
        new(DeviceSection.Vlans, "VLANs", "VLANs and how many ports carry each"),
        new(DeviceSection.Fdb, "FDB", "MAC address table"),
        new(DeviceSection.Arp, "ARP", "IP to MAC table"),
        new(DeviceSection.Routing, "Routing", "BGP sessions, OSPF neighbours, VRFs"),
        new(DeviceSection.Wireless, "Wireless", "Clients, channels, signal and more"),
        new(DeviceSection.Inventory, "Inventory", "Chassis, modules and optics, with serials"),
        new(DeviceSection.EventLog, "Event log", "The device's recent events"),
    ];

    /// <summary>Not in <see cref="All"/>: it isn't LibreNMS data, and only shows when Graylog is set up.</summary>
    public static DeviceSectionInfo Graylog { get; } =
        new(DeviceSection.Graylog, "Graylog", "Syslog and other messages from this device");

    public static DeviceSectionInfo For(DeviceSection section) =>
        section == DeviceSection.Graylog ? Graylog : All.First(s => s.Section == section);
}
