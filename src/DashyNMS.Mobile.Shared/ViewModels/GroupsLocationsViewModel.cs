using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// Desktop's Groups and Locations tabs, as one page: every device group (or
/// location) with how many devices it has and how many are down. Tapping one
/// opens the Devices list filtered to it, rather than a second copy of the list.
/// </summary>
/// <remarks>
/// Groups and locations with devices down come first - on a phone, that's
/// what you look for. Membership is Core's GetMembershipByDeviceAsync, the
/// same the Devices list's Group filter uses; locations are the devices' own
/// location, with LibreNMS's location list adding coordinates and any that
/// have no devices yet.
/// </remarks>
public sealed partial class GroupsLocationsViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly INavigationService _navigation;
    private IReadOnlyList<PlaceRow> _groups = [];
    private IReadOnlyList<PlaceRow> _locations = [];
    private bool _loaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowingLocations))]
    private bool _showingGroups = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public GroupsLocationsViewModel(ILibreNmsClient client, INavigationService navigation)
    {
        _client = client;
        _navigation = navigation;
    }

    public bool ShowingLocations => !ShowingGroups;

    /// <summary>Groups or locations, filtered: counts on the right, red when any device is down.</summary>
    public BulkObservableCollection<SectionRow> Places { get; } = new();

    public bool IsEmpty => _loaded && Places.Count == 0 && !IsBusy;

    public string EmptyText => !string.IsNullOrWhiteSpace(SearchText) ? "Nothing matches."
        : ShowingGroups ? "LibreNMS has no device groups." : "No devices have a location.";

    partial void OnShowingGroupsChanged(bool value) => ApplyFilter();

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(ApplyFilter);

    [RelayCommand]
    private void ShowGroups() => ShowingGroups = true;

    [RelayCommand]
    private void ShowLocations() => ShowingGroups = false;

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var devicesTask = _client.Devices.ListAsync();
        var groupsTask = _client.DeviceGroups.ListAsync();
        var membershipTask = _client.DeviceGroups.GetMembershipByDeviceAsync();
        var locationsTask = LocationsAsync();
        await Task.WhenAll(devicesTask, groupsTask, membershipTask, locationsTask);

        var devices = devicesTask.Result;
        _groups = BuildGroups(devices, groupsTask.Result, membershipTask.Result);
        _locations = BuildLocations(devices, locationsTask.Result);
        _loaded = true;
        ApplyFilter();
    });

    [RelayCommand]
    private Task OpenAsync(SectionRow? row)
    {
        if (row is null)
        {
            return Task.CompletedTask;
        }

        var place = (ShowingGroups ? _groups : _locations).FirstOrDefault(p => p.Row == row);
        if (place is null)
        {
            return Task.CompletedTask;
        }

        return _navigation.GoToAsync(Routes.Devices, new Dictionary<string, object>
        {
            [ShowingGroups ? Routes.GroupParameter : Routes.LocationParameter] = place.Key,
        });
    }

    internal static IReadOnlyList<PlaceRow> BuildGroups(
        IReadOnlyList<Device> devices,
        IReadOnlyList<DeviceGroup> groups,
        IReadOnlyDictionary<int, IReadOnlyList<string>> membership)
    {
        var byGroup = new Dictionary<string, List<Device>>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in devices)
        {
            var names = membership.TryGetValue(device.DeviceId, out var found) && found.Count > 0
                ? found
                : [DevicesViewModel.NoGroupKey];
            foreach (var name in names)
            {
                if (!byGroup.TryGetValue(name, out var members))
                {
                    byGroup[name] = members = [];
                }

                members.Add(device);
            }
        }

        var rows = groups.Select(g => Place(
            g.Name,
            g.Name,
            string.Join(" · ", new[] { g.Description, TypeText(g.Type) }.Where(t => !string.IsNullOrWhiteSpace(t))),
            byGroup.GetValueOrDefault(g.Name) ?? []));

        // Devices in no group, as the Devices list's "Not in a group" filter.
        if (byGroup.TryGetValue(DevicesViewModel.NoGroupKey, out var ungrouped))
        {
            rows = rows.Append(Place(DevicesViewModel.NoGroupKey, "Not in a group", null, ungrouped));
        }

        return Order(rows);
    }

    internal static IReadOnlyList<PlaceRow> BuildLocations(IReadOnlyList<Device> devices, IReadOnlyList<Location> locations)
    {
        var byName = devices
            .Where(d => !string.IsNullOrWhiteSpace(d.Location))
            .GroupBy(d => d.Location!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var known = locations.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

        var names = byName.Keys.Concat(known.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
        return Order(names.Select(name => Place(
            name,
            name,
            known.TryGetValue(name, out var location) ? Coordinates(location) : null,
            byName.GetValueOrDefault(name) ?? [])));
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        Places.ReplaceAll((ShowingGroups ? _groups : _locations)
            .Where(p => term.Length == 0 || p.Row.Matches(term))
            .Select(p => p.Row)
            .ToList());

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    /// <summary>Best effort: without the location list, locations still come from the devices.</summary>
    private async Task<IReadOnlyList<Location>> LocationsAsync()
    {
        try
        {
            return await _client.Locations.ListAsync();
        }
        catch (LibreNmsApiException)
        {
            return [];
        }
    }

    private static PlaceRow Place(string key, string title, string? subtitle, IReadOnlyCollection<Device> members)
    {
        var down = members.Count(d => d.State == DeviceState.Down);
        var disabled = members.Count(d => d.State is DeviceState.Disabled or DeviceState.Ignored);
        return new PlaceRow(key, down, new SectionRow(title)
        {
            Subtitle = string.IsNullOrWhiteSpace(subtitle) ? null : subtitle,
            Value = $"{members.Count} {(members.Count == 1 ? "device" : "devices")}",
            Detail = string.Join(" · ", new[]
            {
                down > 0 ? $"{down} down" : null,
                disabled > 0 ? $"{disabled} disabled" : null,
            }.Where(t => t is not null)),
            Status = down > 0 ? RowStatus.Critical : members.Count > 0 ? RowStatus.Ok : RowStatus.None,
        });
    }

    /// <summary>Any with devices down first, then by name.</summary>
    private static IReadOnlyList<PlaceRow> Order(IEnumerable<PlaceRow> rows) => rows
        .OrderByDescending(p => p.Down > 0)
        .ThenBy(p => p.Key == DevicesViewModel.NoGroupKey)
        .ThenBy(p => p.Row.Title, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static string? TypeText(string? type) => type?.ToLowerInvariant() switch
    {
        "static" => "Static",
        "dynamic" => "Dynamic",
        _ => null,
    };

    private static string? Coordinates(Location location) => location is { Latitude: { } lat, Longitude: { } lng }
        ? string.Create(CultureInfo.InvariantCulture, $"{lat:0.####}, {lng:0.####}")
        : null;

    /// <summary>A group or location: its row, and the key the Devices filter takes.</summary>
    internal sealed record PlaceRow(string Key, int Down, SectionRow Row);
}
