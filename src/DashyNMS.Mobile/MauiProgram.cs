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
		// Start-up time (#218) runs from here, as early as the app can see.
		var startup = new Diagnostics.StartupTiming();

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

		// The log Settings can share (#107): the app's own messages plus what
		// it was doing, kept in a file so a restart doesn't lose it.
		var diagnostics = new DiagnosticsLog(
			Path.Combine(FileSystem.AppDataDirectory, "diagnostics.log"),
			appVersion: $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})",
			device: $"{DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model} · {DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}");
		builder.Logging.AddProvider(diagnostics);
		builder.Services.AddSingleton(diagnostics);

		// What the diagnostics follow beyond log messages (#126): every request,
		// settings as they change, slow pages - and the file's header and "hide names".
		builder.Services.AddSingleton(sp => new HttpRequestLog(diagnostics, () => sp.GetRequiredService<DesktopNMS.Services.ISessionService>().Connection?.WebRoot.Host));
		builder.Services.AddSingleton(sp => new AppDiagnostics(diagnostics, sp.GetRequiredService<DesktopNMS.Core.Configuration.ISettingsStore>(), sp.GetRequiredService<HttpRequestLog>()));
		builder.Services.AddSingleton<DiagnosticsReport>();

#if ANDROID || IOS
		// Every text box and picker as a field well, not the platform's own
		// border or underline - see Platforms/*/FieldWells.
		FieldWells.Register();
#endif

#if IOS
		builder.ConfigureMauiHandlers(handlers =>
			handlers.AddHandler(typeof(AppShell), typeof(AppShellRenderer)));

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
		builder.Services.AddSingleton<INoticePlatform, NoticePlatform>();
		builder.Services.AddSingleton<IClipboardText, MauiClipboard>();
		builder.Services.AddSingleton<DesktopNMS.Core.SignIn.IWebSignIn, WebSignIn>();
		builder.Services.AddSingleton<Map.IMapAssets, MapAssets>();
		builder.Services.AddSingleton<MauiAppearance>();
		builder.Services.AddSingleton<IAppearance>(sp => sp.GetRequiredService<MauiAppearance>());
		builder.Services.AddSingleton<IAppPreferences, MauiPreferences>();

		// Opt-in error reports (#217): Sentry, with the key this build was
		// given (none outside the store builds), and the queue they wait in.
		builder.Services.AddSingleton<Diagnostics.IDiagnosticsTransport>(_ => new Diagnostics.SentryTransport(SentryDsn()));
		builder.Services.AddSingleton(sp => new Diagnostics.DiagnosticsReporter(
			ReportContext(),
			sp.GetRequiredService<Diagnostics.IDiagnosticsTransport>(),
			sp.GetRequiredService<IDiagnosticsConsent>(),
			Path.Combine(FileSystem.AppDataDirectory, "error-reports")));
		builder.Services.AddSingleton<Diagnostics.IErrorReports>(sp => sp.GetRequiredService<Diagnostics.DiagnosticsReporter>());

		// Usage and performance (#218): batched and sampled, queued with the error reports.
		builder.Services.AddSingleton<Diagnostics.AppMetrics>();
		builder.Services.AddSingleton<Diagnostics.IAppMetrics>(sp => sp.GetRequiredService<Diagnostics.AppMetrics>());
		builder.Services.AddSingleton(startup);

		// Stalls, memory and sessions (#219), on screen only - App starts and stops them.
		builder.Services.AddSingleton(sp => new Diagnostics.UiStallMonitor(
			sp.GetRequiredService<Diagnostics.IAppMetrics>(), MainThread.BeginInvokeOnMainThread));
		builder.Services.AddSingleton(sp => new Diagnostics.MemoryMonitor(
			sp.GetRequiredService<Diagnostics.IAppMetrics>(), MemoryReader.Read));
		builder.Services.AddSingleton(sp => new Diagnostics.SessionHealth(
			sp.GetRequiredService<Diagnostics.DiagnosticsReporter>(),
			sp.GetRequiredService<IDiagnosticsConsent>(),
			Path.Combine(FileSystem.AppDataDirectory, "session-health")));

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
		builder.Services.AddTransient<AlertRulesPage>();
		builder.Services.AddTransient<AlertRulePage>();
		builder.Services.AddTransient<AlertTemplatePage>();
		builder.Services.AddTransient<ThresholdsPage>();
		builder.Services.AddTransient<LicencesPage>();
		builder.Services.AddTransient<NotifyRulePickerPage>();
		builder.Services.AddTransient<CustomiseDashboardPage>();
		builder.Services.AddTransient<AddCardPage>();
		builder.Services.AddTransient<SensorPickerPage>();
		builder.Services.AddTransient<GraphPickerPage>();
		builder.Services.AddTransient<TopCardSetUpPage>();
		builder.Services.AddTransient<LogsPage>();
		builder.Services.AddTransient<LogEntryPage>();
		builder.Services.AddTransient<NeighboursPage>();
		builder.Services.AddTransient<NeighbourLinkPage>();
		builder.Services.AddTransient<NeighbourGroupsPage>();
		builder.Services.AddTransient<NeighbourGroupEditorPage>();
		builder.Services.AddTransient<NetworkMapPage>();

		// Where the network map's devices were dragged to, as desktop keeps
		// them - in the app's own folder, one set per server and filter (#86).
		// Resettable, so "Sign out and forget everything" can drop them all (#139).
		builder.Services.AddSingleton(services => new Topology.ResettableMapLayoutStore(
			Path.Combine(FileSystem.AppDataDirectory, "map-layouts.json"),
			services.GetRequiredService<ILogger<DesktopNMS.Core.Topology.MapLayoutStore>>()));
		builder.Services.AddSingleton<DesktopNMS.Core.Topology.IMapLayoutStore>(services => services.GetRequiredService<Topology.ResettableMapLayoutStore>());
		builder.Services.AddSingleton<ILocalDataWipe, MauiLocalDataWipe>();
		builder.Services.AddTransient<MapPage>();
		builder.Services.AddTransient<GraylogPage>();
		builder.Services.AddTransient<GraylogSettingsPage>();
		builder.Services.AddTransient<SensorDefinitionsSettingsPage>();
		builder.Services.AddTransient<WhatsNewPage>();
		builder.Services.AddTransient<DiagnosticsSharingPage>();
		builder.Services.AddTransient<GraylogMessagePage>();
		builder.Services.AddTransient<GraylogDevicePickerPage>();
		builder.Services.AddTransient<UnimusSettingsPage>();
		builder.Services.AddTransient<UnimusBackupsPage>();
		builder.Services.AddTransient<UnimusBackupPage>();
		builder.Services.AddTransient<UnimusDiffPage>();

		return builder.Build();
	}

	/// <summary>The key the store build baked in (see the csproj), or none.</summary>
	private static string? SentryDsn() =>
		typeof(MauiProgram).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
			.OfType<System.Reflection.AssemblyMetadataAttribute>()
			.FirstOrDefault(a => a.Key == "SentryDsn")?.Value is { Length: > 0 } dsn ? dsn : null;

	/// <summary>
	/// "2.0.0+412" - the release the TestFlight and Play workflows record in
	/// Sentry - with the platform, its version band and the architecture.
	/// Never the device's name or model.
	/// </summary>
	private static Diagnostics.ErrorReportContext ReportContext() => new(
		$"{AppInfo.Current.VersionString}+{AppInfo.Current.BuildString}",
		DeviceInfo.Current.Platform == DevicePlatform.iOS ? "ios" : DeviceInfo.Current.Platform == DevicePlatform.Android ? "android" : "unknown",
		Diagnostics.ErrorPayload.OsVersionBand(DeviceInfo.Current.Version),
		Diagnostics.ErrorPayload.ArchitectureName(System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture));
}
