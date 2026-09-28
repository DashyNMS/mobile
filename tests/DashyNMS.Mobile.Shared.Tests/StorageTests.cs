using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Storage;
using DesktopNMS.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

public sealed class MobileSettingsStoreTests
{
    private readonly ISettingsStore _inner = Substitute.For<ISettingsStore>();
    private readonly MobileSettingsStore _store;

    public MobileSettingsStoreTests()
    {
        _inner.Current.Returns(new AppSettings());
        _store = new MobileSettingsStore(_inner);
    }

    [Fact]
    public void Save_writes_even_with_nobody_listening()
    {
        // Desktop's own Save() skips the write when Changed has no subscriber.
        _store.Save();

        _inner.Received(1).SaveQuietly();
        _inner.DidNotReceive().Save();
    }

    [Fact]
    public void Save_still_tells_listeners()
    {
        AppSettings? seen = null;
        _store.Changed += (_, s) => seen = s;

        _store.Save();

        Assert.Same(_inner.Current, seen);
    }

    [Fact]
    public void Replace_writes_too()
    {
        var replacement = new AppSettings();

        _store.Replace(replacement);

        _inner.Received(1).Replace(replacement);
        _inner.Received(1).SaveQuietly();
    }

    [Fact]
    public void The_app_gets_the_wrapped_store()
    {
        var services = new ServiceCollection()
            .AddDashyNmsMobile()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(Substitute.For<ISecureStorage>())
            .AddSingleton(Substitute.For<INavigationService>())
            .AddSingleton(Substitute.For<IDialogService>())
            .AddSingleton(Substitute.For<ILauncherService>())
            .AddSingleton(Substitute.For<IAlertNotifier>())
            .AddSingleton(Substitute.For<IBackgroundAlertScheduler>());
        using var provider = services.BuildServiceProvider();

        Assert.IsType<MobileSettingsStore>(provider.GetRequiredService<ISettingsStore>());
    }
}

public sealed class MobileStorageTests
{
    [Fact]
    public void Creates_the_data_folder_that_a_fresh_install_lacks()
    {
        // Linux takes ApplicationData from XDG_CONFIG_HOME, so point it at a
        // folder that doesn't exist yet - as on a newly installed app.
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var fresh = Path.Combine(Path.GetTempPath(), $"dashynms-{Guid.NewGuid():N}", ".config");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", fresh);
            Assert.Equal(string.Empty, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

            var folder = MobileStorage.EnsureDataFolder();

            Assert.Equal(fresh, folder);
            Assert.True(Directory.Exists(fresh));
            Assert.Equal(fresh, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
            Directory.Delete(Path.GetDirectoryName(fresh)!, recursive: true);
        }
    }
}
