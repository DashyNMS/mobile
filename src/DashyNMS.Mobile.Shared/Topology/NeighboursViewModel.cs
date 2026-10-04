using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Topology;

namespace DashyNMS.Mobile.Topology;

/// <summary>
/// One link between two ends: the row, the devices a tap can open, and the
/// neighbour as desktop's groups see it - from the switch's side, with the
/// switch and its port's description (#98).
/// </summary>
public sealed record NeighbourLink(
    SectionRow Row,
    int LocalDeviceId,
    string LocalName,
    int? RemoteDeviceId,
    string RemoteName,
    bool IsProblem,
    Neighbour Neighbour,
    string SwitchName,
    string? SwitchPortDescription)
{
    /// <summary>The switch's end: the device that reported the link, and its port.</summary>
    public NeighbourEnd Local { get; init; } = new(LocalName, LocalDeviceId, "?", null, null);

    /// <summary>The neighbour's end - a LibreNMS device, or something LibreNMS doesn't poll.</summary>
    public NeighbourEnd Remote { get; init; } = new(RemoteName, RemoteDeviceId, "?", null, null);

    /// <summary>What the neighbour says it is: "Cisco IP Phone 8845".</summary>
    public string? Platform { get; init; }

    /// <summary>"core-sw-01 Gi1/0/24 ↔ eth0" - the row's second line.</summary>
    public string PortsText => Row.Subtitle ?? string.Empty;

    /// <summary>
    /// The row's last line: why it's down, in the critical colour - or what
    /// the neighbour is, and whether LibreNMS polls it or the link's gone quiet.
    /// </summary>
    public string NoteText => Row.Detail ?? string.Join(" · ", new[]
    {
        Platform,
        RemoteDeviceId is null ? "not in LibreNMS" : null,
        Neighbour.Active ? null : "not active",
    }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public bool HasNote => NoteText.Length > 0;
}

/// <summary>One end of a neighbour link, for the link's page (#121).</summary>
/// <param name="DeviceUp">Null when LibreNMS doesn't poll it.</param>
/// <param name="PortUp">Null when the port isn't known.</param>
public sealed record NeighbourEnd(string Name, int? DeviceId, string Port, bool? DeviceUp, bool? PortUp)
{
    public bool IsDown => DeviceUp == false || PortUp == false;

    /// <summary>"device down", "port down", "up" - or blank, knowing neither.</summary>
    public string StateText => DeviceUp == false ? "device down" : PortUp switch
    {
        false => "port down",
        true => "up",
        null => DeviceUp == true ? "device up" : string.Empty,
    };

    public RowStatus Status => IsDown ? RowStatus.Critical : PortUp == true || DeviceUp == true ? RowStatus.Ok : RowStatus.Inactive;

    public bool CanOpen => DeviceId is not null;

    public string OpenText => $"Open {Name}";
}

/// <summary>A group chip above the list: "All", or one of the user's groups, with how many it holds.</summary>
public sealed partial class NeighbourGroupChip(NeighbourViewDefinition? group, string name) : ObservableObject
{
    /// <summary>Null for "All".</summary>
    public NeighbourViewDefinition? Group { get; } = group;

    public string Name { get; } = name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text))]
    private int _count;

    [ObservableProperty]
    private bool _isSelected;

    public string Text => $"{Name} {Count}";
}

/// <summary>
/// Every CDP/LLDP link LibreNMS knows, across the network, laid out as the
/// Devices tab is (#98): a search over every LLDP field, chips for the
/// user's neighbourhoods and for links that are up or down, a count, and device-style rows.
/// </summary>
/// <remarks>
/// <para>A link both devices report (A sees B, B sees A) is one row. It's a
/// problem when either device or either port is down; problems come first.</para>
/// <para>The groups are desktop's own neighbour views
/// (<see cref="AppSettings.NeighbourViews"/>), matched by Core's
/// <see cref="Neighbours.Matches"/>, so a group lists the same neighbours on
/// both apps; they're made and changed in <see cref="NeighbourGroupsViewModel"/>.</para>
/// </remarks>
public sealed partial class NeighboursViewModel : ViewModelBase, IRefreshable
{
    private readonly NeighbourDirectory _directory;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private IReadOnlyList<NeighbourLink> _all = [];
    private string? _selectedGroupId;
    private bool _loaded;

    /// <summary>Links with both ends up - on, with <see cref="ShowDown"/>, until a chip is tapped.</summary>
    [ObservableProperty]
    private bool _showUp = true;

    /// <summary>Links with a device or port down at either end.</summary>
    [ObservableProperty]
    private bool _showDown = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public NeighboursViewModel(NeighbourDirectory directory, ISettingsStore settings, INavigationService navigation)
    {
        _directory = directory;
        _settings = settings;
        _navigation = navigation;
        RebuildGroups();
    }

    public BulkObservableCollection<NeighbourLink> Links { get; } = new();

    /// <summary>"All", then each group, in the user's order.</summary>
    public ObservableCollection<NeighbourGroupChip> Groups { get; } = new();

    public int UpCount => InGroup().Count(l => !l.IsProblem);

    public int DownCount => InGroup().Count(l => l.IsProblem);

    /// <summary>"24 neighbours", or "3 of 24 neighbours" when filtered.</summary>
    public string CountText
    {
        get
        {
            var total = _all.Count;
            var noun = total == 1 ? "neighbour" : "neighbours";
            return Links.Count == total ? $"{total} {noun}" : $"{Links.Count} of {total} {noun}";
        }
    }

    public bool IsEmpty => _loaded && Links.Count == 0 && !IsBusy;

    public string EmptyText => _all.Count == 0
        ? "LibreNMS has no CDP or LLDP neighbours."
        : "None of the neighbours match the search and chips.";

    /// <summary>"No neighbours" when there are none at all; "No matches" when the filters hide them.</summary>
    public string EmptyTitle => _all.Count == 0 ? "No neighbours" : "No matches";

    /// <summary>The first fetch is done - the count line waits for it.</summary>
    public bool HasLoaded => _loaded;

    /// <summary>Anything Clear filters would undo.</summary>
    public bool HasActiveFilters => !ShowUp || !ShowDown || _selectedGroupId is not null || !string.IsNullOrWhiteSpace(SearchText);

    partial void OnShowUpChanged(bool value) => ApplyFilter();

    partial void OnShowDownChanged(bool value) => ApplyFilter();

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(ApplyFilter);

    [RelayCommand]
    private void ToggleUp() => ShowUp = !ShowUp;

    /// <summary>Both state chips on, every neighbourhood, no search.</summary>
    [RelayCommand]
    private void ClearFilters()
    {
        _selectedGroupId = null;
        MarkSelectedGroup();
        ShowUp = true;
        ShowDown = true;
        SearchText = string.Empty;
        ApplyFilter();
    }

    [RelayCommand]
    private void ToggleDown() => ShowDown = !ShowDown;

    /// <summary>A group chip: show just that group - or, tapped again, everything.</summary>
    [RelayCommand]
    private void SelectGroup(NeighbourGroupChip? chip)
    {
        var id = chip?.Group?.Id;
        _selectedGroupId = id == _selectedGroupId ? null : id;
        MarkSelectedGroup();
        ApplyFilter();
    }

    [RelayCommand]
    private Task EditGroupsAsync() => _navigation.GoToAsync(Routes.NeighbourGroups);

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        _all = await _directory.LoadAsync();
        _loaded = true;
        ApplyFilter();
    });

    /// <summary>Back from the groups editor: they may have been added, renamed, reordered or deleted.</summary>
    public void GroupsChanged()
    {
        RebuildGroups();
        ApplyFilter();
    }

    /// <summary>The link's own page (#121): both ends, either device, and what the neighbour announces.</summary>
    [RelayCommand]
    private Task OpenAsync(NeighbourLink? link) => link is null
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.NeighbourLink, new Dictionary<string, object> { [Routes.NeighbourLinkParameter] = link });

    /// <summary>Whether <paramref name="link"/> is in <paramref name="group"/> - Core's test, as desktop runs it.</summary>
    internal static bool InGroup(NeighbourViewDefinition group, NeighbourLink link) =>
        Neighbours.Matches(group, link.Neighbour, link.SwitchName, link.SwitchPortDescription);

    internal static IReadOnlyList<NeighbourLink> Build(
        IReadOnlyList<NetworkLink> links,
        IReadOnlyList<Device> devices,
        IReadOnlyList<Port> ports,
        DeviceNameStyle style)
    {
        var devicesById = devices.ToDictionary(d => d.DeviceId);
        var portsById = ports.ToDictionary(p => p.PortId);
        var seen = new HashSet<(int, int, int, int)>();
        var result = new List<NeighbourLink>();

        foreach (var link in links)
        {
            // The same cable reported from both ends is one link.
            if (link.RemoteDeviceId is { } rd && link.RemotePortId is { } rp)
            {
                var a = (link.LocalDeviceId, link.LocalPortId);
                var b = (rd, rp);
                var key = a.CompareTo(b) <= 0 ? (a.Item1, a.Item2, b.Item1, b.Item2) : (b.Item1, b.Item2, a.Item1, a.Item2);
                if (!seen.Add(key))
                {
                    continue;
                }
            }

            devicesById.TryGetValue(link.LocalDeviceId, out var local);
            Device? remote = link.RemoteDeviceId is { } id && devicesById.TryGetValue(id, out var found) ? found : null;
            portsById.TryGetValue(link.LocalPortId, out var localPort);
            Port? remotePort = link.RemotePortId is { } pid && portsById.TryGetValue(pid, out var p) ? p : null;

            var localName = local is null ? $"Device {link.LocalDeviceId}" : new DeviceItem(local, style).Name;
            var remoteName = remote is null ? link.DisplayRemoteName : new DeviceItem(remote, style).Name;
            var localPortName = localPort?.DisplayName ?? "?";
            var remotePortName = remotePort?.DisplayName ?? (string.IsNullOrWhiteSpace(link.RemotePort) ? "?" : link.RemotePort!);

            var down = new[]
            {
                local?.State == DeviceState.Down ? localName : null,
                remote?.State == DeviceState.Down ? remoteName : null,
                localPort is { IsUp: false } ? $"{localName} {localPortName}" : null,
                remotePort is { IsUp: false } ? $"{remoteName} {remotePortName}" : null,
            }.Where(s => s is not null).ToList();

            // As Devices' rows: the name, then what it is and where it plugs in.
            var platform = string.IsNullOrWhiteSpace(link.RemotePlatform) ? remote?.Hardware : link.RemotePlatform.Trim();
            var problem = down.Count > 0;
            result.Add(new NeighbourLink(
                new SectionRow(remoteName)
                {
                    Subtitle = $"{localName} {localPortName} ↔ {remotePortName}",
                    Value = link.Protocol?.ToUpperInvariant(),
                    Detail = problem ? "Down: " + string.Join(", ", down) : null,
                    Status = problem ? RowStatus.Critical : link.Active ? RowStatus.Ok : RowStatus.Inactive,
                },
                link.LocalDeviceId,
                localName,
                remote?.DeviceId,
                remoteName,
                problem,
                Neighbours.FromLinks([link])[0],
                local?.BestName ?? $"device {link.LocalDeviceId}", // as desktop names the switch for a Switch rule
                localPort?.IfAlias)
            {
                Local = new NeighbourEnd(localName, local?.DeviceId, localPortName, local is null ? null : local.State != DeviceState.Down, localPort?.IsUp),
                Remote = new NeighbourEnd(remoteName, remote?.DeviceId, remotePortName, remote is null ? null : remote.State != DeviceState.Down, remotePort?.IsUp),
                Platform = platform,
            });
        }

        return result
            .OrderByDescending(l => l.IsProblem)
            .ThenBy(l => l.RemoteName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.Row.Subtitle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The search, over every LLDP field as well as what the row shows.</summary>
    internal static bool Matches(NeighbourLink link, string term) =>
        link.Row.Matches(term)
        || new[]
        {
            link.Neighbour.AnnouncedName,
            link.Platform,
            link.Neighbour.Description,
            link.Neighbour.RemotePort,
            link.Neighbour.Mac,
            link.Neighbour.Protocol,
            link.SwitchPortDescription,
        }.Any(field => field?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);

    private IEnumerable<NeighbourLink> InGroup() =>
        SelectedGroup() is { } group ? _all.Where(l => InGroup(group, l)) : _all;

    private NeighbourViewDefinition? SelectedGroup() =>
        _settings.Current.NeighbourViews.FirstOrDefault(v => v.Id == _selectedGroupId);

    private void RebuildGroups()
    {
        // A group that has gone (deleted in the editor, or on desktop) shows everything again.
        if (SelectedGroup() is null)
        {
            _selectedGroupId = null;
        }

        Groups.Clear();
        Groups.Add(new NeighbourGroupChip(null, "All"));
        foreach (var group in _settings.Current.NeighbourViews)
        {
            Groups.Add(new NeighbourGroupChip(group, group.Name));
        }

        MarkSelectedGroup();
    }

    private void MarkSelectedGroup()
    {
        foreach (var chip in Groups)
        {
            chip.IsSelected = chip.Group?.Id == _selectedGroupId;
        }
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        Links.ReplaceAll(InGroup()
            .Where(l => l.IsProblem ? ShowDown : ShowUp)
            .Where(l => term.Length == 0 || Matches(l, term))
            .ToList());

        foreach (var chip in Groups)
        {
            chip.Count = chip.Group is { } group ? _all.Count(l => InGroup(group, l)) : _all.Count;
        }

        OnPropertyChanged(nameof(UpCount));
        OnPropertyChanged(nameof(DownCount));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(HasLoaded));
        OnPropertyChanged(nameof(HasActiveFilters));
    }
}
