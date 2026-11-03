using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Topology;

/// <summary>
/// One neighbour link on its own page (#121), or beside the list on a larger
/// screen (#88): both ends and their ports' states, either device to open,
/// and everything the neighbour announces - in place of asking which end
/// to open.
/// </summary>
/// <remarks>The link comes from the list - its fetch already has every field.</remarks>
public sealed partial class NeighbourLinkViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenLocalCommand), nameof(OpenRemoteCommand))]
    [NotifyPropertyChangedFor(nameof(HasLink), nameof(HasNoLink), nameof(StatusText), nameof(Status), nameof(SubtitleText), nameof(Fields), nameof(HasNoFields), nameof(LocalPortText), nameof(RemotePortText), nameof(LocalStateText), nameof(RemoteStateText), nameof(LocalActionDescription), nameof(RemoteActionDescription))]
    private NeighbourLink? _link;

    public NeighbourLinkViewModel(INavigationService navigation)
    {
        _navigation = navigation;
    }

    public bool HasLink => Link is not null;
    public bool HasNoLink => !HasLink;
    public bool HasNoFields => Fields.Count == 0;
    public string LocalPortText => PortText(Link?.Local);
    public string RemotePortText => PortText(Link?.Remote);
    public string LocalStateText => StateText(Link?.Local);
    public string RemoteStateText => StateText(Link?.Remote);
    public string LocalActionDescription => ActionDescription("local", Link?.Local);
    public string RemoteActionDescription => ActionDescription("remote", Link?.Remote);

    private static string PortText(NeighbourEnd? end) => string.IsNullOrWhiteSpace(end?.Port) || end.Port == "?" ? "Port unknown" : end.Port;
    private static string StateText(NeighbourEnd? end) => string.IsNullOrWhiteSpace(end?.StateText) ? "State unknown" : end.StateText;
    private string ActionDescription(string side, NeighbourEnd? end) => string.Join(" · ",
        new[] { $"Open {side} device", end?.Name, PortText(end), StateText(end), StatusText }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>Active describes discovery, without claiming unknown endpoint health is up.</summary>
    public string StatusText => Link switch
    {
        null => string.Empty,
        { IsProblem: true } => "Down",
        { Neighbour.Active: false } => "Not active",
        _ => "Active",
    };

    public RowStatus Status => Link?.Row.Status ?? RowStatus.None;

    /// <summary>"LLDP · Aruba AP-535".</summary>
    public string SubtitleText => Link is null ? string.Empty : string.Join(" · ", new[] { Link.Row.Value, Link.Platform }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>What the neighbour announces, and the switch port's description - the fields the neighbourhood rules test.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Fields => Link is null ? [] : new[]
    {
        Field("System name", Link.Neighbour.AnnouncedName),
        Field("System description", Link.Neighbour.Description),
        Field("Platform", Link.Platform),
        Field("Port ID", Link.Neighbour.RemotePort),
        Field("MAC address", Link.Neighbour.Mac),
        Field("Protocol", Link.Row.Value),
        Field("Switch port description", Link.SwitchPortDescription),
    }.Where(f => f.Value.Length > 0).ToList();

    public void Load(NeighbourLink link) => Link = link;

    private bool CanOpenLocal() => Link?.Local.CanOpen == true;
    private bool CanOpenRemote() => Link?.Remote.CanOpen == true;

    [RelayCommand(CanExecute = nameof(CanOpenLocal))]
    private Task OpenLocalAsync() => OpenAsync(Link?.Local);

    [RelayCommand(CanExecute = nameof(CanOpenRemote))]
    private Task OpenRemoteAsync() => OpenAsync(Link?.Remote);

    private Task OpenAsync(NeighbourEnd? end) => end?.DeviceId is { } id
        ? _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = id })
        : Task.CompletedTask;

    private static KeyValuePair<string, string> Field(string name, string? value) => new(name, value ?? string.Empty);
}
