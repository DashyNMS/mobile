using System.Net;
using System.Text;
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
        var coverage = _search.Coverage;

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
        Assert.Same(coverage, _search.Coverage);
    }

    [Fact]
    public async Task Device_only_matches_omit_missing_details_and_open_in_full()
    {
        await _search.SearchForAsync("core");

        Assert.True(_search.IsList);
        Assert.Equal("Found in monitored devices. Switch MAC tables were not checked.", _search.CoverageText);
        Assert.False(_search.HasBlindSpots);
        Assert.True(_search.HasSourceDetails);
        _search.ToggleSourcesCommand.Execute(null);
        Assert.True(_search.SourcesExpanded);
        Assert.NotEmpty(_search.Rows);
        Assert.All(_search.Rows, row =>
        {
            Assert.True(row.HasName);
            Assert.False(row.HasMac);
            Assert.False(row.HasNeighbour);
            Assert.Equal($"{row.OpenName}. {row.NameText}", row.Description);
        });

        await _search.Rows[0].OpenCommand.ExecuteAsync(null);

        Assert.True(_search.IsSingle);
        Assert.True(_search.Endpoint!.IsMonitored);
        Assert.False(_search.SourcesExpanded);
    }

    [Fact]
    public async Task List_rows_show_known_macs_and_router_names_and_sort_missing_macs_last()
    {
        await _search.SearchForAsync("192.0.2.0/24");

        var row = _search.Rows.First(r => r.HasMac && r.HasNeighbour);
        Assert.Contains(row.MacText, row.Description, StringComparison.Ordinal);
        Assert.Contains(row.NeighbourText, row.Description, StringComparison.Ordinal);
        Assert.Contains(_search.Rows, r => !r.HasMac);

        _search.ChooseSortCommand.Execute(SearchSort.Mac);

        Assert.Equal(_search.Rows.OrderByDescending(r => r.HasMac), _search.Rows);
    }

    [Fact]
    public async Task Nothing_found_says_whether_every_source_answered()
    {
        await _search.SearchForAsync("198.51.100.250");

        Assert.True(_search.IsNotFound);
        Assert.Contains("198.51.100.250", _search.NotFoundTitle, StringComparison.Ordinal);
        Assert.NotNull(_search.CoverageText);
    }

    [Fact]
    public async Task Paging_keeps_the_completed_query_coverage_and_disclosure()
    {
        await _session.ConnectDemoAsync(new DemoResponder(new DemoFleetOptions(ExtraDevices: 30)).Connection());
        await _search.SearchForAsync("lab-sw");
        var coverage = _search.Coverage;
        var summary = _search.CoverageText;
        _search.ToggleSourcesCommand.Execute(null);

        Assert.True(_search.HasMore);
        await _search.NextPageCommand.ExecuteAsync(null);
        Assert.True(_search.HasEarlier);
        Assert.Same(coverage, _search.Coverage);
        Assert.Equal(summary, _search.CoverageText);
        Assert.True(_search.SourcesExpanded);

        await _search.PreviousPageCommand.ExecuteAsync(null);
        Assert.False(_search.HasEarlier);
        Assert.Same(coverage, _search.Coverage);
    }

    [Fact]
    public async Task A_denied_arp_lookup_does_not_confirm_absence()
    {
        var fixture = new DemoResponder().Connection().WithHandler(() => new DeniedArpHandler());
        await _session.ConnectDemoAsync(fixture);

        await _search.SearchForAsync("198.51.100.250");

        Assert.True(_search.IsNotFound);
        Assert.Contains("couldn't be confirmed absent", _search.NotFoundTitle, StringComparison.Ordinal);
        Assert.Contains("ARP tables: permission denied.", _search.BlindSpotText, StringComparison.Ordinal);
        Assert.Contains(_search.Coverage, source => source.Source == SearchSource.Arp && source.State == SourceState.PermissionDenied);
    }

    private sealed class DeniedArpHandler : HttpMessageHandler
    {
        private readonly DemoResponder _demo = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            var reply = path.StartsWith("/api/v0/resources/ip/arp", StringComparison.Ordinal)
                ? new DemoResponse("""{"status":"error","message":"Forbidden"}""", Status: 403)
                : _demo.Respond(request.Method.Method, path);
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)reply.Status)
            {
                Content = new StringContent(reply.Body, Encoding.UTF8, reply.ContentType),
            });
        }
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
