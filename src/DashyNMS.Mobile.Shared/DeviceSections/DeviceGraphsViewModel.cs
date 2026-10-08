using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.DeviceSections;

public sealed record GraphRangeOption(GraphTimeRange Range, string Label);

/// <summary>
/// A device's own LibreNMS graphs, as desktop's Graphs tab: its device-wide,
/// health and wireless graphs in one list, a time range, and the chosen graph
/// drawn by LibreNMS. Or one port's graphs, as desktop's port graphs panel.
/// </summary>
public sealed partial class DeviceGraphsViewModel : ViewModelBase
{
    /// <summary>Requested size: LibreNMS draws at this, the phone scales it to fit.</summary>
    internal const int GraphWidth = 800;
    internal const int GraphHeight = 400;

    private readonly ILibreNmsClient _client;
    private readonly Dictionary<(string Graph, GraphTimeRange Range, bool Dark), string> _cache = new();
    private bool _loadingList;

    [ObservableProperty]
    private GraphType? _selectedGraph;

    [ObservableProperty]
    private GraphRangeOption _selectedRange;

    /// <summary>The page to show - see <see cref="GraphHtml"/>.</summary>
    [ObservableProperty]
    private string? _graphPage;

    [ObservableProperty]
    private string _title = "Graphs";

    public DeviceGraphsViewModel(ILibreNmsClient client)
    {
        _client = client;
        _selectedRange = Ranges[1];
    }

    public int DeviceId { get; private set; }

    /// <summary>The port whose graphs these are, or null for the device's own.</summary>
    public string? PortIfName { get; private set; }

    /// <summary>
    /// The port graphs LibreNMS draws for any port - desktop's list (the rest,
    /// like PAgP or FDB count, only exist on some ports).
    /// </summary>
    internal static IReadOnlyList<GraphType> PortGraphs { get; } =
    [
        new() { Name = "port_bits", Description = "Traffic" },
        new() { Name = "port_upkts", Description = "Unicast packets" },
        new() { Name = "port_nupkts", Description = "Broadcast and multicast packets" },
        new() { Name = "port_errors", Description = "Errors" },
    ];

    /// <summary>Set by the page from the phone's theme.</summary>
    public bool DarkTheme { get; set; }

    public BulkObservableCollection<GraphType> Graphs { get; } = new();

    public IReadOnlyList<GraphRangeOption> Ranges { get; } =
    [
        new(GraphTimeRange.LastHour, "Hour"),
        new(GraphTimeRange.LastDay, "Day"),
        new(GraphTimeRange.LastWeek, "Week"),
        new(GraphTimeRange.LastMonth, "Month"),
        new(GraphTimeRange.LastYear, "Year"),
    ];

    public bool HasNoGraphs => !IsBusy && !_loadingList && Graphs.Count == 0 && !HasError;

    partial void OnSelectedGraphChanged(GraphType? value)
    {
        if (!_loadingList)
        {
            _ = LoadGraphAsync();
        }
    }

    partial void OnSelectedRangeChanged(GraphRangeOption value)
    {
        if (value is null)
        {
            SelectedRange = Ranges[1];
            return;
        }

        _ = LoadGraphAsync();
    }

    /// <param name="graph">
    /// The graph to open on - the one tapped (a dashboard card, the ping
    /// graph), else the first. Opening on the first whatever was tapped was #119.
    /// </param>
    /// <param name="range">The range that graph was shown over, if any.</param>
    public async Task LoadAsync(int deviceId, string? deviceName = null, string? graph = null, GraphTimeRangePreset? range = null)
    {
        DeviceId = deviceId;
        Title = deviceName is null ? "Graphs" : $"Graphs · {deviceName}";
        if (range is { } preset && Ranges.FirstOrDefault(r => r.Range.Preset == preset) is { } shown)
        {
            SelectedRange = shown;
        }

        _loadingList = true;
        try
        {
            await RunAsync(async () =>
            {
                // Desktop merges the same three listings: they never overlap,
                // and all draw through /devices/{id}/{graph}.
                var deviceWide = _client.Graphs.ListAsync(deviceId);
                var health = _client.Graphs.ListHealthAsync(deviceId);
                var wireless = WirelessGraphsAsync(deviceId);
                await Task.WhenAll(deviceWide, health, wireless);

                Graphs.ReplaceAll(deviceWide.Result.Concat(health.Result).Concat(wireless.Result)
                    .OrderBy(g => g.Description, StringComparer.OrdinalIgnoreCase));
            });
        }
        finally
        {
            _loadingList = false;
        }

        OnPropertyChanged(nameof(HasNoGraphs));
        SelectedGraph = Graphs.FirstOrDefault(g => g.Name == graph) ?? Graphs.FirstOrDefault();
        await LoadGraphAsync();
    }

    /// <summary>
    /// One port's graphs, opened from Device View's ports - or from a
    /// dashboard port graph (#172), on its graph and range.
    /// </summary>
    public async Task LoadPortAsync(int deviceId, string ifName, string? portName = null, string? deviceName = null, string? graph = null, GraphTimeRangePreset? range = null)
    {
        DeviceId = deviceId;
        if (range is { } preset && Ranges.FirstOrDefault(r => r.Range.Preset == preset) is { } shown)
        {
            SelectedRange = shown;
        }

        PortIfName = ifName;
        var port = string.IsNullOrWhiteSpace(portName) ? ifName : portName;
        Title = string.IsNullOrWhiteSpace(deviceName) ? port : $"{port} · {deviceName}";

        _loadingList = true;
        try
        {
            Graphs.ReplaceAll(PortGraphs);
        }
        finally
        {
            _loadingList = false;
        }

        OnPropertyChanged(nameof(HasNoGraphs));
        SelectedGraph = Graphs.FirstOrDefault(g => g.Name == graph) ?? Graphs[0];
        await LoadGraphAsync();
    }

    [RelayCommand]
    private void SelectRange(GraphRangeOption? range)
    {
        if (range is not null)
        {
            SelectedRange = range;
        }
    }

    /// <summary>Redraws the current graph, e.g. after the theme changes.</summary>
    [RelayCommand]
    public Task LoadGraphAsync()
    {
        if (SelectedGraph is not { } graph || SelectedRange is not { } range)
        {
            GraphPage = null;
            return Task.CompletedTask;
        }

        var key = (graph.Name, range.Range, DarkTheme);
        if (_cache.TryGetValue(key, out var cached))
        {
            GraphPage = cached;
            return Task.CompletedTask;
        }

        return RunAsync(async () =>
        {
            var svg = PortIfName is { } ifName
                ? await _client.Graphs.GetPortSvgAsync(DeviceId, ifName, graph.Name, range.Range, GraphWidth, GraphHeight)
                : await _client.Graphs.GetSvgAsync(DeviceId, graph.Name, range.Range, GraphWidth, GraphHeight);
            var html = DeviceSections.GraphHtml.Build(svg, DarkTheme);
            _cache[key] = html;

            // Only if it's still the one wanted - a quick second tap may have moved on.
            if (SelectedGraph == graph && SelectedRange == range)
            {
                GraphPage = html;
            }
        });
    }

    /// <summary>Best effort, as desktop: most devices have none, and failing shouldn't lose the rest.</summary>
    private async Task<IReadOnlyList<GraphType>> WirelessGraphsAsync(int deviceId)
    {
        try
        {
            return await _client.Graphs.ListWirelessAsync(deviceId);
        }
        catch (LibreNmsApiException)
        {
            return [];
        }
    }
}
