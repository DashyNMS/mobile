using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Each dashboard card's own template, by its kind.</summary>
public sealed class DashboardCardTemplateSelector : DataTemplateSelector
{
    public DataTemplate? AlertsGauge { get; set; }

    public DataTemplate? DeviceStatus { get; set; }

    public DataTemplate? Alerts { get; set; }

    public DataTemplate? PinnedDevices { get; set; }

    public DataTemplate? RecentlyViewed { get; set; }

    public DataTemplate? Sensors { get; set; }

    public DataTemplate? Graph { get; set; }

    public DataTemplate? Wireless { get; set; }

    public DataTemplate? Empty { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        ((item as DashboardCard)?.Type switch
        {
            DashboardLayout.AlertsGauge => AlertsGauge,
            DashboardLayout.DeviceStatus => DeviceStatus,
            DashboardLayout.Alerts => Alerts,
            DashboardLayout.PinnedDevices => PinnedDevices,
            DashboardLayout.RecentlyViewed => RecentlyViewed,
            DashboardLayout.Sensors => Sensors,
            DashboardLayout.Graph => Graph,
            DashboardLayout.Wireless => Wireless,
            _ => null,
        }) ?? Empty!;
}
