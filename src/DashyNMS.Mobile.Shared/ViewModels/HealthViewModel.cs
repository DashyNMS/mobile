using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>One of desktop's Health categories, as a chip.</summary>
public sealed partial class HealthCategoryOption : ObservableObject
{
    public HealthCategoryOption(string sensorClass, string label)
    {
        SensorClass = sensorClass;
        Label = label;
    }

    public string SensorClass { get; }

    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Desktop's Health tab: every sensor across the network in desktop's four
/// categories (dBm, signal, temperature, fan speed), coloured against the
/// same thresholds as everywhere else, with severity filters and search.
/// </summary>
/// <remarks>
/// On a phone the problems come first - critical, then warning - rather than
/// desktop's straight device order, since that's what you open it to find.
/// Sensors with no severity (no data) always show, as unknown-severity alerts
/// do on the Alerts tab.
/// </remarks>
public sealed partial class HealthViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    /// <summary>Every sensor at once is a big fetch, so coming back to the tab within this reuses it; pull to refresh sooner.</summary>
    internal static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(1);

    private IReadOnlyList<HealthSensor> _all = [];
    private IReadOnlyList<DesktopNMS.Core.Models.Sensor> _sensors = [];
    private IReadOnlyDictionary<int, string> _names = new Dictionary<int, string>();
    private bool _loaded;
    private DateTimeOffset _loadedAt;

    [ObservableProperty]
    private HealthCategoryOption _selectedCategory;

    [ObservableProperty]
    private bool _showCritical = true;

    [ObservableProperty]
    private bool _showWarning = true;

    [ObservableProperty]
    private bool _showOk = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public HealthViewModel(ILibreNmsClient client, ISettingsStore settings, INavigationService navigation)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
        Categories = SensorCategoryRegistry.KnownClasses
            .Select(c => new HealthCategoryOption(c, SensorCategoryRegistry.Resolve(c)!.DisplayName))
            .ToList();
        _selectedCategory = Categories[0];
        _selectedCategory.IsSelected = true;

        // Thresholds changed in Settings: recolour what's already here.
        settings.Changed += (_, _) =>
        {
            if (_loaded)
            {
                Rebuild();
            }
        };
    }

    public IReadOnlyList<HealthCategoryOption> Categories { get; }

    /// <summary>The selected category's sensors, filtered: a sensor's reading on the right, its device beneath.</summary>
    public BulkObservableCollection<SectionRow> Sensors { get; } = new();

    public int CriticalCount => InCategory.Count(s => s.Row.Status == RowStatus.Critical);

    public int WarningCount => InCategory.Count(s => s.Row.Status == RowStatus.Warning);

    public int OkCount => InCategory.Count(s => s.Row.Status == RowStatus.Ok);

    public string CountText => Sensors.Count == InCategory.Count()
        ? $"{Sensors.Count} {SelectedCategory.Label.ToLowerInvariant()} sensor{(Sensors.Count == 1 ? string.Empty : "s")}"
        : $"{Sensors.Count} of {InCategory.Count()} {SelectedCategory.Label.ToLowerInvariant()} sensors";

    public bool HasActiveFilters => !ShowCritical || !ShowWarning || !ShowOk || !string.IsNullOrWhiteSpace(SearchText);

    public bool IsEmpty => _loaded && Sensors.Count == 0 && !IsBusy;

    public string EmptyText => InCategory.Any()
        ? "No sensors match these filters."
        : $"LibreNMS has no {SelectedCategory.Label.ToLowerInvariant()} sensors.";

    private IEnumerable<HealthSensor> InCategory =>
        _all.Where(s => string.Equals(s.SensorClass, SelectedCategory.SensorClass, StringComparison.OrdinalIgnoreCase));

    partial void OnSelectedCategoryChanged(HealthCategoryOption value)
    {
        foreach (var category in Categories)
        {
            category.IsSelected = category == value;
        }

        ApplyFilter();
    }

    partial void OnShowCriticalChanged(bool value) => ApplyFilter();

    partial void OnShowWarningChanged(bool value) => ApplyFilter();

    partial void OnShowOkChanged(bool value) => ApplyFilter();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SelectCategory(HealthCategoryOption? category)
    {
        if (category is not null)
        {
            SelectedCategory = category;
        }
    }

    [RelayCommand]
    private void ToggleCritical() => ShowCritical = !ShowCritical;

    [RelayCommand]
    private void ToggleWarning() => ShowWarning = !ShowWarning;

    [RelayCommand]
    private void ToggleOk() => ShowOk = !ShowOk;

    [RelayCommand]
    private void ClearFilters()
    {
        ShowCritical = ShowWarning = ShowOk = true;
        SearchText = string.Empty;
    }

    public Task RefreshIfStaleAsync() => _loaded && DateTimeOffset.UtcNow - _loadedAt < StaleAfter
        ? Task.CompletedTask
        : RefreshAsync();

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        // LibreNMS lists every sensor in one call; the device list gives them names.
        var sensorsTask = _client.Sensors.ListAsync();
        var devicesTask = _client.Devices.ListAsync();
        await Task.WhenAll(sensorsTask, devicesTask);

        var style = _settings.Current.DeviceNameStyle;
        _names = devicesTask.Result.ToDictionary(d => d.DeviceId, d => new DeviceItem(d, style).Name);
        _sensors = sensorsTask.Result;

        _loaded = true;
        _loadedAt = DateTimeOffset.UtcNow;
        Rebuild();
    });

    private void Rebuild()
    {
        var settings = _settings.Current;
        var names = _names;

        _all = _sensors
            .Where(s => SensorCategoryRegistry.Resolve(s.SensorClass) is not null)
            .Select(s =>
            {
                var reading = DeviceSectionLoader.ReadSensor(s, settings);
                var device = names.TryGetValue(s.DeviceId, out var name) ? name : $"Device {s.DeviceId}";
                return new HealthSensor(s.SensorClass!, device, new SectionRow(reading.Name)
                {
                    Value = reading.Value,
                    Status = reading.Status,
                    Subtitle = device,
                    Detail = reading.Limits,
                    LinkDeviceId = s.DeviceId,
                });
            })
            .OrderBy(s => Rank(s.Row.Status))
            .ThenBy(s => s.Device, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Row.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ApplyFilter();
    }

    /// <summary>A sensor opens its device's Sensors section, where it sits among the device's others.</summary>
    [RelayCommand]
    private Task OpenSensorAsync(SectionRow? row) => row?.LinkDeviceId is { } deviceId
        ? _navigation.GoToAsync(Routes.DeviceSection, new Dictionary<string, object>
        {
            [Routes.DeviceIdParameter] = deviceId,
            [Routes.SectionParameter] = DeviceSection.Sensors,
            [Routes.DeviceNameParameter] = row.Subtitle ?? string.Empty,
        })
        : Task.CompletedTask;

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        Sensors.ReplaceAll(InCategory
            .Where(s => s.Row.Status switch
            {
                RowStatus.Critical => ShowCritical,
                RowStatus.Warning => ShowWarning,
                RowStatus.Ok => ShowOk,
                _ => true,
            })
            .Where(s => term.Length == 0 || s.Row.Matches(term))
            .Select(s => s.Row)
            .ToList());

        OnPropertyChanged(nameof(CriticalCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(OkCount));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    private static int Rank(RowStatus status) => status switch
    {
        RowStatus.Critical => 0,
        RowStatus.Warning => 1,
        _ => 2,
    };

    private sealed record HealthSensor(string SensorClass, string Device, SectionRow Row);
}
