using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
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

    public string SelectionDescription => $"{Row.Title}, {DeviceName}, {(IsPicked ? "selected" : "not selected")}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionDescription))]
    private bool _isPicked;
}

/// <summary>
/// Chooses a dashboard Sensors card's sensors and title, as desktop's widget picker:
/// search every sensor by name or device, tap to add or remove. Kept in the
/// card's own <see cref="DashboardWidget.Sensors"/> only when Save is chosen.
/// </summary>
public sealed partial class SensorPickerViewModel : ViewModelBase
{
    /// <summary>A search shows at most this many - the network may have thousands.</summary>
    internal const int MaxResults = 100;

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private IReadOnlyList<PickableSensor> _all = [];
    private readonly INavigationService _navigation;
    private List<PinnedSensor> _draft = [];
    private bool _initialised;
    private bool _loaded;
    private bool _saved;

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>The card's own title ("Core switch temps"); blank for the kind's, "Sensors" (#87).</summary>
    [ObservableProperty]
    private string _cardTitle = string.Empty;

    /// <summary>Which card this sets up - from Customise or the card itself (#87).</summary>
    public string? WidgetId { get; set; }

    public SensorPickerViewModel(ILibreNmsClient client, ISettingsStore settings, INavigationService navigation, CorePolling? polling = null)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
        _polling = polling;
    }

    private readonly CorePolling? _polling;

    /// <summary>The shared fleet list, its state sensors named (#210); LibreNMS's own if there's none.</summary>
    private async Task<IReadOnlyList<Sensor>> SensorListAsync()
    {
        if (_polling is null)
        {
            return await _client.Sensors.ListAsync();
        }

        var result = await _polling.SensorsAsync();
        return SensorRefreshStatus.HasNothingToShow(result)
            ? throw new InvalidOperationException(SensorRefreshStatus.Describe(result))
            : result.Sensors;
    }

    /// <summary>What's on the card now, then search results.</summary>
    public BulkObservableCollection<PickableSensor> Sensors { get; } = new();

    public int PickedCount => _draft.Count;

    public string SelectedText => $"{PickedCount} selected";

    public string HintText => string.IsNullOrWhiteSpace(SearchText)
        ? PickedCount == 0 ? "Search for a sensor or device to add." : "On the card. Search to add more."
        : Sensors.Count == 0 ? "No sensors match." : string.Empty;

    partial void OnCardTitleChanged(string value) => _saved = false;

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(ApplyFilter);

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        // Retry a failed fetch without resetting edits or touching persisted settings.
        if (!_initialised)
        {
            var card = Widget();
            CardTitle = card is not null && DashboardLayout.HasOwnTitle(card) ? card.Title : string.Empty;
            _draft = card?.Sensors.ToList() ?? [];
            _initialised = true;
            SelectionChanged();
        }

        var sensorsTask = SensorListAsync();
        var devicesTask = _client.Devices.ListAsync();
        await Task.WhenAll(sensorsTask, devicesTask);

        var style = _settings.Current.DeviceNameStyle;
        var names = devicesTask.Result.ToDictionary(d => d.DeviceId, d => new DeviceItem(d, style).Name);
        var picked = _draft.Select(p => p.SensorId).ToHashSet();
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
            .OrderBy(s => s.DeviceName, NaturalOrder.Instance)
            .ThenBy(s => s.Row.Title, NaturalOrder.Instance)
            .ToList();

        _loaded = true;
        SaveCommand.NotifyCanExecuteChanged();
        ApplyFilter();
    });

    [RelayCommand]
    private void Toggle(PickableSensor? sensor)
    {
        if (sensor is null)
        {
            return;
        }

        _saved = false;
        sensor.IsPicked = !sensor.IsPicked;
        if (sensor.IsPicked)
        {
            _draft.Add(new PinnedSensor
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
            _draft.RemoveAll(p => p.SensorId == sensor.Sensor.SensorId);
        }

        SelectionChanged();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        Sensors.ReplaceAll(term.Length == 0
            ? _all.Where(s => s.IsPicked).ToList()
            : _all.Where(s => s.Row.Matches(term)).Take(MaxResults).ToList());
        OnPropertyChanged(nameof(HintText));
    }

    private void SelectionChanged()
    {
        OnPropertyChanged(nameof(PickedCount));
        OnPropertyChanged(nameof(SelectedText));
        OnPropertyChanged(nameof(HintText));
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (!_saved)
        {
            var settings = _settings.Current;
            var previousWidgets = settings.DashboardWidgets.ToList();
            var widget = Widget();
            var previousTitle = widget?.Title;
            var previousSensors = widget?.Sensors;
            try
            {
                widget ??= DashboardLayout.Add(settings, DashboardLayout.Sensors);
                widget.Title = string.IsNullOrWhiteSpace(CardTitle)
                    ? DashboardLayout.KindOf(DashboardLayout.Sensors).Title : CardTitle.Trim();
                widget.Sensors = _draft.ToList();
                _settings.Save();
                WidgetId = widget.Id;
                _saved = true;
            }
            catch
            {
                // A failed write must not leave a new card or half-applied edits in memory.
                settings.DashboardWidgets = previousWidgets;
                if (widget is not null && previousSensors is not null)
                {
                    widget.Title = previousTitle!;
                    widget.Sensors = previousSensors;
                }
                throw;
            }
        }

        // If navigation fails after persistence, retry Back without saving twice.
        await _navigation.GoToAsync(Routes.Back);
    }, isRead: false);

    private bool CanSave() => _loaded && !IsBusy;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsBusy)) SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Opening or cancelling setup never creates a dashboard card.</summary>
    private DashboardWidget? Widget() =>
        DashboardLayout.Find(_settings.Current, WidgetId) is { WidgetType: DashboardLayout.Sensors } widget ? widget : null;
}
