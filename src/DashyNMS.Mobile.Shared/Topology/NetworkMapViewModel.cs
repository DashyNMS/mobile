using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Topology;
using DesktopNMS.Services;

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

    /// <summary>
    /// Something a switch sees that LibreNMS doesn't monitor (a phone, an
    /// access point) rather than a device - drawn small and square, as on
    /// desktop, with a negative id of the map's own (Core's Neighbours.NodeId).
    /// </summary>
    public bool IsNeighbour { get; init; }

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
/// Filtered by location, by device group (desktop's own scope), or both -
/// then a device has to be in both. Only links between two devices both in
/// scope become lines, as desktop's scope works. Search finds a device by name, selects it and asks the page to
/// centre on it. Devices with no links at all are left out unless asked
/// for, as on desktop - on a whole fleet they'd far outnumber the rest.
/// </remarks>
public sealed partial class NetworkMapViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly IMapLayoutStore? _layouts;
    private readonly ISessionService? _session;

    private IReadOnlyList<Device> _devices = [];
    private IReadOnlyList<NetworkLink> _links = [];
    private IReadOnlyDictionary<int, string>? _portNames;

    /// <summary>Every port's state, for the neighbours' colours - only fetched once they're asked for.</summary>
    private IReadOnlyDictionary<int, Port>? _portStates;
    private IReadOnlyDictionary<int, IReadOnlyList<string>> _groups = new Dictionary<int, IReadOnlyList<string>>();
    private bool _loaded;

    /// <summary>Guards against an older layout (a location since switched away from) landing after a newer one.</summary>
    private int _buildVersion;

    /// <summary>
    /// Set while a refresh puts back the chosen location or group, with its
    /// new count: the choice hasn't changed, so the map isn't rebuilt.
    /// </summary>
    private bool _keepingChoice;

    [ObservableProperty]
    private FacetOption _selectedLocation = AllLocations;

    /// <summary>Desktop's own scope for this map - a device group; with a location too, devices must be in both.</summary>
    [ObservableProperty]
    private FacetOption _selectedGroup = AllGroups;

    [ObservableProperty]
    private bool _showUnlinkedDevices;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private NetworkNode? _selectedNode;

    [ObservableProperty]
    private bool _isLayingOut;

    /// <summary>
    /// Also draw what switches in view see that LibreNMS doesn't monitor -
    /// the neighbours of desktop's Neighbours views set to show on the map,
    /// or, with none (views are made on desktop), every unmonitored one.
    /// Off by default: on a whole fleet they'd swamp the devices.
    /// </summary>
    [ObservableProperty]
    private bool _showNeighbours;

    public NetworkMapViewModel(
        ILibreNmsClient client,
        ISettingsStore settings,
        INavigationService navigation,
        IMapLayoutStore? layouts = null,
        ISessionService? session = null)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
        _layouts = layouts;
        _session = session;
    }

    /// <summary>
    /// Which map this is - per server, location and group - for remembering
    /// where devices were dragged to and where the view was (device ids
    /// mean nothing on another LibreNMS).
    /// </summary>
    public string ScopeKey =>
        $"{_session?.Connection?.WebRoot.Host ?? "server"}|location:{SelectedLocation.Key}|group:{SelectedGroup.Key}";

    /// <summary>A LibreNMS device is selected (not a neighbour), so Open can go to its Device View.</summary>
    public bool HasSelectedDevice => SelectedNode is { IsNeighbour: false };

    private static FacetOption AllLocations { get; } = new(null, "All locations");

    private static FacetOption AllGroups { get; } = new(null, "All groups");

    /// <summary>The map changed shape (a new location, or loaded): the page fits it to the screen.</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>Something drawn changed in place (the selection): the page redraws.</summary>
    public event EventHandler? RedrawRequested;

    /// <summary>A search hit or a tapped connection: the page brings this node to the middle.</summary>
    public event EventHandler<NetworkNode>? CenterOnRequested;

    public IReadOnlyList<NetworkNode> Nodes { get; private set; } = [];

    public IReadOnlyList<NetworkEdge> Edges { get; private set; } = [];

    public BulkObservableCollection<FacetOption> LocationOptions { get; } = [AllLocations];

    public BulkObservableCollection<FacetOption> GroupOptions { get; } = [AllGroups];

    public BulkObservableCollection<NetworkConnection> SelectedConnections { get; } = new();

    public bool HasSelectedNode => SelectedNode is not null;

    public string SelectedNodeStateText => SelectedNode is { IsNeighbour: true } neighbour
        ? neighbour.State switch
        {
            DeviceState.Up => "Not monitored · its switch port is up",
            DeviceState.Down => "Not monitored · its switch port is down",
            _ => "Not monitored by LibreNMS",
        }
        : SelectedNode?.State switch
    {
        null => string.Empty,
        DeviceState.Up => "Up",
        DeviceState.Down => "Down",
        DeviceState.Maintenance => "In maintenance",
        DeviceState.Disabled => "Disabled",
        DeviceState.Ignored => "Ignored",
        _ => string.Empty,
    };

    /// <summary>"42 devices · 51 connections", and "· 12 neighbours" with them shown.</summary>
    public string SummaryText
    {
        get
        {
            if (Nodes.Count == 0)
            {
                return string.Empty;
            }

            var neighbours = Nodes.Count(n => n.IsNeighbour);
            var devices = Nodes.Count - neighbours;
            var text = $"{devices} {(devices == 1 ? "device" : "devices")} · {Edges.Count} {(Edges.Count == 1 ? "connection" : "connections")}";
            return neighbours > 0 ? text + $" · {neighbours} {(neighbours == 1 ? "neighbour" : "neighbours")}" : text;
        }
    }

    /// <summary>Loaded, nothing to draw - a location whose devices have no links between them.</summary>
    public bool IsEmpty => _loaded && !IsBusy && !IsLayingOut && Nodes.Count == 0;

    public string EmptyText =>
        SelectedLocation.Key is null && SelectedGroup.Key is null ? "LibreNMS has no CDP or LLDP links between monitored devices."
        : SelectedGroup.Key is null ? "No links between devices at this location."
        : SelectedLocation.Key is null ? "No links between devices in this group."
        : "No links between devices in this group at this location.";

    partial void OnSelectedLocationChanged(FacetOption value)
    {
        if (_keepingChoice)
        {
            return;
        }

        // A picker whose choices are being refilled briefly sends back null.
        if (value is null)
        {
            SelectedLocation = AllLocations;
            return;
        }

        SelectedNode = null;
        _ = RebuildAsync();
    }

    partial void OnSelectedGroupChanged(FacetOption value)
    {
        if (_keepingChoice)
        {
            return;
        }

        if (value is null)
        {
            SelectedGroup = AllGroups;
            return;
        }

        SelectedNode = null;
        _ = RebuildAsync();
    }

    partial void OnShowUnlinkedDevicesChanged(bool value) => _ = RebuildAsync();

    partial void OnShowNeighboursChanged(bool value) => _ = value && _portStates is null ? LoadPortStatesAsync() : RebuildAsync();

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(FindSearchMatch);

    partial void OnSelectedNodeChanged(NetworkNode? value)
    {
        OnPropertyChanged(nameof(HasSelectedNode));
        OnPropertyChanged(nameof(HasSelectedDevice));
        OnPropertyChanged(nameof(SelectedNodeStateText));
        RebuildSelectedConnections();
        RedrawRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsLayingOutChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    [RelayCommand]
    private void ToggleUnlinkedDevices() => ShowUnlinkedDevices = !ShowUnlinkedDevices;

    [RelayCommand]
    private void ToggleNeighbours() => ShowNeighbours = !ShowNeighbours;

    /// <summary>A node was dragged and dropped (the page moved it): its place is remembered, as desktop's is.</summary>
    public void NodeMoved(NetworkNode node) => SaveLayout();

    /// <summary>Forgets where this map's devices were put and lays it out afresh.</summary>
    [RelayCommand]
    private Task ResetLayoutAsync()
    {
        _layouts?.Clear(ScopeKey);
        return RebuildAsync();
    }

    /// <summary>The switch ports' states, for the neighbours' colours; not worth failing the map over.</summary>
    private async Task LoadPortStatesAsync()
    {
        try
        {
            var ports = await _client.Ports.ListAllStatusAsync();
            _portStates = ports.GroupBy(p => p.PortId).ToDictionary(g => g.Key, g => g.First());
        }
        catch (LibreNmsApiException)
        {
            _portStates = new Dictionary<int, Port>();
        }

        await RebuildAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var devicesTask = _client.Devices.ListAsync();
        var linksTask = _client.Links.ListAllAsync();
        var portsTask = PortNamesAsync();
        var groupsTask = GroupsAsync();
        await Task.WhenAll(devicesTask, linksTask, portsTask, groupsTask);

        _devices = devicesTask.Result;
        _links = linksTask.Result;
        _portNames = portsTask.Result;
        _groups = groupsTask.Result;
        _loaded = true;

        RebuildLocations();
        RebuildGroups();
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
    private Task OpenSelectedDeviceAsync() => SelectedNode is not { IsNeighbour: false } node
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
        var group = SelectedGroup.Key;
        var scope = _devices
            .Where(d => location is null || string.Equals(d.LocationName(), location, StringComparison.OrdinalIgnoreCase))
            .Where(d => group is null
                || (_groups.TryGetValue(d.DeviceId, out var names) && names.Contains(group, StringComparer.OrdinalIgnoreCase)))
            .Select(d => d.DeviceId)
            .ToList();

        var graph = NetworkTopology.Build(scope, _links, _portNames);
        var byId = _devices.ToDictionary(d => d.DeviceId);

        // Neighbours as desktop draws them: one node each, joined to the
        // switches in view they're plugged into - which makes those switches
        // linked, even with nothing else connected to them.
        var neighbours = ShowNeighbours ? MapNeighbours(scope.ToHashSet(), byId) : [];
        var neighbourEdges = Neighbours.MapEdges(neighbours, _portNames);
        var neighbourNodes = neighbours.GroupBy(Neighbours.NodeId).ToDictionary(g => g.Key, g => g.ToList());

        var allEdges = graph.Edges.Concat(neighbourEdges).ToList();
        var linkedIds = graph.DeviceIds.Except(graph.UnlinkedDeviceIds)
            .Concat(neighbourEdges.Select(e => e.DeviceB))
            .Distinct()
            .Concat(neighbourNodes.Keys)
            .ToList();
        var linkedSet = linkedIds.ToHashSet();
        var unlinkedIds = ShowUnlinkedDevices ? graph.UnlinkedDeviceIds.Where(id => !linkedSet.Contains(id)).ToList() : [];
        var pairs = allEdges.Select(e => (e.DeviceA, e.DeviceB)).ToList();

        // Where devices were dragged to (or last laid out) stays put; only
        // what's new is placed, beside what it's cabled to.
        var key = ScopeKey;
        var saved = _layouts?.Get(key) ?? new Dictionary<int, MapPoint>();

        IsLayingOut = true;
        var positions = await Task.Run(() =>
        {
            var pinned = linkedIds.Where(saved.ContainsKey).ToDictionary(id => id, id => saved[id]);
            var laidOut = ForceDirectedLayout.Compute(linkedIds, pairs, pinned);
            foreach (var (id, point) in ForceDirectedLayout.Grid(unlinkedIds.Where(id => !saved.ContainsKey(id)).ToList(), laidOut.Values.ToList()))
            {
                laidOut[id] = point;
            }

            foreach (var id in unlinkedIds.Where(saved.ContainsKey))
            {
                laidOut[id] = saved[id];
            }

            return laidOut;
        });

        if (version != _buildVersion)
        {
            return;
        }

        var style = _settings.Current.DeviceNameStyle;
        var nodes = new Dictionary<int, NetworkNode>();
        foreach (var id in linkedIds.Concat(unlinkedIds))
        {
            var point = positions[id];
            if (neighbourNodes.TryGetValue(id, out var seen))
            {
                var first = seen[0];
                nodes[id] = new NetworkNode(id)
                {
                    X = point.X,
                    Y = point.Y,
                    IsNeighbour = true,
                    Name = first.IsUnnamed ? first.Mac ?? first.RemotePort ?? first.Name : first.Name,
                    State = NeighbourState(seen, byId),
                    Detail = string.Join(" · ", new[] { first.Description, first.Mac }.Where(s => !string.IsNullOrWhiteSpace(s))),
                };
                continue;
            }

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
        Edges = allEdges.Select(e => new NetworkEdge(nodes[e.DeviceA], nodes[e.DeviceB], e)).ToList();
        IsLayingOut = false;
        SaveLayout();

        OnPropertyChanged(nameof(Nodes));
        OnPropertyChanged(nameof(Edges));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
        SelectedNode = selectedId is { } sid && nodes.TryGetValue(sid, out var kept) ? kept : null;
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The neighbours to draw: unmonitored ones on a switch in view - those
    /// of desktop's Neighbours views set to show on the map, or all of them
    /// with none - keeping, for one seen on several ports, only its live
    /// links, as desktop does.
    /// </summary>
    private List<Neighbour> MapNeighbours(IReadOnlySet<int> scope, IReadOnlyDictionary<int, Device> byId)
    {
        var views = _settings.Current.NeighbourViews.Where(v => v.ShowOnMap).ToList();
        var candidates = Neighbours.FromLinks(_links)
            .Where(n => !n.IsMonitored && scope.Contains(n.SwitchDeviceId))
            .Where(n => views.Count == 0 || views.Any(v => Neighbours.Matches(
                v,
                n,
                byId.TryGetValue(n.SwitchDeviceId, out var sw) ? sw.BestName : null,
                PortOf(n)?.IfAlias)))
            .ToList();

        bool SwitchUp(Neighbour n) => byId.TryGetValue(n.SwitchDeviceId, out var sw) && sw.State == DeviceState.Up;
        return Neighbours.PreferLiveLinks(candidates, n => n, n => SwitchUp(n) && PortOf(n) is { IsUp: true }, SwitchUp).ToList();
    }

    private Port? PortOf(Neighbour neighbour) => _portStates?.GetValueOrDefault(neighbour.SwitchPortId);

    /// <summary>A neighbour follows its switch port: up if any is, down if one is (or its switch is down), otherwise unknown - as desktop reads it.</summary>
    private DeviceState NeighbourState(IReadOnlyList<Neighbour> seen, IReadOnlyDictionary<int, Device> byId)
    {
        var states = seen.Select(n =>
            byId.TryGetValue(n.SwitchDeviceId, out var sw) && sw.State == DeviceState.Down ? DeviceState.Down
            : PortOf(n) is not { } port ? DeviceState.Disabled
            : port.IsUp ? DeviceState.Up
            : string.Equals(port.IfAdminStatus, "down", StringComparison.OrdinalIgnoreCase) ? DeviceState.Disabled
            : DeviceState.Down).ToList();

        return states.Contains(DeviceState.Up) ? DeviceState.Up
            : states.Contains(DeviceState.Down) ? DeviceState.Down
            : DeviceState.Disabled;
    }

    /// <summary>
    /// Remembers every node's place for this map, merged over what's saved
    /// so devices not drawn just now (unlinked ones, with that off) keep theirs.
    /// </summary>
    private void SaveLayout()
    {
        if (_layouts is null || Nodes.Count == 0)
        {
            return;
        }

        var merged = new Dictionary<int, MapPoint>(_layouts.Get(ScopeKey));
        foreach (var node in Nodes)
        {
            merged[node.DeviceId] = new MapPoint(node.X, node.Y);
        }

        _layouts.Save(ScopeKey, merged);
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
            KeepChoice(() => SelectedLocation = keep);
        }
    }

    private void KeepChoice(Action set)
    {
        _keepingChoice = true;
        try
        {
            set();
        }
        finally
        {
            _keepingChoice = false;
        }
    }

    /// <summary>Every device group with a member, and how many - the Group filter's choices.</summary>
    private void RebuildGroups()
    {
        var counted = _groups.Values
            .SelectMany(names => names)
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new FacetOption(g.Key, $"{g.Key} ({g.Count()})"))
            .ToList();

        var selected = SelectedGroup.Key;
        GroupOptions.ReplaceAll(counted.Prepend(AllGroups));

        var keep = counted.FirstOrDefault(o => string.Equals(o.Key, selected, StringComparison.OrdinalIgnoreCase)) ?? AllGroups;
        if (!ReferenceEquals(keep, SelectedGroup))
        {
            KeepChoice(() => SelectedGroup = keep);
        }
    }

    /// <summary>Which groups each device is in. Best effort: without them the map still draws, just without the Group filter's choices.</summary>
    private async Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GroupsAsync()
    {
        try
        {
            return await _client.DeviceGroups.GetMembershipByDeviceAsync();
        }
        catch (LibreNmsApiException)
        {
            return new Dictionary<int, IReadOnlyList<string>>();
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
