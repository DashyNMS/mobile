using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Topology;

namespace DashyNMS.Mobile.Topology;

/// <summary>
/// One device on the network map. Plain data, not observable: the page
/// draws the whole map itself and redraws when told to, as desktop's canvas does.
/// </summary>
public sealed class NetworkNode
{
    public NetworkNode(int deviceId)
    {
        DeviceId = deviceId;
    }

    public int DeviceId { get; }

    public string Name { get; set; } = string.Empty;

    public DeviceState State { get; set; } = DeviceState.Down;

    /// <summary>"192.0.2.33 · Aruba JL320A" - for the selected device's panel.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Position in map units, from the layout.</summary>
    public double X { get; set; }

    public double Y { get; set; }
}

/// <summary>One line between two devices, however many cables it stands for.</summary>
public sealed class NetworkEdge
{
    public NetworkEdge(NetworkNode a, NetworkNode b, TopologyEdge source)
    {
        A = a;
        B = b;
        Source = source;
    }

    public NetworkNode A { get; }

    public NetworkNode B { get; }

    public TopologyEdge Source { get; }

    public int LinkCount => Source.LinkCount;

    /// <summary>Either end is down - drawn dotted, as on desktop.</summary>
    public bool IsToOfflineDevice => A.State == DeviceState.Down || B.State == DeviceState.Down;

    public bool Touches(NetworkNode node) => ReferenceEquals(A, node) || ReferenceEquals(B, node);

    public NetworkNode Other(NetworkNode node) => ReferenceEquals(A, node) ? B : A;
}

/// <summary>One line in the selected device's connections: "Gi1/0/48 → core-sw (Te1/1/1)".</summary>
public sealed record NetworkConnection(NetworkNode Neighbour, string LocalPort, string RemotePort)
{
    public string NeighbourName => Neighbour.Name;

    public string Ports => $"{LocalPort} → {RemotePort}";
}

/// <summary>
/// Desktop's Network map (its issue #56) on a phone: LibreNMS's fleet-wide
/// LLDP/CDP links table drawn as a diagram, one node per monitored device,
/// one line per connected pair. Built and laid out by Core's own
/// <see cref="NetworkTopology"/> and <see cref="ForceDirectedLayout"/>, so it
/// matches desktop's.
/// </summary>
/// <remarks>
/// Filtered by location rather than desktop's device groups - the question
/// on a phone is usually "what's at this site". Only links between two
/// devices both in the chosen location become lines, as desktop's scope
/// works. Search finds a device by name, selects it and asks the page to
/// centre on it. Devices with no links at all are left out unless asked
/// for, as on desktop - on a whole fleet they'd far outnumber the rest.
/// </remarks>
public sealed partial class NetworkMapViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;

    private IReadOnlyList<Device> _devices = [];
    private IReadOnlyList<NetworkLink> _links = [];
    private IReadOnlyDictionary<int, string>? _portNames;
    private bool _loaded;

    /// <summary>Guards against an older layout (a location since switched away from) landing after a newer one.</summary>
    private int _buildVersion;

    [ObservableProperty]
    private FacetOption _selectedLocation = AllLocations;

    [ObservableProperty]
    private bool _showUnlinkedDevices;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private NetworkNode? _selectedNode;

    [ObservableProperty]
    private bool _isLayingOut;

    public NetworkMapViewModel(ILibreNmsClient client, ISettingsStore settings, INavigationService navigation)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
    }

    private static FacetOption AllLocations { get; } = new(null, "All locations");

    /// <summary>The map changed shape (a new location, or loaded): the page fits it to the screen.</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>Something drawn changed in place (the selection): the page redraws.</summary>
    public event EventHandler? RedrawRequested;

    /// <summary>A search hit or a tapped connection: the page brings this node to the middle.</summary>
    public event EventHandler<NetworkNode>? CenterOnRequested;

    public IReadOnlyList<NetworkNode> Nodes { get; private set; } = [];

    public IReadOnlyList<NetworkEdge> Edges { get; private set; } = [];

    public BulkObservableCollection<FacetOption> LocationOptions { get; } = [AllLocations];

    public BulkObservableCollection<NetworkConnection> SelectedConnections { get; } = new();

    public bool HasSelectedNode => SelectedNode is not null;

    public string SelectedNodeStateText => SelectedNode?.State switch
    {
        null => string.Empty,
        DeviceState.Up => "Up",
        DeviceState.Down => "Down",
        DeviceState.Maintenance => "In maintenance",
        DeviceState.Disabled => "Disabled",
        DeviceState.Ignored => "Ignored",
        _ => string.Empty,
    };

    /// <summary>"42 devices · 51 connections"</summary>
    public string SummaryText => Nodes.Count == 0
        ? string.Empty
        : $"{Nodes.Count} {(Nodes.Count == 1 ? "device" : "devices")} · {Edges.Count} {(Edges.Count == 1 ? "connection" : "connections")}";

    /// <summary>Loaded, nothing to draw - a location whose devices have no links between them.</summary>
    public bool IsEmpty => _loaded && !IsBusy && !IsLayingOut && Nodes.Count == 0;

    public string EmptyText => SelectedLocation.Key is null
        ? "LibreNMS has no CDP or LLDP links between monitored devices."
        : "No links between devices at this location.";

    partial void OnSelectedLocationChanged(FacetOption value)
    {
        // A picker whose choices are being refilled briefly sends back null.
        if (value is null)
        {
            SelectedLocation = AllLocations;
            return;
        }

        SelectedNode = null;
        _ = RebuildAsync();
    }

    partial void OnShowUnlinkedDevicesChanged(bool value) => _ = RebuildAsync();

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(FindSearchMatch);

    partial void OnSelectedNodeChanged(NetworkNode? value)
    {
        OnPropertyChanged(nameof(HasSelectedNode));
        OnPropertyChanged(nameof(SelectedNodeStateText));
        RebuildSelectedConnections();
        RedrawRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsLayingOutChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    [RelayCommand]
    private void ToggleUnlinkedDevices() => ShowUnlinkedDevices = !ShowUnlinkedDevices;

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var devicesTask = _client.Devices.ListAsync();
        var linksTask = _client.Links.ListAllAsync();
        var portsTask = PortNamesAsync();
        await Task.WhenAll(devicesTask, linksTask, portsTask);

        _devices = devicesTask.Result;
        _links = linksTask.Result;
        _portNames = portsTask.Result;
        _loaded = true;

        RebuildLocations();
        await RebuildAsync();
    });

    /// <summary>A tap on a node, or on empty space (null) to clear it.</summary>
    public void Select(NetworkNode? node) => SelectedNode = node;

    [RelayCommand]
    private void ClearSelection() => SelectedNode = null;

    /// <summary>A connection in the panel: that neighbour becomes the selection, in the middle.</summary>
    [RelayCommand]
    private void SelectNeighbour(NetworkConnection? connection)
    {
        if (connection is null)
        {
            return;
        }

        SelectedNode = connection.Neighbour;
        CenterOnRequested?.Invoke(this, connection.Neighbour);
    }

    [RelayCommand]
    private Task OpenSelectedDeviceAsync() => SelectedNode is not { } node
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = node.DeviceId });

    /// <summary>
    /// Builds the graph for the chosen location and lays it out, off the UI
    /// thread (the layout is O(n²) a step); a newer rebuild started
    /// meanwhile wins.
    /// </summary>
    internal async Task RebuildAsync()
    {
        if (!_loaded)
        {
            return;
        }

        var version = ++_buildVersion;
        var location = SelectedLocation.Key;
        var scope = _devices
            .Where(d => location is null || string.Equals(d.LocationName(), location, StringComparison.OrdinalIgnoreCase))
            .Select(d => d.DeviceId)
            .ToList();

        var graph = NetworkTopology.Build(scope, _links, _portNames);
        var linkedIds = graph.DeviceIds.Except(graph.UnlinkedDeviceIds).ToList();
        var unlinkedIds = ShowUnlinkedDevices ? graph.UnlinkedDeviceIds.ToList() : [];
        var pairs = graph.Edges.Select(e => (e.DeviceA, e.DeviceB)).ToList();

        IsLayingOut = true;
        var positions = await Task.Run(() =>
        {
            var laidOut = ForceDirectedLayout.Compute(linkedIds, pairs, new Dictionary<int, MapPoint>());
            foreach (var (id, point) in ForceDirectedLayout.Grid(unlinkedIds, laidOut.Values.ToList()))
            {
                laidOut[id] = point;
            }

            return laidOut;
        });

        if (version != _buildVersion)
        {
            return;
        }

        var byId = _devices.ToDictionary(d => d.DeviceId);
        var style = _settings.Current.DeviceNameStyle;
        var nodes = new Dictionary<int, NetworkNode>();
        foreach (var id in linkedIds.Concat(unlinkedIds))
        {
            var point = positions[id];
            var node = new NetworkNode(id) { X = point.X, Y = point.Y };
            if (byId.TryGetValue(id, out var device))
            {
                node.Name = new DeviceItem(device, style).Name;
                node.State = device.State;
                node.Detail = string.Join(" · ", new[] { device.Ip, device.Hardware }.Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            nodes[id] = node;
        }

        var selectedId = SelectedNode?.DeviceId;
        Nodes = nodes.Values.ToList();
        Edges = graph.Edges.Select(e => new NetworkEdge(nodes[e.DeviceA], nodes[e.DeviceB], e)).ToList();
        IsLayingOut = false;

        OnPropertyChanged(nameof(Nodes));
        OnPropertyChanged(nameof(Edges));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
        SelectedNode = selectedId is { } sid && nodes.TryGetValue(sid, out var kept) ? kept : null;
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Every location with a device, and how many - the filter's choices.</summary>
    private void RebuildLocations()
    {
        var counted = _devices
            .Select(d => d.LocationName())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(name => name!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new FacetOption(g.Key, $"{g.Key} ({g.Count()})"))
            .ToList();

        var selected = SelectedLocation.Key;
        LocationOptions.ReplaceAll(counted.Prepend(AllLocations));

        // Keep the chosen location through a refresh, with its new count.
        var keep = counted.FirstOrDefault(o => string.Equals(o.Key, selected, StringComparison.OrdinalIgnoreCase)) ?? AllLocations;
        if (!ReferenceEquals(keep, SelectedLocation))
        {
            SetProperty(ref _selectedLocation, keep, nameof(SelectedLocation));
        }
    }

    private void RebuildSelectedConnections()
    {
        if (SelectedNode is not { } node)
        {
            SelectedConnections.ReplaceAll([]);
            return;
        }

        SelectedConnections.ReplaceAll(Edges
            .Where(e => e.Touches(node))
            .OrderBy(e => e.Other(node).Name, StringComparer.OrdinalIgnoreCase)
            .SelectMany(e =>
            {
                var isA = ReferenceEquals(e.A, node);
                return e.Source.Connections.Select(c => new NetworkConnection(
                    e.Other(node),
                    (isA ? c.PortA : c.PortB) ?? "?",
                    (isA ? c.PortB : c.PortA) ?? "?"));
            }));
    }

    /// <summary>The device whose name best matches - starting with the term first, then the shortest - selected and centred.</summary>
    private void FindSearchMatch()
    {
        var term = SearchText.Trim();
        if (term.Length == 0)
        {
            return;
        }

        var match = Nodes
            .Where(n => n.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(n => n.Name.Length)
            .FirstOrDefault();

        if (match is not null)
        {
            SelectedNode = match;
            CenterOnRequested?.Invoke(this, match);
        }
    }

    /// <summary>
    /// Every port's name, so each end of a link is named from its own port
    /// (see <see cref="NetworkTopology.Build"/>). Not worth failing the map
    /// over: without them a link reported from one side shows "?" for its own port.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, string>?> PortNamesAsync()
    {
        try
        {
            var ports = await _client.Ports.ListAllNamesAsync();
            var names = new Dictionary<int, string>(ports.Count);
            foreach (var port in ports)
            {
                if (PortLabels.ForPort(port) is { } name)
                {
                    names[port.PortId] = name;
                }
            }

            return names;
        }
        catch (LibreNmsApiException)
        {
            return null;
        }
    }
}
