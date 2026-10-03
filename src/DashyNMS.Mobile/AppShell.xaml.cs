using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Pages;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile;

public partial class AppShell : Shell
{
	private readonly TabPins _pins;
	private readonly AlertTabDot _alertDot;
	private readonly Dictionary<AppPage, Tab> _pinnedTabs = [];

	public AppShell(TabPins pins, AlertTabDot alertDot)
	{
		InitializeComponent();
		_pins = pins;
		_alertDot = alertDot;

		Routing.RegisterRoute(Routes.DeviceDetail, typeof(DeviceDetailPage));
		Routing.RegisterRoute(Routes.DeviceSection, typeof(DeviceSectionPage));
		Routing.RegisterRoute(Routes.DeviceGraphs, typeof(DeviceGraphsPage));
		Routing.RegisterRoute(Routes.Maintenance, typeof(MaintenancePage));
		Routing.RegisterRoute(Routes.AlertDetail, typeof(AlertDetailPage));
		Routing.RegisterRoute(Routes.Thresholds, typeof(ThresholdsPage));
		Routing.RegisterRoute(Routes.CustomiseDashboard, typeof(CustomiseDashboardPage));
		Routing.RegisterRoute(Routes.PickSensors, typeof(SensorPickerPage));
		Routing.RegisterRoute(Routes.PickGraph, typeof(GraphPickerPage));
		Routing.RegisterRoute(Routes.TopCardSetUp, typeof(TopCardSetUpPage));
		Routing.RegisterRoute(Routes.AlertRule, typeof(AlertRulePage));
		Routing.RegisterRoute(Routes.AlertTemplate, typeof(AlertTemplatePage));
		Routing.RegisterRoute(Routes.GraylogSettings, typeof(GraylogSettingsPage));
		Routing.RegisterRoute(Routes.GraylogMessage, typeof(GraylogMessagePage));
		Routing.RegisterRoute(Routes.GraylogDevice, typeof(GraylogDevicePickerPage));
		Routing.RegisterRoute(Routes.SettingsSection, typeof(SettingsSectionPage));
		Routing.RegisterRoute(Routes.NeighbourLink, typeof(NeighbourLinkPage));
		Routing.RegisterRoute(Routes.NeighbourGroups, typeof(NeighbourGroupsPage));
		Routing.RegisterRoute(Routes.NeighbourGroupEditor, typeof(NeighbourGroupEditorPage));

		// Every page More lists can also be pushed, for when it isn't pinned.
		foreach (var page in AppPages.All)
		{
			Routing.RegisterRoute(AppPages.PushRoute(page), PageType(page));
		}

		ArrangeTabs();
		_pins.Changed += (_, _) => Dispatcher.Dispatch(ArrangeTabs);

		// The Alerts dot (#90): redrawn when it changes, and whenever the tab
		// bar may have been rebuilt underneath it.
		_alertDot.Changed += (_, _) => Dispatcher.Dispatch(ShowAlertDot);
		Navigated += (_, _) => ShowAlertDot();
	}

	/// <summary>
	/// The dot goes on the Alerts tab when it's pinned, otherwise on More -
	/// Alerts is then under it - so it's never out of sight.
	/// </summary>
	private void ShowAlertDot()
	{
		var visible = MainTabs.Items.Where(tab => tab.IsVisible).ToList();
		var target = _pinnedTabs.TryGetValue(AppPage.Alerts, out var alerts) ? alerts : visible.LastOrDefault();
		var index = target is null ? -1 : visible.IndexOf(target);
#if IOS
		TabDots.Show(index, _alertDot.Severity, _alertDot.Description);
#endif
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
		AppPage.AlertRules => typeof(AlertRulesPage),
		AppPage.Logs => typeof(LogsPage),
		AppPage.Graylog => typeof(GraylogPage),
		_ => typeof(SettingsPage),
	};

	/// <summary>
	/// Swaps the tab showing for a new one with a fresh page - the last
	/// resort for a page left blank after the phone is unlocked (#107), which
	/// otherwise stayed blank until the app was restarted. Same title, route
	/// and icon, so nothing else notices. False if it isn't a tab of the main
	/// tab bar made from a template.
	/// </summary>
	public bool RebuildCurrentTab()
	{
		if (CurrentItem != MainTabs || MainTabs.CurrentItem is not Tab tab
			|| tab.CurrentItem is not { ContentTemplate: { } template })
		{
			return false;
		}

		var index = MainTabs.Items.IndexOf(tab);
		var fresh = new Tab
		{
			Title = tab.Title,
			Route = tab.Route,
			Icon = tab.Icon,
			Items = { new ShellContent { ContentTemplate = template } },
		};

		MainTabs.Items.RemoveAt(index);
		MainTabs.Items.Insert(index, fresh);
		foreach (var (page, pinned) in _pinnedTabs.ToList())
		{
			if (pinned == tab)
			{
				_pinnedTabs[page] = fresh;
			}
		}

		MainTabs.CurrentItem = fresh;
		Dispatcher.Dispatch(ShowAlertDot);
		return true;
	}

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

		// The tabs moved: the dot goes with Alerts, or to More.
		Dispatcher.Dispatch(ShowAlertDot);
	}
}
