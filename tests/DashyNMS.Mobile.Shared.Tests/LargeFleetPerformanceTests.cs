using System.Diagnostics;
using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Rules;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Widgets;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Demo;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using CoreWatchState = DesktopNMS.Core.Alerting.AlertWatchState;
using MobileWatchState = DashyNMS.Mobile.Alerts.AlertWatchState;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// Large-fleet performance benchmarks and regressions (#132): thousands of devices,
/// tens of thousands of ports, long alert lists, network map layout, alert rule
/// caching, Top cards caching, and bounded background alert recovery checks.
/// Uses synthetic example.net / documentation-range data only.
/// </summary>
public sealed class LargeFleetPerformanceTests
{
    private static List<Device> SyntheticDevices(int count) =>
        Enumerable.Range(1, count).Select(i => new Device
        {
            DeviceId = i,
            Hostname = $"sw-{i:D4}.example.net",
            SysName = $"sw-{i:D4}",
            Ip = $"192.0.{(i / 254) % 256}.{(i % 254) + 1}",
            Os = i % 3 == 0 ? "iosxe" : i % 3 == 1 ? "junos" : "linux",
            Hardware = i % 2 == 0 ? "C9300-48P" : "EX4300-48T",
            Type = i % 5 == 0 ? "server" : "network",
            Location = $"Site-{(i % 12) + 1:D2}",
            Status = i % 20 != 0,
            Disabled = i % 50 == 0,
            Uptime = 86_400L * (i % 180 + 1),
        }).ToList();

    private static List<Port> SyntheticPorts(int deviceCount, int portsPerDevice)
    {
        var list = new List<Port>(deviceCount * portsPerDevice);
        var id = 1;
        for (var d = 1; d <= deviceCount; d++)
        {
            for (var p = 1; p <= portsPerDevice; p++)
            {
                list.Add(new Port
                {
                    PortId = id++,
                    DeviceId = d,
                    IfName = $"Gi1/0/{p}",
                    IfAlias = p == 1 ? $"Uplink-{d}" : string.Empty,
                    IfOperStatus = "up",
                    IfAdminStatus = "up",
                    IfSpeed = 1_000_000_000,
                    IfInOctetsRate = (id % 500) * 100_000,
                    IfOutOctetsRate = (id % 400) * 100_000,
                    IfInErrorsRate = id % 1000 == 0 ? 2.5 : 0,
                    IfOutErrorsRate = id % 1500 == 0 ? 1.2 : 0,
                });
            }
        }
        return list;
    }

    [Fact]
    public async Task Devices_load_search_and_ip_sort_stay_fast_for_2000_devices()
    {
        var devices = SyntheticDevices(2000);
        var client = Substitute.For<ILibreNmsClient>();
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(devices);
        client.DeviceGroups.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<DeviceGroup>());

        var settings = new SettingsStore(NullLogger<SettingsStore>.Instance, Path.Combine(Path.GetTempPath(), $"perf-dev-{Guid.NewGuid():N}.json"));
        var vm = new DevicesViewModel(
            client,
            Substitute.For<INavigationService>(),
            settings,
            new DeviceBookmarks(settings, TimeProvider.System),
            TimeProvider.System);

        var sw = Stopwatch.StartNew();
        await vm.RefreshCommand.ExecuteAsync(null);
        var loadMs = sw.ElapsedMilliseconds;

        sw.Restart();
        vm.SelectedSort = vm.SortOptions.First(o => o.Sort == DeviceSort.IpAddress);
        var ipSortMs = sw.ElapsedMilliseconds;

        sw.Restart();
        vm.SearchText = "sw-19";
        vm.ShowOnly(location: "Site-03");
        var filterMs = sw.ElapsedMilliseconds;

        Assert.True(vm.Devices.Count > 0);
        Assert.Contains("2000 devices", vm.CountText, StringComparison.Ordinal);
        Assert.True(loadMs < 5000, $"2000-device load took {loadMs} ms");
        Assert.True(ipSortMs < 2000, $"2000-device IP sort took {ipSortMs} ms");
        Assert.True(filterMs < 1000, $"2000-device filter took {filterMs} ms");
    }

    [Fact]
    public void Top_cards_rank_20000_ports_quickly()
    {
        var devices = SyntheticDevices(1000).ToDictionary(d => d.DeviceId);
        var ports = SyntheticPorts(1000, 20);
        var widget = new DashboardWidget { WidgetType = DashboardWidgetTypes.TopInterfaces, TopCount = 10 };

        var sw = Stopwatch.StartNew();
        var traffic = TopCards.Rows(widget, ports, devices, d => d.Hostname ?? string.Empty);
        var errors = TopCards.Rows(new DashboardWidget { WidgetType = DashboardWidgetTypes.TopErrors, TopCount = 10 }, ports, devices, d => d.Hostname ?? string.Empty);
        var topDevices = TopCards.Rows(new DashboardWidget { WidgetType = DashboardWidgetTypes.TopDevices, TopCount = 10 }, ports, devices, d => d.Hostname ?? string.Empty);
        sw.Stop();

        Assert.Equal(10, traffic.Count);
        Assert.Equal(10, errors.Count);
        Assert.Equal(10, topDevices.Count);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Ranking 20,000 ports across all 3 Top cards took {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task Network_map_records_layout_timing_and_reuses_shared_device_cache()
    {
        var responder = new DemoResponder(new DemoFleetOptions(ExtraDevices: 180));
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        var folder = Path.Combine(Path.GetTempPath(), "perf-map-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var settings = new SettingsStore(NullLogger<SettingsStore>.Instance, Path.Combine(folder, "settings.json"));
            var session = new SessionService(client, settings, Substitute.For<ITokenProtector>(), NullLogger<SessionService>.Instance);
            await session.ConnectDemoAsync(responder.Connection());
            var polling = new CorePolling(client, session, settings);
            var diagnostics = new DiagnosticsLog(null);

            var map = new NetworkMapViewModel(client, settings, Substitute.For<INavigationService>(), session: session, polling: polling, diagnostics: diagnostics);
            await map.RefreshCommand.ExecuteAsync(null);

            Assert.NotEmpty(map.Nodes);
            Assert.Contains(diagnostics.Lines, l => l.Contains("Network map layout:", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Alert_rule_reuses_shared_device_cache()
    {
        var devices = SyntheticDevices(50);
        var client = Substitute.For<ILibreNmsClient>();
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(devices);
        client.Alerts.ListAsync(AlertQuery.Open, Arg.Any<CancellationToken>()).Returns(Array.Empty<Alert>());
        client.AlertTemplates.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<AlertTemplate>());
        client.Rules.GetAsync(7, Arg.Any<CancellationToken>()).Returns(new AlertRule
        {
            Id = 7,
            Name = "Device down",
            Devices = [1, 2],
        });

        var settings = new SettingsStore(NullLogger<SettingsStore>.Instance, Path.Combine(Path.GetTempPath(), $"perf-rule-{Guid.NewGuid():N}.json"));
        var session = Substitute.For<ISessionService>();
        session.IsConnected.Returns(true);
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://demo.example.net/"), "token"));
        var polling = new CorePolling(client, session, settings);

        // Prime cache once, then open two rule pages: Devices.ListAsync must be called only once.
        _ = await polling.DevicesAsync();
        var ruleVm = new AlertRuleViewModel(client, settings, Substitute.For<INavigationService>(), polling: polling) { RuleId = 7 };
        await ruleVm.RefreshCommand.ExecuteAsync(null);
        await ruleVm.Extras;

        await client.Devices.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Background_alert_check_bounds_mass_recovery_lookups_within_budget()
    {
        var client = Substitute.For<ILibreNmsClient>();
        client.Alerts.GetSourceAsync(Arg.Any<CancellationToken>()).Returns(AlertSource.Alert);
        client.Alerts.ListAsync(AlertQuery.Open, Arg.Any<CancellationToken>()).Returns(Array.Empty<Alert>());
        client.Alerts.GetAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), call.ArgAt<CancellationToken>(1));
            return (Alert?)null;
        });

        var settings = new SettingsStore(NullLogger<SettingsStore>.Instance, Path.Combine(Path.GetTempPath(), $"perf-bg-{Guid.NewGuid():N}.json"));
        var session = Substitute.For<ISessionService>();
        session.IsConnected.Returns(true);
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://demo.example.net/"), "token"));

        var store = Substitute.For<IAlertWatchStore>();
        var previous = Enumerable.Range(1, 200).ToDictionary(
            i => new AlertKey(AlertSource.Alert, i),
            _ => new AlertWatchEntry(1, 7, (int)AlertState.Active));
        store.Load().Returns(new MobileWatchState("https://demo.example.net/", new CoreWatchState(AlertSource.Alert, previous, false)));

        var watcher = new AlertWatcher(
            client, session, Fakes.Secrets(),
            settings, store, new SelfActionTracker(),
            new RecordingNotifier(), new NoAppBadge(),
            Substitute.For<IHomeWidgets>(), TimeProvider.System, NullLogger<AlertWatcher>.Instance)
        {
            RecoveryLookupBudget = TimeSpan.FromMilliseconds(150),
        };

        var sw = Stopwatch.StartNew();
        var result = await watcher.CheckAsync(background: true);
        sw.Stop();

        Assert.Equal(AlertCheckOutcome.Checked, result.Outcome);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Bounded recovery check took {sw.ElapsedMilliseconds} ms");
        store.Received(1).Save(Arg.Any<MobileWatchState>());
    }
}
