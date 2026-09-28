using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.ViewModels;
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
    /// The app head adds <see cref="ISecureStorage"/>, navigation, dialogs and
    /// the launcher on top.
    /// </summary>
    /// <remarks>
    /// Mirrors desktop's <c>AddDesktopNmsCore</c>, minus its DPAPI secret
    /// stores (Windows-only) and the integrations mobile doesn't have screens
    /// for yet (Unimus, Graylog, update checks).
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
        services.AddSingleton<ITokenProtector, SecureTokenProtector>();
        services.AddSingleton<ISessionService, SessionService>();

        services.AddTransient<SignInViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DevicesViewModel>();
        services.AddTransient<DeviceDetailViewModel>();
        services.AddTransient<AlertsViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services;
    }
}
