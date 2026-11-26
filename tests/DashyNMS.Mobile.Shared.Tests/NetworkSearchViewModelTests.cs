using DashyNMS.Mobile.Search;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Demo;
using DesktopNMS.Core.Search;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DashyNMS.Mobile.Tests;

/// <summary>Network Search (#129) over Core's example network, in process: example.net names and documentation addresses only.</summary>
public sealed class NetworkSearchViewModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "dashynms-search-" + Guid.NewGuid().ToString("N"));
    private readonly SettingsStore _settings;
    private readonly LibreNmsTransport _transport = new(NullLogger<LibreNmsTransport>.Instance);
    private readonly LibreNmsClient _client;
    private readonly SessionService _session;
    private readonly RecordingNavigation _navigation = new();
    private readonly IClipboardText _clipboard = Substitute.For<IClipboardText>();
    private readonly IShareService _share = Substitute.For<IShareService>();
    private readonly NetworkSearchViewModel _search;

    public NetworkSearchViewModelTests()
    {
        Directory.CreateDirectory(_folder);
        _settings = new SettingsStore(NullLogger<SettingsStore>.Instance, Path.Combine(_folder, "settings.json"));
        _client = new LibreNmsClient(_transport, NullLogger<LibreNmsClient>.Instance);
        _session = new SessionService(_client, _settings, Substitute.For<ITokenProtector>(), NullLogger<SessionService>.Instance);
        _session.ConnectDemoAsync(new DemoResponder().Connection()).GetAwaiter().GetResult();
        _search = new NetworkSearchViewModel(_client, _settings, _session, _navigation, _clipboard, _share);
    }

    public void Dispose()
    {
        _client.Dispose();
        _transport.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task One_address_leads_with_the_port_it_plugs_into()
    {
        await _search.SearchForAsync("192.0.2.117");

        Assert.True(_search.IsSingle);
        var endpoint = _search.Endpoint!;
        Assert.True(endpoint.HasMacLocations);
        var port = endpoint.MacLocations[0];
        Assert.Equal("Gi1/0/19", port.PortName);
        Assert.StartsWith("Direct", port.KindText, StringComparison.Ordinal);
        Assert.Contains(endpoint.AllLocations, l => l.IsUplink);
        Assert.Equal(["192.0.2.117"], _search.Recent);
    }

    [Fact]
    public async Task An_address_two_macs_answer_for_is_a_conflict()
    {
        await _search.SearchForAsync("192.0.2.200");

        Assert.True(_search.IsSingle);
        Assert.True(_search.Endpoint!.HasConflicts);
    }

    [Fact]
    public async Task A_network_is_a_list_with_source_chips_and_an_order()
    {
        await _search.SearchForAsync("192.0.2.0/24");

        Assert.True(_search.IsList);
        Assert.True(_search.Rows.Count > 5);
        Assert.NotNull(_search.ListHeading);

        _search.ChooseSortCommand.Execute(SearchSort.Name);
        Assert.Equal(_search.Rows.Where(r => r.HasName).Select(r => r.NameText).Order(NaturalOrder.Instance), _search.Rows.Where(r => r.HasName).Select(r => r.NameText));

        if (_search.HasSourceChips)
        {
            var all = _search.Rows.Count;
            foreach (var chip in _search.SourceChips) chip.IsSelected = false;
            Assert.True(_search.ShowNoMatchingRows);
            _search.SourceChips[0].IsSelected = true;
            Assert.InRange(_search.Rows.Count, 1, all);
        }
    }

    [Fact]
    public async Task Nothing_found_says_whether_every_source_answered()
    {
        await _search.SearchForAsync("198.51.100.250");

        Assert.True(_search.IsNotFound);
        Assert.Contains("198.51.100.250", _search.NotFoundTitle, StringComparison.Ordinal);
        Assert.NotEmpty(_search.NotFoundDetail);
        Assert.NotNull(_search.CoverageText);
    }

    [Fact]
    public async Task Rows_and_values_open_the_device_and_copy()
    {
        await _search.SearchForAsync("192.0.2.117");
        _search.Endpoint!.MacLocations[0].OpenDeviceCommand.Execute(null);
        _search.Endpoint.Addresses[0].CopyCommand.Execute(null);

        Assert.Equal(Routes.DeviceDetail, _navigation.Visits[^1].Route);
        await _clipboard.Received().SetTextAsync("192.0.2.117");
    }

    [Fact]
    public async Task Results_share_as_csv()
    {
        await _search.SearchForAsync("192.0.2.0/24");
        await _search.ShareCommand.ExecuteAsync(null);

        await _share.Received().ShareTextFileAsync(Arg.Is<string>(n => n.EndsWith(".csv", StringComparison.Ordinal)), Arg.Any<string>(), "text/csv", Arg.Any<string>());
    }

    [Fact]
    public async Task Another_connection_drops_the_results()
    {
        await _search.SearchForAsync("192.0.2.117");
        _session.LeaveDemo();

        Assert.False(_search.IsSingle);
        Assert.Equal(string.Empty, _search.Query);
    }

    [Fact]
    public async Task Macs_follow_desktops_format_setting()
    {
        await _search.SearchForAsync("192.0.2.117");
        _settings.Current.NetworkSearchMacFormat = nameof(MacAddressFormat.Dotted);
        _settings.Save();

        Assert.Contains('.', _search.Endpoint!.Macs[0].Value.Value);
    }

    [Fact]
    public async Task Monitored_device_neighbours_resolve_through_core_polling()
    {
        using var polling = new CorePolling(_client, _session, _settings);
        var search = new NetworkSearchViewModel(_client, _settings, _session, _navigation, _clipboard, _share, polling: polling);

        await search.SearchForAsync("core-sw-01.example.net");

        Assert.True(search.IsSingle);
        var endpoint = search.Endpoint!;
        Assert.True(endpoint.IsMonitored);
        Assert.True(endpoint.HasConnections);
        Assert.Contains(endpoint.AllConnections!, c => c.IsKnownDevice && c.OpenCommand.CanExecute(null) && c.StateText != "Not monitored");
    }
}
