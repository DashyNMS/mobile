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
/// drawn by LibreNMS.
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

    /// <summary>Set by the page from the phone's theme.</summary>
    public bool DarkTheme { get; set; }

    public ObservableCollection<GraphType> Graphs { get; } = new();

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

    public async Task LoadAsync(int deviceId, string? deviceName = null)
    {
        DeviceId = deviceId;
        Title = deviceName is null ? "Graphs" : $"Graphs · {deviceName}";

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

                Graphs.Clear();
                foreach (var graph in deviceWide.Result.Concat(health.Result).Concat(wireless.Result)
                             .OrderBy(g => g.Description, StringComparer.OrdinalIgnoreCase))
                {
                    Graphs.Add(graph);
                }
            });
        }
        finally
        {
            _loadingList = false;
        }

        OnPropertyChanged(nameof(HasNoGraphs));
        SelectedGraph = Graphs.FirstOrDefault();
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
            var svg = await _client.Graphs.GetSvgAsync(DeviceId, graph.Name, range.Range, GraphWidth, GraphHeight);
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
