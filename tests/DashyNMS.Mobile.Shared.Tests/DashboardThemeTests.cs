using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

/// <summary>Live appearance switches re-style loaded dashboard and ping graphs without refetching (#703).</summary>
public sealed class DashboardThemeTests
{
    private const string RrdSvg =
        """<svg xmlns="http://www.w3.org/2000/svg" width="800" height="400"><rect width="800" height="400" style="fill:rgb(100%,100%,100%);"/><path d="M0 0 L10 10" style="stroke:rgb(0%,0%,0%);"/></svg>""";

    [Fact]
    public async Task Dashboard_graph_redraws_in_new_theme_without_refetching()
    {
        var appSettings = new AppSettings();
        var cardWidget = DashboardLayout.Add(appSettings, DashboardLayout.Graph);
        cardWidget.Title = "core-sw-01 traffic";
        cardWidget.GraphDeviceId = 1;
        cardWidget.GraphName = "device_bits";
        cardWidget.GraphTimeRangePreset = GraphTimeRangePreset.Day;

        var settings = Fakes.Settings(appSettings);
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw-01")]);
        client.Graphs.GetSvgAsync(1, "device_bits", Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false)
            .Returns(RrdSvg);

        var vm = new DashboardViewModel(client, settings, new RecordingNavigation(), new DeviceBookmarks(settings, TimeProvider.System))
        {
            DarkTheme = true,
        };

        await vm.RefreshCommand.ExecuteAsync(null);

        var card = vm.Cards.Single(c => c.Type == DashboardLayout.Graph);
        Assert.NotNull(card.GraphPage);
        Assert.Contains("background: #11141A;", card.GraphPage);

        vm.DarkTheme = false;
        vm.RedrawTheme();

        Assert.NotNull(card.GraphPage);
        Assert.Contains("background: #FFFFFF;", card.GraphPage);
        Assert.Equal("core-sw-01 traffic", card.Title);
        Assert.Equal("Day ▾", card.GraphRangeText);

        vm.DarkTheme = true;
        vm.RedrawTheme();

        Assert.Contains("background: #11141A;", card.GraphPage);
        await client.Graphs.Received(1).GetSvgAsync(1, "device_bits", Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false);
    }

    [Fact]
    public async Task Dashboard_graph_uses_latest_theme_when_appearance_changes_during_load()
    {
        var appSettings = new AppSettings();
        var cardWidget = DashboardLayout.Add(appSettings, DashboardLayout.Graph);
        cardWidget.GraphDeviceId = 1;
        cardWidget.GraphName = "device_bits";

        var settings = Fakes.Settings(appSettings);
        var client = Fakes.Client(devices: [Fakes.Device(1, "core-sw-01")]);
        var pending = new TaskCompletionSource<string>();
        client.Graphs.GetSvgAsync(1, "device_bits", Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false)
            .Returns(_ => pending.Task);

        var vm = new DashboardViewModel(client, settings, new RecordingNavigation(), new DeviceBookmarks(settings, TimeProvider.System))
        {
            DarkTheme = true,
        };

        var refresh = vm.RefreshCommand.ExecuteAsync(null);
        vm.DarkTheme = false;
        vm.RedrawTheme();
        pending.SetResult(RrdSvg);
        await refresh;

        var card = vm.Cards.Single(c => c.Type == DashboardLayout.Graph);
        Assert.NotNull(card.GraphPage);
        Assert.Contains("background: #FFFFFF;", card.GraphPage);
    }

    [Fact]
    public async Task Device_detail_ping_graph_redraws_in_new_theme_without_refetching()
    {
        var device = Fakes.Device(1, "core-sw-01");
        var client = Fakes.Client(devices: [device]);
        client.Devices.GetAsync("1", Arg.Any<CancellationToken>()).Returns(device);
        client.Graphs.GetSvgAsync(1, DeviceDetailViewModel.PingGraphName, Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false)
            .Returns(RrdSvg);

        var settings = Fakes.Settings(new AppSettings());
        var vm = new DeviceDetailViewModel(
            client,
            settings,
            Substitute.For<ILauncherService>(),
            new DeviceBookmarks(settings, TimeProvider.System),
            Substitute.For<IDialogService>(),
            new RecordingNavigation())
        {
            DarkTheme = true,
        };

        await vm.LoadAsync(1);
        await vm.Extras;
        Assert.NotNull(vm.PingGraphPage);
        Assert.Contains("background: #11141A;", vm.PingGraphPage);

        vm.DarkTheme = false;
        vm.RedrawTheme();
        Assert.Contains("background: #FFFFFF;", vm.PingGraphPage);

        await client.Graphs.Received(1).GetSvgAsync(1, DeviceDetailViewModel.PingGraphName, Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), false);
    }
}
