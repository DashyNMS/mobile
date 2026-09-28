using DashyNMS.Mobile.Pages;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(Routes.DeviceDetail, typeof(DeviceDetailPage));
	}
}
