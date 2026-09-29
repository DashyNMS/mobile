using DashyNMS.Mobile.DeviceSections;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Device View's card grid: ping and active alerts, then a card per section,
/// in one grid (#69) - see <c>DeviceDetailViewModel.GridCards</c>.
/// </summary>
public sealed class DeviceCardTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Ping { get; set; }

    public DataTemplate? Alerts { get; set; }

    public DataTemplate? Section { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        (item == DeviceOverviewCard.Ping ? Ping
            : item == DeviceOverviewCard.Alerts ? Alerts
            : Section) ?? throw new InvalidOperationException("DeviceCardTemplateSelector needs every template set.");
}
