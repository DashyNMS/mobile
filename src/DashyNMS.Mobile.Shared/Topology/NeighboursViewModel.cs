using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Topology;

/// <summary>One link between two ends: the row, and the devices a tap can open.</summary>
public sealed record NeighbourLink(SectionRow Row, int LocalDeviceId, string LocalName, int? RemoteDeviceId, string RemoteName, bool IsProblem);

/// <summary>
/// Every CDP/LLDP link LibreNMS knows, across the network - Device View's
/// Neighbours for all devices at once.
/// </summary>
/// <remarks>
/// Desktop's Neighbours tab is built around views the user defines over
/// what neighbours announce; on a phone the useful question is simpler -
/// what's connected to what, and is either end down. A link both devices
/// report (A sees B, B sees A) is one row. A link is a problem when either
/// device or either port is down; problems come first, and a filter shows
/// just them. Links to things LibreNMS doesn't monitor show the remote's
/// own name and platform.
/// </remarks>
public sealed partial class NeighboursViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private IReadOnlyList<NeighbourLink> _all = [];
    private bool _loaded;

    [ObservableProperty]
    private bool _problemsOnly;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public NeighboursViewModel(ILibreNmsClient client, ISettingsStore settings, INavigationService navigation, IDialogService dialogs)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
        _dialogs = dialogs;
    }

    public BulkObservableCollection<NeighbourLink> Links { get; } = new();

    public int ProblemCount => _all.Count(l => l.IsProblem);

    public string CountText => Links.Count == _all.Count
        ? $"{_all.Count} {(_all.Count == 1 ? "link" : "links")}"
        : $"{Links.Count} of {_all.Count} links";

    public bool IsEmpty => _loaded && Links.Count == 0 && !IsBusy;

    public string EmptyText => _all.Count == 0 ? "LibreNMS has no CDP or LLDP neighbours." : "No links match.";

    partial void OnProblemsOnlyChanged(bool value) => ApplyFilter();

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(ApplyFilter);

    [RelayCommand]
    private void ToggleProblemsOnly() => ProblemsOnly = !ProblemsOnly;

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

            var problem = down.Count > 0;
            result.Add(new NeighbourLink(
                new SectionRow($"{localName} ↔ {remoteName}")
                {
                    Subtitle = $"{localPortName} ↔ {remotePortName}",
                    Value = link.Protocol?.ToUpperInvariant(),
                    Detail = problem
                        ? "Down: " + string.Join(", ", down)
                        : remote is null ? link.RemotePlatform : null,
                    Status = problem ? RowStatus.Critical : link.Active ? RowStatus.Ok : RowStatus.Inactive,
                },
                link.LocalDeviceId,
                localName,
                remote?.DeviceId,
                remoteName,
                problem));
        }

        return result
            .OrderByDescending(l => l.IsProblem)
            .ThenBy(l => l.LocalName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.Row.Subtitle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        Links.ReplaceAll(_all
            .Where(l => !ProblemsOnly || l.IsProblem)
            .Where(l => term.Length == 0 || l.Row.Matches(term))
            .ToList());

        OnPropertyChanged(nameof(ProblemCount));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }
}
