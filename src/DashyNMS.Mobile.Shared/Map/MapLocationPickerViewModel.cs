using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Map;

/// <summary>A snapshot row whose identity is the pin object, never its display text.</summary>
public sealed record MapLocationChoice(MapPinData Pin, string Details, bool IsSelected)
{
    public string Name => Pin.Name;
    public string Description => $"{Name}, {Details}" + (IsSelected ? ", selected" : string.Empty);
}

/// <summary>Searches all located pins and commits one choice immediately to the calling map.</summary>
public sealed partial class MapLocationPickerViewModel(INavigationService navigation) : ViewModelBase
{
    private MapViewModel? _source;
    private IReadOnlyList<MapLocationChoice> _all = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    public BulkObservableCollection<MapLocationChoice> Choices { get; } = new();
    public string StatusText => HasError ? string.Empty : Choices.Count > 0 ? string.Empty
        : _all.Count == 0 ? "No map locations." : "No locations match your search.";
    public bool HasStatus => StatusText.Length > 0;

    public void Load(MapViewModel source)
    {
        _source = source;
        ErrorMessage = null;
        var pins = source.Locations.OrderBy(p => p.Name, NaturalOrder.Instance).ToList();
        var occurrences = new Dictionary<(string, double, double), int>();
        _all = pins.Select(pin =>
        {
            var key = (pin.Name, pin.Latitude, pin.Longitude);
            occurrences.TryGetValue(key, out var ordinal);
            occurrences[key] = ++ordinal;
            var details = FormattableString.Invariant($"{pin.Count} {(pin.Count == 1 ? "device" : "devices")} · {pin.Latitude:G17}, {pin.Longitude:G17}");
            // Only identical names/coordinates need an ordinal. This is not a server ID.
            if (ordinal > 1) details += $" · choice {ordinal}";
            return new MapLocationChoice(pin, details, source.SelectedPinIds.Contains(pin.Id));
        }).ToList();
        SearchText = string.Empty;
        Filter();
    }

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(Filter);

    private void Filter()
    {
        var term = SearchText.Trim();
        Choices.ReplaceAll(_all.Where(choice => term.Length == 0
            || choice.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || choice.Details.Contains(term, StringComparison.OrdinalIgnoreCase)));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasStatus));
    }

    [RelayCommand]
    private async Task ChooseAsync(MapLocationChoice? choice)
    {
        if (choice is null || !_all.Any(row => ReferenceEquals(row, choice)) || _source is null) return;
        if (_source.IsBusy || !_source.Locations.Any(pin => ReferenceEquals(pin, choice.Pin)))
        {
            ErrorMessage = "Map locations changed. Go back and reopen the location picker.";
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(HasStatus));
            return;
        }
        _source.SelectPin(choice.Pin.Id);
        await navigation.GoToAsync(Routes.Back);
    }
}
