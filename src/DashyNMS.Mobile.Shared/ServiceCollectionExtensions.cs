using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Storage;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Widgets;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile;

public static class ServiceCollectionExtensions
{
    /// <summary>Every secret key the app uses, read into <see cref="SecretCache"/> at start-up.</summary>
    public static IReadOnlyList<string> SecretKeys { get; } = [SecureTokenProtector.Key, SecureGraylogPasswordProtector.Key];

    /// <summary>
    /// Core's LibreNMS client and settings, the session, and the view models.
    /// The app head adds <see cref="ISecureStorage"/>, navigation, dialogs, the
    /// launcher and the platform's notifications and background scheduling on top.
    /// </summary>
    /// <remarks>
    /// Mirrors desktop's <c>AddDesktopNmsCore</c>, minus its DPAPI secret
    /// stores (Windows-only, replaced by keychain-backed ones) and the
    /// integrations mobile doesn't have screens for yet (Unimus, update checks).
    /// </remarks>
    public static IServiceCollection AddDashyNmsMobile(this IServiceCollection services)
    {
        // Before anything touches desktop's AppPaths - see MobileStorage.
        MobileStorage.EnsureDataFolder();

        services.AddSingleton<ServerFailover>();
        services.AddSingleton<LibreNmsTransport>();
        services.AddSingleton<ILibreNmsTransport>(sp => sp.GetRequiredService<LibreNmsTransport>());
        services.AddSingleton<LibreNmsClient>();
        services.AddSingleton<ILibreNmsClient>(sp => sp.GetRequiredService<LibreNmsClient>());

        // Desktop's store, wrapped so Save() writes - see MobileSettingsStore.
        services.AddSingleton<SettingsStore>();
        services.AddSingleton<ISettingsStore>(sp => new MobileSettingsStore(sp.GetRequiredService<SettingsStore>()));
        services.AddSingleton<SecretCache>();
        services.AddSingleton<ITokenProtector, SecureTokenProtector>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<DeviceBookmarks>();
        services.AddSingleton<MaintenanceScan>();

        // Alert notifications. The app head supplies IAlertNotifier and
        // IBackgroundAlertScheduler for its platform, and IAppBadge where the
        // platform can set a number on the icon (registered later, it wins).
        services.AddSingleton<IAppBadge, NoAppBadge>();
        services.AddSingleton<INotificationPrivacy, SystemNotificationPrivacy>();
        services.AddSingleton<IHomeWidgets, NoHomeWidgets>();
        services.AddSingleton<IAppearance, InMemoryAppearance>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISelfActionTracker, SelfActionTracker>();
        services.AddSingleton<IAlertWatchStore, AlertWatchStore>();
        services.AddSingleton<AlertWatcher>();
        services.AddSingleton<AlertWatchCoordinator>();
        services.AddSingleton<NotificationRouter>();

        services.AddTransient<SignInViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DevicesViewModel>();
        services.AddTransient<DeviceDetailViewModel>();
        services.AddTransient<AlertsViewModel>();
        services.AddTransient<AlertDetailViewModel>();
        services.AddTransient<HealthViewModel>();
        services.AddTransient<GroupsLocationsViewModel>();
        services.AddTransient<ThresholdsViewModel>();
        services.AddTransient<Dashboard.CustomiseDashboardViewModel>();
        services.AddTransient<Dashboard.SensorPickerViewModel>();
        services.AddTransient<Dashboard.GraphPickerViewModel>();
        services.AddSingleton<Logs.INetworkLogs, Logs.NetworkLogs>();
        services.AddTransient<Logs.LogsViewModel>();
        services.AddTransient<Topology.NeighboursViewModel>();
        services.AddTransient<Map.MapViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MaintenanceViewModel>();

        // Graylog: independent of the LibreNMS sign-in, as on desktop - see GraylogSetup.
        services.AddSingleton<GraylogApi>();
        services.AddSingleton<IGraylogApi>(sp => sp.GetRequiredService<GraylogApi>());
        services.AddSingleton<IGraylogPasswordProtector, SecureGraylogPasswordProtector>();
        services.AddSingleton<Graylog.GraylogSetup>();
        services.AddSingleton<Graylog.IGraylogConnectionTester, Graylog.GraylogConnectionTester>();
        services.AddTransient<Graylog.GraylogViewModel>();
        services.AddTransient<Graylog.GraylogSettingsViewModel>();

        // Device View sections.
        services.AddSingleton<DeviceSectionLoader>();
        services.AddTransient<DeviceSectionViewModel>();
        services.AddTransient<DeviceGraphsViewModel>();

        return services;
    }
}
