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
    string? SwitchPortDescription);

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
public sealed partial class NeighboursViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
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

    public NeighboursViewModel(ILibreNmsClient client, ISettingsStore settings, INavigationService navigation, IDialogService dialogs)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
        _dialogs = dialogs;
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

    public string EmptyText => _all.Count == 0 ? "LibreNMS has no CDP or LLDP neighbours." : "No neighbours match.";

    partial void OnShowUpChanged(bool value) => ApplyFilter();

    partial void OnShowDownChanged(bool value) => ApplyFilter();

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(ApplyFilter);

    [RelayCommand]
    private void ToggleUp() => ShowUp = !ShowUp;

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
        var linksTask = _client.Links.ListAllAsync();
        var devicesTask = _client.Devices.ListAsync();
        var portsTask = _client.Ports.ListAllStatusAsync();
        await Task.WhenAll(linksTask, devicesTask, portsTask);

        _all = Build(linksTask.Result, devicesTask.Result, portsTask.Result, _settings.Current.DeviceNameStyle);
        _loaded = true;
        ApplyFilter();
    });

    /// <summary>Back from the groups editor: they may have been added, renamed, reordered or deleted.</summary>
    public void GroupsChanged()
    {
        RebuildGroups();
        ApplyFilter();
    }

    /// <summary>A link to another LibreNMS device asks which end to open; otherwise it opens the one end there is.</summary>
    [RelayCommand]
    private async Task OpenAsync(NeighbourLink? link)
    {
        if (link is null)
        {
            return;
        }

        var deviceId = link.LocalDeviceId;
        if (link.RemoteDeviceId is { } remote)
        {
            var choice = await _dialogs.ChooseAsync("Open", [link.LocalName, link.RemoteName]);
            if (choice is null)
            {
                return;
            }

            deviceId = choice == link.RemoteName ? remote : link.LocalDeviceId;
        }

        await _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = deviceId });
    }

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
            var platform = remote is null ? link.RemotePlatform : null;
            var problem = down.Count > 0;
            result.Add(new NeighbourLink(
                new SectionRow(remoteName)
                {
                    Subtitle = string.Join(" · ", new[] { platform, $"{localName} {localPortName} ↔ {remotePortName}" }.Where(s => !string.IsNullOrWhiteSpace(s))),
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
                localPort?.IfAlias));
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
    }
}
