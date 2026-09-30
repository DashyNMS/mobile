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
				// The mock-ups' type (#69): IBM Plex Sans for text, Sora for
				// titles and the name. Both SIL OFL - see docs/licences.
				fonts.AddFont("IBMPlexSans-Regular.ttf", "BodyRegular");
				fonts.AddFont("IBMPlexSans-SemiBold.ttf", "BodySemibold");
				fonts.AddFont("Sora-Bold.ttf", "Display");
				fonts.AddFont("IBMPlexMono-Regular.ttf", "Mono");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

#if ANDROID
		// An entry in a Field well (Sign in) has the well as its outline, so
		// Android's underline would draw a second one.
		Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("FieldWell", (handler, entry) =>
		{
			if (entry is Entry { Parent: Border })
			{
				handler.PlatformView.BackgroundTintList =
					Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
			}
		});
#endif

#if IOS
		// A search box in a list's header (Batch 10) drew UISearchBar's own
		// bar round it: a black outline on the toned page. Minimal style with
		// no background image leaves just the rounded field. After Background,
		// which would otherwise put the bar's tint back.
		Microsoft.Maui.Handlers.SearchBarHandler.Mapper.AppendToMapping(nameof(IView.Background), (handler, _) =>
		{
			handler.PlatformView.SearchBarStyle = UIKit.UISearchBarStyle.Minimal;
			handler.PlatformView.BackgroundImage = new UIKit.UIImage();
			handler.PlatformView.BarTintColor = UIKit.UIColor.Clear;
		});
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
		builder.Services.AddSingleton<IAppPreferences, MauiPreferences>();

#if ANDROID
		builder.Services.AddSingleton<IAlertNotifier, AndroidAlertNotifier>();
		builder.Services.AddSingleton<INotificationPrivacy, AndroidNotificationPrivacy>();
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
		builder.Services.AddTransient<SettingsSectionPage>();
		builder.Services.AddTransient<MorePage>();
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
		builder.Services.AddTransient<NetworkMapPage>();
		builder.Services.AddTransient<MapPage>();
		builder.Services.AddTransient<GraylogPage>();
		builder.Services.AddTransient<GraylogSettingsPage>();

		return builder.Build();
	}
}
