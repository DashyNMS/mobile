namespace DashyNMS.Mobile.DeviceSections;

/// <summary>
/// Device View's ping response and active alerts cards, as entries in the
/// same card grid as the sections (#69) - the page draws each from the view
/// model, so these only say which.
/// </summary>
public sealed class DeviceOverviewCard
{
    private DeviceOverviewCard(string name)
    {
        Name = name;
    }

    public static DeviceOverviewCard Ping { get; } = new("Ping response");

    public static DeviceOverviewCard Alerts { get; } = new("Active alerts");

    public string Name { get; }

    public override string ToString() => Name;
}
