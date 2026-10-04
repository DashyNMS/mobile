using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Topology;

namespace DashyNMS.Mobile.Map;

/// <summary>Leaflet's files, bundled with the app - the platform reads them from the app package.</summary>
public interface IMapAssets
{
    Task<(string Js, string Css)> LeafletAsync();
}

/// <summary>
/// Desktop's Geographical map: one pin per LibreNMS location, coloured by its
/// devices' state (red if any is down), on OpenStreetMap - or the tile server
/// in desktop's own MapTileUrl setting. Tap a pin for that location's devices.
/// </summary>
/// <remarks>
/// Placing devices is Core's <see cref="GeoLocations"/>, as desktop; devices
/// with no location, or a location without real coordinates, are counted
/// rather than dropped silently.
/// </remarks>
public sealed partial class MapViewModel : ViewModelBase, IRefreshable
{
    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly IMapAssets _assets;
    private IReadOnlyList<(MapPinData Pin, IReadOnlyList<DeviceItem> Devices)> _pins = [];

    [ObservableProperty]
    private string? _mapPage;

    [ObservableProperty]
    private string? _selectedLocation;

    [ObservableProperty]
    private string _unplacedText = string.Empty;

    public MapViewModel(ILibreNmsClient client, ISettingsStore settings, INavigationService navigation, IMapAssets assets)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
        _assets = assets;
    }

    /// <summary>Set by the page from the phone's theme.</summary>
    public bool DarkTheme { get; set; }

    /// <summary>The tapped pin's devices, down first.</summary>
    public BulkObservableCollection<DeviceItem> Devices { get; } = new();

    public bool HasSelection => SelectedLocation is not null;

    partial void OnSelectedLocationChanged(string? value) => OnPropertyChanged(nameof(HasSelection));

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var devicesTask = _client.Devices.ListAsync();
        var locationsTask = LocationsAsync();
        var assetsTask = _assets.LeafletAsync();
        await Task.WhenAll(devicesTask, locationsTask, assetsTask);

        var style = _settings.Current.DeviceNameStyle;
        var devices = devicesTask.Result;
        foreach (var device in devices)
        {
            // Geocoded locations arrive as an object with the id and coordinates in it (#84).
            DeviceLocation.CompleteFromLocationObject(device);
        }

        var byId = devices.ToDictionary(d => d.DeviceId);
        var placement = GeoLocations.Build(devices, locationsTask.Result);

        _pins = placement.Pins.Select((pin, index) =>
        {
            var members = pin.DeviceIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
            var state = members.Any(d => d.State == DeviceState.Down) ? "down"
                : members.Any(d => d.State == DeviceState.Up) ? "up"
                : "off";
            var items = members
                .Select(d => new DeviceItem(d, style))
                .OrderByDescending(d => d.State == DeviceState.Down)
                .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            // The name as the Devices tab's Location filter shows it, not a geocoded object's JSON.
            var name = DeviceLocation.Name(pin.Name) ?? pin.Name;
            var down = members.Count(d => d.State == DeviceState.Down);
            var maintenance = members.Count(d => d.State == DeviceState.Maintenance);
            var off = members.Count(d => d.State is DeviceState.Disabled or DeviceState.Ignored);
            var up = members.Count - down - maintenance - off;
            return (new MapPinData(index, name, pin.Latitude, pin.Longitude, members.Count, state, up, down, maintenance, off), (IReadOnlyList<DeviceItem>)items);
        }).ToList();

        var unplaced = placement.UnplacedDeviceIds.Count;
        UnplacedText = unplaced == 0 ? string.Empty
            : $"{unplaced} {(unplaced == 1 ? "device has" : "devices have")} no location with coordinates, so {(unplaced == 1 ? "isn't" : "aren't")} on the map.";

        var template = TileUrlTemplate.Normalise(_settings.Current.MapTileUrl) ?? TileUrlTemplate.Default;
        var (js, css) = assetsTask.Result;
        MapPage = MapHtml.Build(_pins.Select(p => p.Pin).ToList(), template, TileUrlTemplate.IsOpenStreetMap(template), DarkTheme, js, css);

        // Keep the chosen locations' list current, as far as they're still there.
        var kept = _pins.Where(p => _selectedNames.Contains(p.Pin.Name)).Select(p => p.Pin.Id).ToList();
        if (kept.Count > 0)
        {
            SelectPins(kept);
        }
        else
        {
            ClearSelection();
        }
    });

    private IReadOnlyList<string> _selectedNames = [];

    /// <summary>
    /// One location, when a single place is chosen - what Show in Devices
    /// filters to. A merged pin covers several, so it has none of its own.
    /// </summary>
    public bool HasSingleLocation => _selectedNames.Count == 1;

    /// <summary>A pin was tapped on the map.</summary>
    public void SelectPin(int id) => SelectPins([id]);

    /// <summary>
    /// A pin was tapped - one location, or every location in a merged pin:
    /// their devices together, down first, as desktop lists a merged pin's.
    /// </summary>
    public void SelectPins(IReadOnlyList<int> ids)
    {
        var chosen = ids.Where(id => id >= 0 && id < _pins.Count).Distinct().Select(id => _pins[id]).ToList();
        if (chosen.Count == 0)
        {
            return;
        }

        _selectedNames = chosen.Select(p => p.Pin.Name).ToList();
        OnPropertyChanged(nameof(HasSingleLocation));
        SelectedLocation = chosen.Count == 1 ? chosen[0].Pin.Name : $"{chosen.Count} locations";
        Devices.ReplaceAll(chosen
            .SelectMany(p => p.Devices)
            .OrderByDescending(d => d.State == DeviceState.Down)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase));
    }

    [RelayCommand]
    private void ClearSelection()
    {
        _selectedNames = [];
        OnPropertyChanged(nameof(HasSingleLocation));
        SelectedLocation = null;
        Devices.ReplaceAll([]);
    }

    /// <summary>
    /// The tapped location's devices on the Devices tab, filtered to it - as
    /// Device View's Location row does - so a pin leads somewhere (#84).
    /// </summary>
    [RelayCommand]
    private Task ShowInDevicesAsync() => !HasSingleLocation
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.Devices, new Dictionary<string, object> { [Routes.LocationParameter] = _selectedNames[0] });

    [RelayCommand]
    private Task OpenDeviceAsync(DeviceItem? device) => device is null
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = device.DeviceId });

    /// <summary>Best effort: without the list, devices still place by their own joined coordinates.</summary>
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
}
