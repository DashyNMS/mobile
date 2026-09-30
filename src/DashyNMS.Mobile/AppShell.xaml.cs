using DashyNMS.Mobile.Pages;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile;

public partial class AppShell : Shell
{
	private readonly TabPins _pins;
	private readonly Dictionary<AppPage, Tab> _pinnedTabs = [];

	public AppShell(TabPins pins)
	{
		InitializeComponent();
		_pins = pins;

		Routing.RegisterRoute(Routes.DeviceDetail, typeof(DeviceDetailPage));
		Routing.RegisterRoute(Routes.DeviceSection, typeof(DeviceSectionPage));
		Routing.RegisterRoute(Routes.DeviceGraphs, typeof(DeviceGraphsPage));
		Routing.RegisterRoute(Routes.Maintenance, typeof(MaintenancePage));
		Routing.RegisterRoute(Routes.AlertDetail, typeof(AlertDetailPage));
		Routing.RegisterRoute(Routes.Thresholds, typeof(ThresholdsPage));
		Routing.RegisterRoute(Routes.CustomiseDashboard, typeof(CustomiseDashboardPage));
		Routing.RegisterRoute(Routes.PickSensors, typeof(SensorPickerPage));
		Routing.RegisterRoute(Routes.PickGraph, typeof(GraphPickerPage));
		Routing.RegisterRoute(Routes.GraylogSettings, typeof(GraylogSettingsPage));
		Routing.RegisterRoute(Routes.SettingsSection, typeof(SettingsSectionPage));

		// Every page More lists can also be pushed, for when it isn't pinned.
		foreach (var page in AppPages.All)
		{
			Routing.RegisterRoute(AppPages.PushRoute(page), PageType(page));
		}

		ArrangeTabs();
		_pins.Changed += (_, _) => Dispatcher.Dispatch(ArrangeTabs);
	}

	private static Type PageType(AppPage page) => page switch
	{
		AppPage.Devices => typeof(DevicesPage),
		AppPage.Alerts => typeof(AlertsPage),
		AppPage.Health => typeof(HealthPage),
		AppPage.Map => typeof(MapPage),
		AppPage.NetworkMap => typeof(NetworkMapPage),
		AppPage.Neighbours => typeof(NeighboursPage),
		AppPage.GroupsLocations => typeof(GroupsLocationsPage),
		AppPage.Logs => typeof(LogsPage),
		AppPage.Graylog => typeof(GraylogPage),
		_ => typeof(SettingsPage),
	};

	/// <summary>
	/// Puts the pinned pages between Dashboard and More, in order. Tabs that
	/// stay pinned are kept, not rebuilt, so their pages keep what they've loaded.
	/// </summary>
	private void ArrangeTabs()
	{
		var pinned = _pins.Pinned;
		foreach (var (page, tab) in _pinnedTabs.ToList())
		{
			if (!pinned.Contains(page))
			{
				MainTabs.Items.Remove(tab);
				_pinnedTabs.Remove(page);
			}
		}

		var index = 1; // after Dashboard
		foreach (var page in pinned)
		{
			if (!_pinnedTabs.TryGetValue(page, out var tab))
			{
				tab = new Tab
				{
					Title = AppPages.TabTitle(page),
					Route = AppPages.TabRoute(page),
					Icon = AppPages.Icon(page),
					Items = { new ShellContent { ContentTemplate = new DataTemplate(PageType(page)) } },
				};
				_pinnedTabs[page] = tab;
				MainTabs.Items.Insert(index, tab);
			}
			else if (MainTabs.Items.IndexOf(tab) != index)
			{
				MainTabs.Items.Remove(tab);
				MainTabs.Items.Insert(index, tab);
			}

			index++;
		}
	}
}
