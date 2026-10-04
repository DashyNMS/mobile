using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Devices;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

/// <summary>The Top interfaces, Top errors and Top devices cards (#103).</summary>
public sealed class TopCardsTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly ILibreNmsClient _client = Fakes.Client(devices:
    [
        Fakes.Device(1, "core-sw"),
        Fakes.Device(2, "edge-rtr", up: false),
    ]);

    private static readonly Port[] Ports =
    [
        // 1 Gbps port at 50 Mbps in, 100 Mbps out (octets per second).
        new() { PortId = 10, DeviceId = 1, IfName = "Gi1/0/1", IfAlias = "Uplink", IfSpeed = 1_000_000_000, IfInOctetsRate = 6_250_000, IfOutOctetsRate = 12_500_000, IfInErrorsRate = 0.4 },
        new() { PortId = 11, DeviceId = 1, IfName = "Gi1/0/2", IfSpeed = 1_000_000_000, IfInOctetsRate = 125_000, IfOutOctetsRate = 0, IfOutErrorsRate = 3 },
        new() { PortId = 20, DeviceId = 2, IfName = "ge-0/0/0", IfAlias = "ge-0/0/0", IfSpeed = 100_000_000, IfInOctetsRate = 12_500_000, IfOutOctetsRate = 250_000 },
        new() { PortId = 21, DeviceId = 2, IfName = "ge-0/0/1" },
    ];

    public TopCardsTests()
    {
        _settings = Fakes.Settings(_appSettings);
        _client.Ports.ListAllStatusAsync(Arg.Any<CancellationToken>()).Returns(Ports);
    }

    private static IReadOnlyList<TopRow> Rows(DashboardWidget widget) =>
        TopCards.Rows(widget, Ports, new Dictionary<int, Device> { [1] = Fakes.Device(1, "core-sw") }, d => d.Hostname);

    [Fact]
    public void Top_interfaces_lists_the_busiest_ports_in_bits_with_how_full_each_is()
    {
        var rows = Rows(new DashboardWidget { WidgetType = DashboardLayout.TopInterfaces });

        Assert.Equal([10, 20, 11], rows.Select(r => r.PortId));
        Assert.Equal("core-sw", rows[0].Title);
        Assert.Equal("Gi1/0/1 · Uplink", rows[0].Subtitle);
        Assert.Equal("50 Mbps", rows[0].InText);
        Assert.Equal("100 Mbps", rows[0].OutText);
        Assert.Equal(0.1, rows[0].Bar, precision: 9); // 100 Mbps of 1 Gbps
        Assert.Equal("ge-0/0/0", rows[1].Subtitle); // the description says nothing more
        Assert.Equal("Device 2", rows[1].Title); // not in the device list
        Assert.Equal(DeviceState.Up, rows[0].DeviceState);
    }

    [Fact]
    public void Ranking_by_in_and_the_row_count_follow_the_cards_options()
    {
        var widget = new DashboardWidget { WidgetType = DashboardLayout.TopInterfaces, TopRankBy = RankBy.In, TopCount = 5 };
        Assert.Equal([20, 10, 11], Rows(widget).Select(r => r.PortId));

        // A count desktop doesn't offer shows as its default, as there.
        widget.TopCount = 7;
        widget.TopRankBy = RankBy.Total;
        Assert.Equal(3, TopCards.CountOf(widget));
        widget.TopCount = 1;
        Assert.Equal(3, Rows(widget).Count);
    }

    [Fact]
    public void Top_errors_is_amber_for_any_errors_and_red_from_one_a_second()
    {
        var rows = Rows(new DashboardWidget { WidgetType = DashboardLayout.TopErrors });

        Assert.Equal([11, 10], rows.Select(r => r.PortId)); // error-free ports left out
        Assert.Equal(RowStatus.Critical, rows[0].Severity);
        Assert.Equal("3/s", rows[0].OutText);
        Assert.Equal(RowStatus.Warning, rows[1].Severity);
        Assert.Equal("0.4/s", rows[1].InText);
        Assert.Equal(1, rows[0].Bar, precision: 9); // against the worst

        var all = Rows(new DashboardWidget { WidgetType = DashboardLayout.TopErrors, TopHideQuiet = false, TopCount = 10 });
        Assert.Equal(4, all.Count);
        Assert.Equal(RowStatus.None, all[^1].Severity);
    }

    [Fact]
    public void Top_devices_sums_each_devices_ports_and_says_its_not_throughput()
    {
        var rows = Rows(new DashboardWidget { WidgetType = DashboardLayout.TopDevices });

        Assert.Equal([1, 2], rows.Select(r => r.DeviceId));
        Assert.Null(rows[0].PortId);
        Assert.Equal("2 active ports", rows[0].Subtitle);
        Assert.Equal("1 active port", rows[1].Subtitle);
        Assert.Equal("51 Mbps", rows[0].InText);
        Assert.Equal(1, rows[0].Bar, precision: 9);
        Assert.NotNull(TopCards.Footnote(DashboardLayout.TopDevices));
        Assert.Null(TopCards.Footnote(DashboardLayout.TopInterfaces));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(840, "840 bps")]
    [InlineData(840_000, "840 Kbps")]
    [InlineData(1_200_000_000, "1.2 Gbps")]
    [InlineData(25_000_000, "25 Mbps")]
    public void Rates_read_as_LibreNMS_writes_them(double bits, string expected) =>
        Assert.Equal(expected, TopCards.Bits(bits));

    [Fact]
    public async Task Every_top_card_shares_one_fetch_of_the_networks_ports()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.TopInterfaces);
        DashboardLayout.Add(_appSettings, DashboardLayout.TopErrors);
        DashboardLayout.Add(_appSettings, DashboardLayout.TopDevices);
        var vm = new DashboardViewModel(_client, _settings, new RecordingNavigation(), new DeviceBookmarks(_settings, TimeProvider.System));

        await vm.RefreshCommand.ExecuteAsync(null);

        await _client.Ports.Received(1).ListAllStatusAsync(Arg.Any<CancellationToken>());
        var interfaces = vm.Cards.Single(c => c.Type == DashboardLayout.TopInterfaces);
        Assert.Equal(3, interfaces.TopRows.Count);
        Assert.Equal("core-sw", interfaces.TopRows[0].Title);
        Assert.Equal(DeviceState.Down, vm.Cards.Single(c => c.Type == DashboardLayout.TopDevices).TopRows[1].DeviceState);
        Assert.False(interfaces.HasNoTopRows);
    }

    [Fact]
    public async Task No_top_card_no_ports_fetched()
    {
        var vm = new DashboardViewModel(_client, _settings, new RecordingNavigation(), new DeviceBookmarks(_settings, TimeProvider.System));

        await vm.RefreshCommand.ExecuteAsync(null);

        await _client.Ports.DidNotReceive().ListAllStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_ranking_chip_saves_and_reranks_without_fetching_again()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.TopInterfaces);
        var vm = new DashboardViewModel(_client, _settings, new RecordingNavigation(), new DeviceBookmarks(_settings, TimeProvider.System));
        await vm.RefreshCommand.ExecuteAsync(null);
        var card = vm.Cards.Single(c => c.Type == DashboardLayout.TopInterfaces);

        card.RankCommand.Execute("In");

        Assert.True(card.RanksByIn);
        Assert.Equal(20, card.TopRows[0].PortId);
        Assert.Equal("In", _appSettings.DashboardWidgets.Single(w => w.WidgetType == DashboardLayout.TopInterfaces).TopRankByName);
        _settings.Received().Save();
        await _client.Ports.Received(1).ListAllStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_healthy_network_says_so()
    {
        _client.Ports.ListAllStatusAsync(Arg.Any<CancellationToken>()).Returns([Ports[3]]);
        DashboardLayout.Add(_appSettings, DashboardLayout.TopErrors);
        var vm = new DashboardViewModel(_client, _settings, new RecordingNavigation(), new DeviceBookmarks(_settings, TimeProvider.System));

        await vm.RefreshCommand.ExecuteAsync(null);

        var card = vm.Cards.Single(c => c.Type == DashboardLayout.TopErrors);
        Assert.True(card.HasNoTopRows);
        Assert.Equal("No interface errors.", card.TopEmptyText);
    }

    [Fact]
    public async Task A_heading_ranks_by_it_and_again_by_both()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.TopInterfaces);
        var vm = new DashboardViewModel(_client, _settings, new RecordingNavigation(), new DeviceBookmarks(_settings, TimeProvider.System));
        await vm.RefreshCommand.ExecuteAsync(null);
        var card = vm.Cards.Single();

        card.RankHeadingCommand.Execute("In");
        Assert.True(card.RanksByIn);

        card.RankHeadingCommand.Execute("In");
        Assert.True(card.RanksByTotal);
    }

    [Fact]
    public async Task The_top_chip_chooses_how_many_rows()
    {
        DashboardLayout.Add(_appSettings, DashboardLayout.TopInterfaces);
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ChooseAsync("Rows", Arg.Any<IReadOnlyList<string>>()).Returns("Top 10");
        var vm = new DashboardViewModel(_client, _settings, new RecordingNavigation(), new DeviceBookmarks(_settings, TimeProvider.System), dialogs: dialogs);
        await vm.RefreshCommand.ExecuteAsync(null);
        var card = vm.Cards.Single();
        Assert.Equal("Top 3", card.TopCountText);

        await card.ChooseTopCountCommand.ExecuteAsync(null);

        Assert.Equal("Top 10", card.TopCountText);
        Assert.Equal(10, _appSettings.DashboardWidgets.Single().TopCount);
        _settings.Received().Save();
    }

    [Fact]
    public async Task Set_up_saves_desktops_fields_and_a_title_of_its_own()
    {
        var widget = DashboardLayout.Add(_appSettings, DashboardLayout.TopErrors);
        var vm = new TopCardSetUpViewModel(_settings, new RecordingNavigation()) { WidgetId = widget.Id };
        vm.Load();
        Assert.True(vm.IsErrors);
        Assert.Equal(3, vm.SelectedCount);
        Assert.Equal(string.Empty, vm.CardTitle);

        vm.SelectedCount = 10;
        vm.SelectedRank = vm.Ranks.Single(r => r.RankBy == RankBy.Out);
        vm.HideQuiet = false;
        vm.CardTitle = "Core errors";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(10, widget.TopCount);
        Assert.Equal("Out", widget.TopRankByName);
        Assert.False(widget.TopHideQuiet);
        Assert.Equal("Core errors", widget.Title);
        Assert.True(DashboardLayout.HasOwnTitle(widget));
    }
}
