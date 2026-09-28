using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Dashboard;

/// <summary>A sensor to pick: its reading, its device, and whether it's on the card.</summary>
public sealed partial class PickableSensor : ObservableObject
{
    public PickableSensor(Sensor sensor, string deviceName, SectionRow row, bool isPicked)
    {
        Sensor = sensor;
        DeviceName = deviceName;
        Row = row;
        _isPicked = isPicked;
    }

    public Sensor Sensor { get; }

    public string DeviceName { get; }

    public SectionRow Row { get; }

    [ObservableProperty]
    private bool _isPicked;
}

/// <summary>
/// Chooses the dashboard Sensors card's sensors, as desktop's widget picker:
/// search every sensor by name or device, tap to add or remove. Kept in the
/// card's own <see cref="DashboardWidget.Sensors"/>, and saved as it's tapped.
/// </summary>
public sealed partial class SensorPickerViewModel : ViewModelBase
{
    /// <summary>A search shows at most this many - the network may have thousands.</summary>
    internal const int MaxResults = 100;

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private IReadOnlyList<PickableSensor> _all = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    public SensorPickerViewModel(ILibreNmsClient client, ISettingsStore settings)
    {
        _client = client;
        _settings = settings;
    }

    /// <summary>What's on the card now, then search results.</summary>
    public BulkObservableCollection<PickableSensor> Sensors { get; } = new();

    public int PickedCount => _all.Count(s => s.IsPicked);

    public string HintText => string.IsNullOrWhiteSpace(SearchText)
        ? PickedCount == 0 ? "Search for a sensor or device to add." : "On the card. Search to add more."
        : Sensors.Count == 0 ? "No sensors match." : string.Empty;

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(ApplyFilter);

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var sensorsTask = _client.Sensors.ListAsync();
        var devicesTask = _client.Devices.ListAsync();
        await Task.WhenAll(sensorsTask, devicesTask);

        var style = _settings.Current.DeviceNameStyle;
        var names = devicesTask.Result.ToDictionary(d => d.DeviceId, d => new DeviceItem(d, style).Name);
        var picked = Widget().Sensors.Select(p => p.SensorId).ToHashSet();
        var settings = _settings.Current;

        _all = sensorsTask.Result
            .Select(s =>
            {
                var reading = DeviceSectionLoader.ReadSensor(s, settings);
                var device = names.TryGetValue(s.DeviceId, out var name) ? name : $"Device {s.DeviceId}";
                return new PickableSensor(s, device, new SectionRow(reading.Name)
                {
                    Value = reading.Value,
                    Status = reading.Status,
                    Subtitle = device,
                }, picked.Contains(s.SensorId));
            })
            .OrderBy(s => s.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Row.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ApplyFilter();
    });

    [RelayCommand]
    private void Toggle(PickableSensor? sensor)
    {
        if (sensor is null)
        {
            return;
        }

        var widget = Widget();
        sensor.IsPicked = !sensor.IsPicked;
        if (sensor.IsPicked)
        {
            widget.Sensors.Add(new PinnedSensor
            {
                SensorId = sensor.Sensor.SensorId,
                DeviceId = sensor.Sensor.DeviceId,
                SensorClass = sensor.Sensor.SensorClass ?? string.Empty,
                DeviceName = sensor.DeviceName,
                Description = sensor.Row.Title,
            });
        }
        else
        {
            widget.Sensors.RemoveAll(p => p.SensorId == sensor.Sensor.SensorId);
        }

        _settings.Save();
        OnPropertyChanged(nameof(PickedCount));
        OnPropertyChanged(nameof(HintText));
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        Sensors.ReplaceAll(term.Length == 0
            ? _all.Where(s => s.IsPicked).ToList()
            : _all.Where(s => s.Row.Matches(term)).Take(MaxResults).ToList());
        OnPropertyChanged(nameof(HintText));
    }

    private DashboardWidget Widget() => DashboardLayout.Ensure(_settings.Current, DashboardLayout.Sensors);
}
