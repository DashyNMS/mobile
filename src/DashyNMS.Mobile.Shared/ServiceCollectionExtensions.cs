using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
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
    /// Mirrors desktop's <c>AddDesktopNmsCore</c>, plus keychain-backed secret
    /// stores (Core leaves those to each app), minus the integrations mobile
    /// doesn't have screens for yet (Unimus, update checks).
    /// </remarks>
    public static IServiceCollection AddDashyNmsMobile(this IServiceCollection services)
    {
        services.AddSingleton<ServerFailover>();
        services.AddSingleton<LibreNmsTransport>();
        services.AddSingleton<ILibreNmsTransport>(sp => sp.GetRequiredService<LibreNmsTransport>());
        services.AddSingleton<LibreNmsClient>();
        services.AddSingleton<ILibreNmsClient>(sp => sp.GetRequiredService<LibreNmsClient>());

        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<SecretCache>();
        services.AddSingleton<DesktopNMS.Core.SignIn.ICertificateProbe, DesktopNMS.Core.SignIn.CertificateProbe>();
        services.AddSingleton<ITokenProtector, SecureTokenProtector>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<DeviceBookmarks>();
        services.AddSingleton<MaintenanceScan>();

        // On the backup address, and the way back (#114) - the dashboard's pill and Settings' Server card.
        services.AddSingleton<BackupAddressStatus>();

        // A certificate met while signed in - the backup address's, say (#141).
        services.AddSingleton<CertificatePrompt>();

        // Alert notifications. The app head supplies IAlertNotifier and
        // IBackgroundAlertScheduler for its platform, and IAppBadge where the
        // platform can set a number on the icon (registered later, it wins).
        services.AddSingleton<IAppBadge, NoAppBadge>();
        services.AddSingleton<INotificationPrivacy, SystemNotificationPrivacy>();
        services.AddSingleton<IHomeWidgets, NoHomeWidgets>();
        services.AddSingleton<IAppearance, InMemoryAppearance>();
        services.AddSingleton<IAppPreferences, InMemoryPreferences>();
        services.AddSingleton<TabPins>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISelfActionTracker, SelfActionTracker>();
        services.AddSingleton<IAlertWatchStore, AlertWatchStore>();
        services.AddSingleton<AlertTabDot>();
        services.AddSingleton<AlertCountThreshold>();
        services.AddSingleton<IgnoredAlerts>();
        services.AddSingleton<AlertWatcher>();
        services.AddSingleton<AlertWatchCoordinator>();
        services.AddSingleton<NotificationRouter>();

        services.AddTransient<SignInViewModel>();
        services.AddTransient<LicencesViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DevicesViewModel>();
        services.AddTransient<DeviceDetailViewModel>();
        services.AddTransient<AlertsViewModel>();
        services.AddTransient<AlertDetailViewModel>();
        services.AddTransient<HealthViewModel>();
        services.AddTransient<GroupsLocationsViewModel>();
        services.AddTransient<Rules.AlertRulesViewModel>();
        services.AddTransient<Rules.AlertRuleViewModel>();
        services.AddTransient<Rules.AlertTemplateViewModel>();
        services.AddTransient<ThresholdsViewModel>();
        services.AddTransient<Dashboard.CustomiseDashboardViewModel>();

        // One for the app, so a card added in the card picker is announced back on the page it returns to (#140).
        services.AddSingleton<Dashboard.DashboardToast>();
        services.AddTransient<Dashboard.DashboardWelcome>();
        services.AddTransient<Dashboard.AddCardViewModel>();
        services.AddTransient<Dashboard.SensorPickerViewModel>();
        services.AddTransient<Dashboard.GraphPickerViewModel>();
        services.AddTransient<Dashboard.TopCardSetUpViewModel>();
        services.AddTransient<Logs.LogsViewModel>();
        services.AddTransient<Logs.LogEntryViewModel>();
        services.AddTransient<Topology.NeighboursViewModel>();
        services.AddTransient<Topology.NeighbourGroupsViewModel>();
        services.AddTransient<Topology.NeighbourGroupEditorViewModel>();
        services.AddTransient<Topology.NeighbourLinkViewModel>();
        services.AddTransient<Topology.NetworkMapViewModel>();
        services.AddTransient<Map.MapViewModel>();
        // One for Settings and its section pages (#67), which all show and change the same values.
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<MoreViewModel>();
        services.AddTransient<MaintenanceViewModel>();

        // Graylog: independent of the LibreNMS sign-in, as on desktop - see GraylogSetup.
        services.AddSingleton<GraylogApi>();
        services.AddSingleton<IGraylogApi>(sp => sp.GetRequiredService<GraylogApi>());
        services.AddSingleton<IGraylogPasswordProtector, SecureGraylogPasswordProtector>();
        services.AddSingleton<Graylog.GraylogSetup>();
        services.AddSingleton<Graylog.IGraylogConnectionTester, Graylog.GraylogConnectionTester>();
        services.AddSingleton<Topology.NeighbourDirectory>();
        services.AddTransient<Graylog.GraylogViewModel>();
        services.AddTransient<Graylog.GraylogSettingsViewModel>();
        services.AddTransient<Graylog.GraylogMessageViewModel>();
        services.AddTransient<Graylog.GraylogDevicePickerViewModel>();

        // Device View sections.
        services.AddSingleton<DeviceSectionLoader>();
        services.AddTransient<DeviceSectionViewModel>();
        services.AddTransient<DeviceGraphsViewModel>();

        return services;
    }
}
