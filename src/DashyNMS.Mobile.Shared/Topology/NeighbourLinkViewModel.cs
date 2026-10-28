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
    [NotifyPropertyChangedFor(nameof(HasLink), nameof(StatusText), nameof(Status), nameof(SubtitleText), nameof(Fields))]
    private NeighbourLink? _link;

    public NeighbourLinkViewModel(INavigationService navigation)
    {
        _navigation = navigation;
    }

    public bool HasLink => Link is not null;

    /// <summary>The badge: "Down" when either end is, "Not active" when LibreNMS has stopped seeing it, else "Up".</summary>
    public string StatusText => Link switch
    {
        null => string.Empty,
        { IsProblem: true } => "Down",
        { Neighbour.Active: false } => "Not active",
        _ => "Up",
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

    [RelayCommand]
    private Task OpenLocalAsync() => OpenAsync(Link?.Local);

    [RelayCommand]
    private Task OpenRemoteAsync() => OpenAsync(Link?.Remote);

    private Task OpenAsync(NeighbourEnd? end) => end?.DeviceId is { } id
        ? _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = id })
        : Task.CompletedTask;

    private static KeyValuePair<string, string> Field(string name, string? value) => new(name, value?.Trim() ?? string.Empty);
}
