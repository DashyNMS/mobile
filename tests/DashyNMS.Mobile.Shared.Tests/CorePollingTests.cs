using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class CorePollingTests
{
    [Fact]
    public async Task Widget_and_page_share_Core_device_cache()
    {
        var client = Fakes.Client([Fakes.Device(7, "example.net")]);
        var session = Substitute.For<ISessionService>();
        session.IsConnected.Returns(true);
        using var polling = new CorePolling(client, session, Fakes.Settings());
        Assert.Single(await polling.DevicesAsync());
        Assert.Single((await polling.CachedDevicesAsync())!);
        await client.Devices.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Switching_session_drops_the_previous_device_cache()
    {
        var client = Fakes.Client([Fakes.Device(7, "first.example.net")]);
        var session = Substitute.For<ISessionService>();
        session.IsConnected.Returns(true);
        using var polling = new CorePolling(client, session, Fakes.Settings());
        Assert.Single((await polling.CachedDevicesAsync())!);
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Device>());
        session.StateChanged += Raise.Event();
        Assert.Empty((await polling.CachedDevicesAsync())!);
    }

    [Fact]
    public async Task Rule_details_share_one_bulk_lookup_and_clear_on_session_change()
    {
        var client = Fakes.Client();
        client.Rules.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { new AlertRule { Id = 7, Name = "Example rule" } });
        var session = Substitute.For<ISessionService>();
        using var polling = new CorePolling(client, session, Fakes.Settings());
        Assert.NotNull(await polling.Rules.GetRuleAsync(7));
        Assert.NotNull(await polling.Rules.GetRuleAsync(7));
        await client.Rules.Received(1).ListAsync(Arg.Any<CancellationToken>());
        session.StateChanged += Raise.Event();
        await polling.Rules.GetRuleAsync(7);
        await client.Rules.Received(2).ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Signed_out_requests_fail_without_starting_a_monitor()
    {
        using var polling = new CorePolling(Fakes.Client(), Substitute.For<ISessionService>(), Fakes.Settings());
        await Assert.ThrowsAsync<InvalidOperationException>(() => polling.DevicesAsync());
    }
}
