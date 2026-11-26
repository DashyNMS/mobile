using DashyNMS.Mobile.Demo;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DashyNMS.Mobile.Tests;

/// <summary>The demo (#160) runs the real client in process and leaves the real sign-in as it found it.</summary>
public sealed class DemoSessionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "dashynms-demo-" + Guid.NewGuid().ToString("N"));
    private readonly SettingsStore _real;
    private readonly SwitchableSettingsStore _settings;
    private readonly ITokenProtector _tokens = Substitute.For<ITokenProtector>();
    private readonly LibreNmsTransport _transport = new(NullLogger<LibreNmsTransport>.Instance);
    private readonly LibreNmsClient _client;
    private readonly SessionService _session;
    private readonly DemoState _state = new();
    private readonly InMemoryPreferences _phone = new();
    private readonly SwitchablePreferences _preferences;
    private readonly RecordingNavigation _navigation = new();
    private readonly LastKnownData _lastKnown;
    private readonly DemoSession _demo;

    public DemoSessionTests()
    {
        Directory.CreateDirectory(_folder);
        _real = new SettingsStore(NullLogger<SettingsStore>.Instance, Path.Combine(_folder, "settings.json"));
        _real.Current.ServerUrl = "https://nms.example.net/";
        _real.Current.RememberToken = true;
        _real.Save();
        _settings = new SwitchableSettingsStore(_real);
        _client = new LibreNmsClient(_transport, NullLogger<LibreNmsClient>.Instance);
        _lastKnown = new LastKnownData(_folder, _settings, NullLogger<LastKnownData>.Instance, _state);
        _session = new SessionService(_client, _settings, _tokens, NullLogger<SessionService>.Instance, _lastKnown);
        _preferences = new SwitchablePreferences(_phone, _state);
        _demo = new DemoSession(_state, _session, _settings, _navigation, _preferences, folder: Path.Combine(_folder, "demo"));
    }

    public void Dispose()
    {
        _client.Dispose();
        _transport.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task The_demo_runs_on_its_own_settings_and_the_example_network()
    {
        Assert.True(await _demo.EnterAsync());

        Assert.True(_state.IsActive);
        Assert.True(_session.IsConnected);
        Assert.Equal(DesktopNMS.Core.Demo.DemoResponder.Address, _session.Connection!.WebRoot);
        Assert.Equal(DesktopNMS.Core.Demo.DemoResponder.Address.ToString(), _settings.Current.ServerUrl);
        Assert.False(_settings.Current.Notifications.Enabled);
        Assert.True(_settings.Current.IntelligenceSingleChatMode);
        Assert.Equal(24, (await _client.Devices.ListAsync()).Count);
        Assert.Equal(Routes.Main, _navigation.Visits[^1].Route);
    }

    [Fact]
    public async Task Leaving_puts_the_real_sign_in_back_and_keeps_nothing()
    {
        var before = await File.ReadAllTextAsync(Path.Combine(_folder, "settings.json"));
        await _demo.EnterAsync();
        _settings.Current.PinnedDevices.Clear();
        _settings.Save();
        _preferences.Set("tab-pins", "Map");
        await _lastKnown.DevicesAsync(() => _client.Devices.ListAsync());

        await _demo.LeaveAsync();

        Assert.False(_state.IsActive);
        Assert.False(_session.IsConnected);
        Assert.Equal("https://nms.example.net/", _settings.Current.ServerUrl);
        Assert.Equal(before, await File.ReadAllTextAsync(Path.Combine(_folder, "settings.json")));
        Assert.False(Directory.Exists(Path.Combine(_folder, "demo")));
        Assert.False(Directory.Exists(Path.Combine(_folder, "last-known")));
        Assert.Null(_phone.Get("tab-pins"));
        Assert.Equal(Routes.SignIn, _navigation.Visits[^1].Route);
    }

    [Fact]
    public async Task The_saved_token_is_never_read_written_or_cleared()
    {
        await _demo.EnterAsync();
        await _demo.LeaveAsync();

        Assert.Empty(_tokens.ReceivedCalls());
    }

    [Fact]
    public async Task It_only_starts_from_signed_out()
    {
        _client.Connect(new LibreNmsConnection(new Uri("https://nms.example.net/"), "x").WithHandler(() => new DesktopNMS.Core.Demo.DemoMessageHandler(new())));

        Assert.False(await _demo.EnterAsync());
        Assert.False(_state.IsActive);
        Assert.False(_settings.IsUsingDemo);
    }

    [Fact]
    public void A_demo_the_app_closed_in_leaves_nothing_at_the_next_launch()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "demo"));
        File.WriteAllText(Path.Combine(_folder, "demo", "settings.json"), "{}");

        _demo.ClearLeftovers();

        Assert.False(Directory.Exists(Path.Combine(_folder, "demo")));
    }

    [Fact]
    public async Task Diagnostics_share_nothing_in_the_demo()
    {
        var consent = new DiagnosticsConsent(_preferences, reportingAvailable: true, _state);
        consent.SetShareErrors(true);
        consent.SetShareUsage(true);
        Assert.True(consent.ShareErrors);
        await _demo.EnterAsync();

        Assert.False(consent.ShareErrors);
        Assert.False(consent.ShareUsage);
    }
}
