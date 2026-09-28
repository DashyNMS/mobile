using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Storage;
using DashyNMS.Mobile.ViewModels;
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
    public static IReadOnlyList<string> SecretKeys { get; } = [SecureTokenProtector.Key];

    /// <summary>
    /// Core's LibreNMS client and settings, the session, and the view models.
    /// The app head adds <see cref="ISecureStorage"/>, navigation, dialogs, the
    /// launcher and the platform's notifications and background scheduling on top.
    /// </summary>
    /// <remarks>
    /// Mirrors desktop's <c>AddDesktopNmsCore</c>, minus its DPAPI secret
    /// stores (Windows-only) and the integrations mobile doesn't have screens
    /// for yet (Unimus, Graylog, update checks).
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

        // Alert notifications. The app head supplies IAlertNotifier and
        // IBackgroundAlertScheduler for its platform.
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
        services.AddTransient<SettingsViewModel>();

        return services;
    }
}
