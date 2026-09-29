namespace DashyNMS.Mobile.ViewModels;

/// <summary>Where a Device card row leads when tapped.</summary>
public enum DevicePropertyLink
{
    None,

    /// <summary>The Devices page, filtered to the location.</summary>
    Location,

    /// <summary>The Devices page, filtered to one of the device's groups.</summary>
    Groups,
}

/// <summary>One row of Device View's Device card: "IP address", "10.0.0.2".</summary>
public sealed record DeviceProperty(string Key, string Value)
{
    public DevicePropertyLink Link { get; init; }

    /// <summary>Drawn as a link, in the accent colour.</summary>
    public bool IsLink => Link != DevicePropertyLink.None;
}
