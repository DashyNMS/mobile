using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// The app head adds only the platform services below on top of
/// AddDashyNmsMobile - so if this resolves, a missing registration can't
/// first show up as a crash on a phone.
/// </summary>
public sealed class ServiceRegistrationTests
{
    [Theory]
    [InlineData(typeof(SignInViewModel))]
    [InlineData(typeof(DashboardViewModel))]
    [InlineData(typeof(DevicesViewModel))]
    [InlineData(typeof(DeviceDetailViewModel))]
    [InlineData(typeof(AlertsViewModel))]
    [InlineData(typeof(SettingsViewModel))]
    [InlineData(typeof(MaintenanceViewModel))]
    [InlineData(typeof(DeviceSections.DeviceSectionViewModel))]
    [InlineData(typeof(DeviceSections.DeviceGraphsViewModel))]
    [InlineData(typeof(AlertDetailViewModel))]
    [InlineData(typeof(HealthViewModel))]
    [InlineData(typeof(GroupsLocationsViewModel))]
    [InlineData(typeof(AlertWatcher))]
    [InlineData(typeof(AlertWatchCoordinator))]
    [InlineData(typeof(NotificationRouter))]
    public void Every_view_model_and_alert_service_resolves(Type viewModel)
    {
        var services = new ServiceCollection()
            .AddDashyNmsMobile()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(Substitute.For<ISecureStorage>())
            .AddSingleton(Substitute.For<INavigationService>())
            .AddSingleton(Substitute.For<IDialogService>())
            .AddSingleton(Substitute.For<ILauncherService>())
            .AddSingleton(Substitute.For<IShareService>())
            .AddSingleton(Substitute.For<IAlertNotifier>())
            .AddSingleton(Substitute.For<IBackgroundAlertScheduler>());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService(viewModel));
    }
}
