using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class DeviceLinksTests
{
    [Theory]
    [InlineData("10.0.0.1", "core-sw", "ssh://10.0.0.1/")]
    [InlineData(null, "core-sw.example.net", "ssh://core-sw.example.net/")]
    [InlineData("2001:db8::1", "core-sw", "ssh://[2001:db8::1]/")]
    public void Uses_the_ip_then_the_hostname_as_desktop(string? ip, string hostname, string expected)
    {
        Assert.Equal(expected, DeviceLinks.For(DeviceLinks.Ssh, new Device { Ip = ip, Hostname = hostname })!.ToString());
    }

    [Fact]
    public void Nothing_to_open_without_an_address() =>
        Assert.Null(DeviceLinks.For(DeviceLinks.Telnet, new Device { Hostname = " " }));
}

public sealed class OpenInAppTests
{
    private readonly ILibreNmsClient _client = Fakes.Client();
    private readonly ILauncherService _launcher = Substitute.For<ILauncherService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly DeviceDetailViewModel _vm;

    public OpenInAppTests()
    {
        _client.Devices.GetAsync("7", Arg.Any<CancellationToken>()).Returns(Fakes.Device(7, "edge-rtr", ip: "10.0.0.7"));
        var settings = Fakes.Settings();
        _vm = new DeviceDetailViewModel(_client, settings, _launcher, new DeviceBookmarks(settings, TimeProvider.System), _dialogs, new RecordingNavigation());
    }

    [Fact]
    public async Task Ssh_and_telnet_open_in_the_phones_app_for_the_link()
    {
        _launcher.TryOpenAppAsync(Arg.Any<Uri>()).Returns(true);
        await _vm.LoadAsync(7);

        await _vm.OpenSshCommand.ExecuteAsync(null);
        await _vm.OpenTelnetCommand.ExecuteAsync(null);

        await _launcher.Received(1).TryOpenAppAsync(new Uri("ssh://10.0.0.7"));
        await _launcher.Received(1).TryOpenAppAsync(new Uri("telnet://10.0.0.7"));
        await _dialogs.DidNotReceiveWithAnyArgs().AlertAsync(default!, default!);
    }

    [Fact]
    public async Task Says_so_when_no_app_handles_the_link()
    {
        _launcher.TryOpenAppAsync(Arg.Any<Uri>()).Returns(false);
        await _vm.LoadAsync(7);

        await _vm.OpenSshCommand.ExecuteAsync(null);

        await _dialogs.Received(1).AlertAsync("No SSH app", Arg.Is<string>(m => m.Contains("ssh://")));
    }

    [Fact]
    public void Nothing_to_open_before_the_device_loads() => Assert.False(_vm.OpenSshCommand.CanExecute(null));
}
