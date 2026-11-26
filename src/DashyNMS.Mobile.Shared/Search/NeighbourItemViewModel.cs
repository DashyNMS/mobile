using CommunityToolkit.Mvvm.Input;
using DesktopNMS.Core;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Topology;

namespace DashyNMS.Mobile.Search;

/// <summary>
/// One link a monitored device has (#129), as Device View's neighbour cards
/// describe it - desktop's DeviceNeighbourItemViewModel without the editing
/// it does there.
/// </summary>
public sealed class NeighbourItemViewModel
{
    private readonly DeviceNeighbour _neighbour;
    private readonly Device? _device;

    public NeighbourItemViewModel(DeviceNeighbour neighbour, Device? device, string? deviceName, Port? localPort, string thisDeviceName, Action<int> openDevice)
    {
        _neighbour = neighbour;
        _device = device;
        Name = deviceName ?? neighbour.RemoteName;
        LocalPort = localPort;
        var who = neighbour.Source == NeighbourSource.ThisDevice ? thisDeviceName : Name;
        SourceText = $"Seen by {who}" + (neighbour.Protocols.Count > 0 ? " · " + string.Join(" + ", neighbour.Protocols) : string.Empty);
        OpenCommand = new RelayCommand(() => openDevice(neighbour.RemoteDeviceId!.Value), () => IsKnownDevice);
    }

    public string Name { get; }

    public bool IsKnownDevice => _neighbour.RemoteDeviceId is > 0 && _device is not null;

    public AlertSeverity StateSeverity => _device?.State switch
    {
        DeviceState.Up => AlertSeverity.Ok,
        DeviceState.Down => AlertSeverity.Critical,
        DeviceState.Maintenance => AlertSeverity.Warning,
        _ => AlertSeverity.Unknown,
    };

    public string StateText => _device is null ? "Not monitored" : _device.State.ToDisplayString();

    public string Description
    {
        get
        {
            var parts = _device is null ? new[] { _neighbour.Platform } : new[] { _device.Hardware, _device.Type, _device.Ip };
            var text = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            return text.Length > 0 ? text : "No details announced";
        }
    }

    public Port? LocalPort { get; }

    public string LocalPortText => LocalPort?.DisplayName ?? _neighbour.LocalPortName ?? "Unknown port";

    public string RemotePortText => !string.IsNullOrWhiteSpace(_neighbour.RemotePortName)
        ? PortLabels.FromNeighbourPort(_neighbour.RemotePortName) ?? _neighbour.RemotePortName!
        : "Unknown port";

    /// <summary>"Te1/1/1 to xe-0/0/1", the two ends of the cable.</summary>
    public string ConnectionText => $"{LocalPortText} to {RemotePortText}";

    public string LocalPortStateText => LocalPort is null ? string.Empty : PortText.State(LocalPort);

    public string SourceText { get; }

    public bool IsStale => !_neighbour.Active;

    /// <summary>Live links to known devices first.</summary>
    public int SortRank => (_neighbour.Active ? 0 : 2) + (IsKnownDevice ? 0 : 1);

    public string AccessibleText => $"{Name}, {StateText}. {ConnectionText}. {Description}" + (IsStale ? ". Not seen lately" : string.Empty);

    public RelayCommand OpenCommand { get; }
}
