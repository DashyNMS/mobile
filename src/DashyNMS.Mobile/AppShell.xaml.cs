using DashyNMS.Mobile.Pages;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(Routes.DeviceDetail, typeof(DeviceDetailPage));
		Routing.RegisterRoute(Routes.DeviceSection, typeof(DeviceSectionPage));
		Routing.RegisterRoute(Routes.DeviceGraphs, typeof(DeviceGraphsPage));
		Routing.RegisterRoute(Routes.Maintenance, typeof(MaintenancePage));
	}
}
