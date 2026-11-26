using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// Mobile uses Core's settings store as it is. It once needed wrapping: Save()
/// skipped writing when nothing listened for changes, and nothing does on a
/// phone (fixed in DashyNMS/desktop#191).
/// </summary>
public sealed class SettingsStorageTests
{
    [Fact]
    public void Saving_writes_even_with_nothing_listening()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dashynms-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "settings.json");
        try
        {
            var store = new SettingsStore(NullLogger<SettingsStore>.Instance, file);
            store.Load();
            store.Current.ServerUrl = "https://librenms.example/";

            store.Save();

            var reloaded = new SettingsStore(NullLogger<SettingsStore>.Instance, file);
            Assert.Equal("https://librenms.example/", reloaded.Load().ServerUrl);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_app_uses_Cores_store()
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
        using var provider = services.BuildServiceProvider();

        // Core's store holds the real settings; the app reaches it through the switch the demo uses (#160).
        var store = Assert.IsType<DashyNMS.Mobile.Demo.SwitchableSettingsStore>(provider.GetRequiredService<ISettingsStore>());
        Assert.False(store.IsUsingDemo);
        Assert.Same(provider.GetRequiredService<SettingsStore>().Current, store.Current);
    }
}
