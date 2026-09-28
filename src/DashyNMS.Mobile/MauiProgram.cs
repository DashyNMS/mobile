using DashyNMS.Mobile.Adapters;
using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Pages;
using DashyNMS.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Core, the session and view models - shared with the unit tests.
		builder.Services.AddDashyNmsMobile();

		// What the shared layer needs from the platform.
		builder.Services.AddSingleton<Security.ISecureStorage, MauiSecureStorage>();
		builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
		builder.Services.AddSingleton<IDialogService, DialogService>();
		builder.Services.AddSingleton<ILauncherService, LauncherService>();
		builder.Services.AddSingleton<IShareService, ShareService>();
		builder.Services.AddSingleton<Map.IMapAssets, MapAssets>();
		builder.Services.AddSingleton<MauiAppearance>();
		builder.Services.AddSingleton<IAppearance>(sp => sp.GetRequiredService<MauiAppearance>());

#if ANDROID
		builder.Services.AddSingleton<IAlertNotifier, AndroidAlertNotifier>();
		builder.Services.AddSingleton<IBackgroundAlertScheduler, AndroidAlertScheduler>();
		builder.Services.AddSingleton<Widgets.IHomeWidgets, AndroidHomeWidgets>();
#elif IOS
		builder.Services.AddSingleton<IAlertNotifier, IosAlertNotifier>();
		builder.Services.AddSingleton<IBackgroundAlertScheduler, IosAlertScheduler>();
		builder.Services.AddSingleton<IAppBadge, IosAppBadge>();
		builder.Services.AddSingleton<Widgets.IHomeWidgets, IosHomeWidgets>();
#endif

		// Shell resolves registered pages through DI, so each gets its view model.
		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddTransient<SignInPage>();
		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<DevicesPage>();
		builder.Services.AddTransient<DeviceDetailPage>();
		builder.Services.AddTransient<AlertsPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<DeviceSectionPage>();
		builder.Services.AddTransient<DeviceGraphsPage>();
		builder.Services.AddTransient<MaintenancePage>();
		builder.Services.AddTransient<AlertDetailPage>();
		builder.Services.AddTransient<HealthPage>();
		builder.Services.AddTransient<GroupsLocationsPage>();
		builder.Services.AddTransient<ThresholdsPage>();
		builder.Services.AddTransient<CustomiseDashboardPage>();
		builder.Services.AddTransient<SensorPickerPage>();
		builder.Services.AddTransient<GraphPickerPage>();
		builder.Services.AddTransient<LogsPage>();
		builder.Services.AddTransient<NeighboursPage>();
		builder.Services.AddTransient<MapPage>();
		builder.Services.AddTransient<GraylogPage>();
		builder.Services.AddTransient<GraylogSettingsPage>();

		return builder.Build();
	}
}
