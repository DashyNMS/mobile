using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Graphs;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.DeviceSections;

/// <summary>A device-wide graph or a port, identified by the interface name the graph API expects.</summary>
public sealed record GraphTarget(string Label, string? IfName = null);

public sealed record GraphRangeOption(GraphTimeRange Range, string Label);

/// <summary>A range as a chip on the Graphs page (#158): Hour to Year, the chosen one on.</summary>
public sealed partial class GraphRangeChip(GraphRangeOption option) : ObservableObject
{
    public GraphRangeOption Option { get; } = option;

    public string Label => Option.Label;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// One series in the Graphs page's own legend (#158): its colour, name,
/// detail and value now. Tap to turn it off or on; press and hold for only it.
/// </summary>
public sealed partial class GraphSeriesItem : ObservableObject
{
    private readonly DeviceGraphsViewModel _owner;

    public GraphSeriesItem(GraphLegendEntry entry, string colour, DeviceGraphsViewModel owner)
    {
        _owner = owner;
        Name = entry.Name;
        Detail = entry.Detail;
        Value = entry.Value;
        Colour = colour;
    }

    public string Name { get; }

    public string? Detail { get; }

    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    public string? Value { get; }

    /// <summary>"#3B82F6" - the colour it's drawn in.</summary>
    [ObservableProperty]
    private string _colour;

    [ObservableProperty]
    private bool _isShown = true;

    [RelayCommand]
    private void Toggle() => _owner.Toggle(Name);

    [RelayCommand]
    private void Only() => _owner.ShowOnly(Name);
}

/// <summary>
/// A device's own LibreNMS graphs, as desktop's Graphs tab: its device-wide,
/// health and wireless graphs in one list, a time range, and the chosen graph
/// drawn by LibreNMS. Or one port's graphs, as desktop's port graphs panel.
/// </summary>
/// <remarks>
/// <para>In the app's colours, with our own legend (#158), as desktop's Graphs
/// in cards (DashyNMS/desktop#260). The graph is fetched with LibreNMS's
/// legend, whose colour squares say how many series there are; they're named
/// from the API (sensors, processors, traffic, with their values now), from
/// LibreNMS's own graph definitions (Core's <see cref="GraphSeriesNames"/>),
/// or for a lone series the graph's own name - and the legend is cropped off
/// for ours. A graph that can't be named keeps LibreNMS's legend.</para>
/// <para>Turning a series off redraws from the graph already fetched. With
/// one sensor left its own graph is fetched, so the scale fits it. Hidden
/// series are remembered per device and graph in desktop's settings
/// (<see cref="AppSettings.GraphHiddenSeries"/>), keyed as desktop keys them.</para>
/// </remarks>
public sealed partial class DeviceGraphsViewModel : ViewModelBase
{
    /// <summary>Requested size, as desktop: wide enough to fill the card once the legend is cropped.</summary>
    internal const int GraphWidth = 1000;
    internal const int GraphHeight = 520;

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore? _settings;
    private readonly Dictionary<(string Key, GraphTimeRange Range), string> _cache = new();
    private IReadOnlyList<Sensor>? _sensors;
    private bool _loadingList;
    private int? _targetsDeviceId;
    private string? _deviceName;
    private IReadOnlyList<GraphType>? _deviceGraphs;

    public BulkObservableCollection<GraphTarget> Targets { get; } = new();

    [ObservableProperty]
    private GraphTarget? _selectedTarget;

    [ObservableProperty]
    private string? _targetsError;

    public bool HasTargetsError => !string.IsNullOrEmpty(TargetsError);
    partial void OnTargetsErrorChanged(string? value) => OnPropertyChanged(nameof(HasTargetsError));

    partial void OnSelectedTargetChanged(GraphTarget? value)
    {
        if (!_loadingList && value is not null) _ = SelectTargetAsync(value);
    }
    private int _loadVersion;
    private string? _raw;
    private IReadOnlyList<GraphLegendEntry>? _entries;
    private HashSet<string> _hidden = new(StringComparer.Ordinal);

    [ObservableProperty]
    private GraphType? _selectedGraph;

    [ObservableProperty]
    private GraphRangeOption _selectedRange;

    /// <summary>The page to show - see <see cref="GraphHtml"/>.</summary>
    [ObservableProperty]
    private string? _graphPage;

    /// <summary>Height over width of the graph shown, for fitting the web view to it.</summary>
    [ObservableProperty]
    private double _graphAspect = 0.52;

    [ObservableProperty]
    private string _title = "Graphs";

    public DeviceGraphsViewModel(ILibreNmsClient client, ISettingsStore? settings = null)
    {
        _client = client;
        _settings = settings;
        _selectedRange = Ranges[1];
        RangeChips = Ranges.Select(r => new GraphRangeChip(r) { IsSelected = r == _selectedRange }).ToList();
    }

    /// <summary>The ranges as chips, the chosen one on.</summary>
    public IReadOnlyList<GraphRangeChip> RangeChips { get; }

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

    /// <summary>Hour to Year, as chips (#158).</summary>
    public IReadOnlyList<GraphRangeOption> Ranges { get; } =
    [
        new(GraphTimeRange.LastHour, "Hour"),
        new(GraphTimeRange.LastDay, "Day"),
        new(GraphTimeRange.LastWeek, "Week"),
        new(GraphTimeRange.LastMonth, "Month"),
        new(GraphTimeRange.LastYear, "Year"),
    ];

    public bool HasNoGraphs => !IsBusy && !_loadingList && Graphs.Count == 0 && !HasError;

    /// <summary>Our own legend - empty when the graph keeps LibreNMS's.</summary>
    public BulkObservableCollection<GraphSeriesItem> Series { get; } = new();

    public bool HasSeries => Series.Count > 0;

    /// <summary>"4 of 6 shown".</summary>
    public string ShownCount => $"{Series.Count(s => s.IsShown).ToString(CultureInfo.InvariantCulture)} of {Series.Count.ToString(CultureInfo.InvariantCulture)} shown";

    public bool SomeHidden => Series.Any(s => !s.IsShown);

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

        foreach (var chip in RangeChips)
        {
            chip.IsSelected = chip.Option == value;
        }

        if (!_loadingList) _ = LoadGraphAsync();
    }

    /// <param name="graph">
    /// The graph to open on - the one tapped (a dashboard card, the ping
    /// graph), else the first. Opening on the first whatever was tapped was #119.
    /// </param>
    /// <param name="range">The range that graph was shown over, if any.</param>
    public Task LoadAsync(int deviceId, string? deviceName = null, string? graph = null, GraphTimeRangePreset? range = null) =>
        LoadContextAsync(deviceId, null, null, deviceName, graph, range);

    /// <summary>A port deep link starts on that port while retaining the device and other ports in Show.</summary>
    public Task LoadPortAsync(int deviceId, string ifName, string? portName = null, string? deviceName = null, string? graph = null, GraphTimeRangePreset? range = null) =>
        LoadContextAsync(deviceId, ifName, portName, deviceName, graph, range);

    private async Task LoadContextAsync(int deviceId, string? ifName, string? portName, string? deviceName, string? graph, GraphTimeRangePreset? range)
    {
        ++_loadVersion;
        _loadingList = true;
        SelectedTarget = null;
        try
        {
            if (DeviceId != deviceId)
            {
                _cache.Clear();
                _sensors = null;
                _deviceGraphs = null;
                _targetsDeviceId = null;
            }
            DeviceId = deviceId;
            _deviceName = deviceName;
            if (range is { } preset && Ranges.FirstOrDefault(r => r.Range.Preset == preset) is { } shown)
                SelectedRange = shown;

            await RunAsync(async () =>
            {
                if (_targetsDeviceId != deviceId)
                {
                    TargetsError = null;
                    var targets = new List<GraphTarget> { new("Device graphs") };
                    try
                    {
                        var ports = await _client.Ports.ListForDeviceAsync(deviceId);
                        targets.AddRange(ports.Where(p => !string.IsNullOrWhiteSpace(p.IfName))
                            .GroupBy(p => p.IfName, StringComparer.Ordinal).Select(g => g.First())
                            .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                            .Select(p => new GraphTarget(p.DisplayName, p.IfName)));
                        _targetsDeviceId = deviceId;
                    }
                    catch (LibreNmsApiException)
                    {
                        // The device's own graphs and a direct port link still work without a port listing.
                        TargetsError = "The port list could not be loaded.";
                    }
                    Targets.ReplaceAll(targets);
                }

                if (ifName is not null && !Targets.Any(t => t.IfName == ifName))
                    Targets.Add(new GraphTarget(portName ?? ifName, ifName));
                SelectedTarget = Targets.First(t => t.IfName == ifName);
            });
        }
        finally { _loadingList = false; }

        if (SelectedTarget is { } target) await DisplayTargetAsync(target, graph);
    }

    /// <summary>Changes the graph target while retaining the range and per-target hidden series.</summary>
    [RelayCommand]
    public Task SelectTargetAsync(GraphTarget? target) => target is null ? Task.CompletedTask : DisplayTargetAsync(target, null);

    private async Task DisplayTargetAsync(GraphTarget target, string? graph)
    {
        ++_loadVersion;
        _loadingList = true;
        GraphPage = null;
        SelectedGraph = null;
        Graphs.ReplaceAll([]);
        _raw = null;
        _entries = null;
        Series.ReplaceAll([]);
        RaiseSeriesChanged();
        try
        {
            SelectedTarget = target;
            PortIfName = target.IfName;
            var name = target.IfName is null ? "Graphs" : target.Label;
            Title = string.IsNullOrWhiteSpace(_deviceName) ? name : $"{name} · {_deviceName}";
            await RunAsync(async () =>
            {
                if (target.IfName is null && _deviceGraphs is null)
                {
                    var deviceWide = _client.Graphs.ListAsync(DeviceId);
                    var health = _client.Graphs.ListHealthAsync(DeviceId);
                    var wireless = WirelessGraphsAsync(DeviceId);
                    await Task.WhenAll(deviceWide, health, wireless);
                    _deviceGraphs = deviceWide.Result.Concat(health.Result).Concat(wireless.Result)
                        .OrderBy(g => g.Description, StringComparer.OrdinalIgnoreCase).ToList();
                }
                Graphs.ReplaceAll(target.IfName is null ? _deviceGraphs! : PortGraphs);
                SelectedGraph = Graphs.FirstOrDefault(g => g.Name == graph) ?? Graphs.FirstOrDefault();
            });
        }
        finally { _loadingList = false; }

        OnPropertyChanged(nameof(HasNoGraphs));
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

    /// <summary>
    /// Draws the chosen graph - fetched once per graph and range, then
    /// redrawn from that for the theme or a series turned off or on.
    /// </summary>
    [RelayCommand]
    public Task LoadGraphAsync()
    {
        if (SelectedGraph is not { } graph || SelectedRange is not { } range)
        {
            GraphPage = null;
            return Task.CompletedTask;
        }

        var version = ++_loadVersion;
        return RunAsync(async () =>
        {
            GraphPage = null;
            _raw = null;
            Series.ReplaceAll([]);
            var raw = await RawAsync(graph, range.Range);
            var legendColours = GraphSvgStyle.Restyle(raw, Palette).LegendColours;
            var entries = await NameSeriesAsync(graph, legendColours);

            // Only if it's still the one wanted - a quick second tap may have moved on.
            if (version != _loadVersion)
            {
                return;
            }

            _raw = raw;
            _entries = entries;
            var remembered = entries is null ? [] : HiddenSetting(graph).Where(n => entries.Any(e => e.Name == n));
            _hidden = new HashSet<string>(remembered, StringComparer.Ordinal);

            // Never every series hidden: a remembered set that would empty the graph shows them all.
            if (entries is not null && entries.All(e => _hidden.Contains(e.Name)))
            {
                _hidden.Clear();
            }

            await RenderAsync(version);
        });
    }

    [RelayCommand]
    private void ShowAll() => SetHidden([]);

    internal void Toggle(string name)
    {
        var next = new HashSet<string>(_hidden, StringComparer.Ordinal);
        if (!next.Remove(name))
        {
            next.Add(name);
        }

        SetHidden(next);
    }

    internal void ShowOnly(string name) => SetHidden(Series.Select(s => s.Name).Where(n => n != name));

    private GraphPalette Palette => GraphHtml.PaletteFor(DarkTheme);

    private void SetHidden(IEnumerable<string> hidden)
    {
        var next = new HashSet<string>(hidden, StringComparer.Ordinal);

        // Turning the last one off too would leave an empty graph - not something to ask for.
        if (_entries is null || _entries.All(e => next.Contains(e.Name)))
        {
            return;
        }

        _hidden = next;
        if (SelectedGraph is { } graph && _settings is not null)
        {
            var key = HiddenKey(graph);
            if (_hidden.Count == 0)
            {
                _settings.Current.GraphHiddenSeries.Remove(key);
            }
            else
            {
                _settings.Current.GraphHiddenSeries[key] = _hidden.ToList();
            }

            _settings.Save();
        }

        _ = RenderAsync(_loadVersion);
    }

    private async Task RenderAsync(int version)
    {
        if (_raw is not { } raw)
        {
            return;
        }

        if (_entries is not { } entries)
        {
            // Unnamed: LibreNMS's own legend stays, in the app's colours.
            Series.ReplaceAll([]);
            RaiseSeriesChanged();
            Show(GraphSvgStyle.Restyle(raw, Palette).Svg);
            return;
        }

        var hiddenIndexes = entries.Select((e, i) => (e, i)).Where(x => _hidden.Contains(x.e.Name)).Select(x => x.i).ToHashSet();
        var styled = GraphSvgStyle.Restyle(raw, Palette, hiddenIndexes, cropLegend: true);
        SyncSeries(entries, styled.SeriesColours);

        var shown = entries.Where(e => !_hidden.Contains(e.Name)).ToList();
        if (shown is [{ SensorId: { } sensorId } only] && entries.Count > 1 && SelectedGraph is { } graph && SelectedRange is { } range)
        {
            // One sensor left: its own graph, so the scale fits it rather than every sensor.
            try
            {
                var colour = styled.SeriesColours[entries.ToList().IndexOf(only)];
                var single = await SensorRawAsync(graph.Name, sensorId, range.Range);
                if (version != _loadVersion || !_hidden.SetEquals(entries.Where(e => e != only).Select(e => e.Name)))
                {
                    return;
                }

                Show(GraphSvgStyle.Restyle(single, Palette with { Accent = colour }).Svg);
                return;
            }
            catch (LibreNmsApiException)
            {
                // Keep the shared scale.
            }
        }

        Show(styled.Svg);
    }

    private void Show(string svg)
    {
        GraphImages.ThrowIfDenied(svg);
        GraphAspect = GraphHtml.AspectOf(svg) ?? GraphAspect;
        GraphPage = GraphHtml.Page(svg, DarkTheme);
    }

    private void SyncSeries(IReadOnlyList<GraphLegendEntry> entries, IReadOnlyList<string> colours)
    {
        var same = Series.Count == entries.Count && Series.Select(s => s.Name).SequenceEqual(entries.Select(e => e.Name));
        if (!same)
        {
            Series.ReplaceAll(entries.Select((e, i) => new GraphSeriesItem(e, i < colours.Count ? colours[i] : Palette.Accent, this)).ToList());
        }

        for (var i = 0; i < Series.Count; i++)
        {
            Series[i].IsShown = !_hidden.Contains(Series[i].Name);
            if (i < colours.Count)
            {
                Series[i].Colour = colours[i];
            }
        }

        RaiseSeriesChanged();
    }

    private void RaiseSeriesChanged()
    {
        OnPropertyChanged(nameof(HasSeries));
        OnPropertyChanged(nameof(ShownCount));
        OnPropertyChanged(nameof(SomeHidden));
    }

    /// <summary>The graph with LibreNMS's legend - fetched once per graph and range.</summary>
    private async Task<string> RawAsync(GraphType graph, GraphTimeRange range)
    {
        var key = ($"{DeviceId}:{GraphKey(graph)}", range);
        if (!_cache.TryGetValue(key, out var raw))
        {
            raw = PortIfName is { } ifName
                ? await _client.Graphs.GetPortSvgAsync(DeviceId, ifName, graph.Name, range, GraphWidth, GraphHeight, legend: true)
                : await _client.Graphs.GetSvgAsync(DeviceId, graph.Name, range, GraphWidth, GraphHeight, legend: true);
            GraphImages.ThrowIfDenied(raw);
            _cache[key] = raw;
        }

        return raw;
    }

    private async Task<string> SensorRawAsync(string graphName, int sensorId, GraphTimeRange range)
    {
        var key = ($"{DeviceId}:" + graphName + "/" + sensorId.ToString(CultureInfo.InvariantCulture), range);
        if (!_cache.TryGetValue(key, out var raw))
        {
            raw = await _client.Graphs.GetSensorSvgAsync(DeviceId, graphName, sensorId, range, GraphWidth, GraphHeight, legend: false);
            GraphImages.ThrowIfDenied(raw);
            _cache[key] = raw;
        }

        return raw;
    }

    /// <summary>
    /// Names for the legend's entries, as desktop names them - or null to keep
    /// LibreNMS's legend: the API's own list when its count matches, a port's
    /// from Core's port graphs with its rates, Core's graph definitions, or
    /// the graph's own name for a lone series.
    /// </summary>
    private async Task<IReadOnlyList<GraphLegendEntry>?> NameSeriesAsync(GraphType graph, IReadOnlyList<string> legendColours)
    {
        if (legendColours.Count == 0)
        {
            return null;
        }

        if (PortIfName is { } ifName)
        {
            if (GraphSeriesNames.Resolve(graph.Name, legendColours) is { } portNames)
            {
                return await PortAsync(ifName) is { } port
                    ? GraphLegend.ForPort(graph.Name, portNames, port)
                    : portNames.Select(n => new GraphLegendEntry(n, null, null)).ToList();
            }

            return legendColours.Count == 1 ? [new GraphLegendEntry(graph.Description ?? graph.Name, null, null)] : null;
        }

        if (await LegendEntriesAsync(graph.Name) is { } fromApi)
        {
            // Missing readings can leave fewer drawn series than API entries.
            // Keep LibreNMS’s legend when we cannot map colours reliably.
            return fromApi.Count == legendColours.Count ? fromApi : null;
        }

        if (GraphSeriesNames.Resolve(graph.Name, legendColours) is { } names)
        {
            return names.Select(n => new GraphLegendEntry(n, null, null)).ToList();
        }

        return legendColours.Count == 1 ? [new GraphLegendEntry(graph.Description ?? graph.Name, null, null)] : null;
    }

    private async Task<IReadOnlyList<GraphLegendEntry>?> LegendEntriesAsync(string graphName)
    {
        try
        {
            if (graphName == GraphLegend.TrafficGraph)
            {
                return GraphLegend.ForTraffic(await _client.Ports.ListForDeviceAsync(DeviceId));
            }

            if (graphName == GraphLegend.ProcessorGraph)
            {
                return GraphLegend.ForProcessors(await _client.Health.ListProcessorsAsync(DeviceId));
            }

            if (GraphLegend.SensorClassOf(graphName) is { } sensorClass)
            {
                _sensors ??= (await _client.Sensors.ListAsync()).Where(s => s.DeviceId == DeviceId).ToList();
                var entries = GraphLegend.ForSensors(_sensors, sensorClass);
                return entries.Count > 0 ? entries : null;
            }
        }
        catch (LibreNmsApiException)
        {
            // Named some other way, or LibreNMS's legend stays.
        }

        return null;
    }

    /// <summary>The port as of now, for its rates - none when it can't be read.</summary>
    private async Task<Port?> PortAsync(string ifName)
    {
        try
        {
            return (await _client.Ports.ListForDeviceAsync(DeviceId)).FirstOrDefault(p => p.IfName == ifName);
        }
        catch (LibreNmsApiException)
        {
            return null;
        }
    }

    /// <summary>A port's graph has the port in it: "port_bits:Gi0/1".</summary>
    private string GraphKey(GraphType graph) => PortIfName is { } ifName ? $"{graph.Name}:{ifName}" : graph.Name;

    /// <summary>"7:device_temperature" or "7:port_bits:Gi0/1" - desktop's keys, so the choice is shared.</summary>
    private string HiddenKey(GraphType graph) => string.Create(CultureInfo.InvariantCulture, $"{DeviceId}:{GraphKey(graph)}");

    private IReadOnlyList<string> HiddenSetting(GraphType graph) =>
        _settings?.Current.GraphHiddenSeries.TryGetValue(HiddenKey(graph), out var hidden) == true ? hidden : [];

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
