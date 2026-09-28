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
		Routing.RegisterRoute(Routes.AlertDetail, typeof(AlertDetailPage));
		Routing.RegisterRoute(Routes.GroupsLocations, typeof(GroupsLocationsPage));
		Routing.RegisterRoute(Routes.Thresholds, typeof(ThresholdsPage));
		Routing.RegisterRoute(Routes.CustomiseDashboard, typeof(CustomiseDashboardPage));
		Routing.RegisterRoute(Routes.PickSensors, typeof(SensorPickerPage));
		Routing.RegisterRoute(Routes.PickGraph, typeof(GraphPickerPage));
		Routing.RegisterRoute(Routes.Logs, typeof(LogsPage));
	}
}
