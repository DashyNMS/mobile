using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

public sealed record MaintenanceBehaviorOption(MaintenanceBehavior Value)
{
    public string Label => Value.ToDisplayString();
}

public sealed record DurationPreset(string Label, int Hours, int Minutes);

/// <summary>
/// Schedules a maintenance window, as desktop's dialog: title, notes, now or
/// later, a duration, and what happens to alerts meanwhile.
/// </summary>
/// <remarks>
/// A later start is sent as the date and time picked, with no time zone
/// conversion - LibreNMS reads it as the server's own wall-clock time, the
/// same convention desktop (and every LibreNMS timestamp) uses.
/// </remarks>
public sealed partial class MaintenanceViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly TimeProvider _time;

    [ObservableProperty]
    private string _maintenanceTitle = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScheduledForLater))]
    private bool _startNow = true;

    [ObservableProperty]
    private DateTime _startDate;

    [ObservableProperty]
    private TimeSpan _startTime;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationText))]
    private int _durationHours = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationText))]
    private int _durationMinutes;

    [ObservableProperty]
    private MaintenanceBehaviorOption _selectedBehavior;

    public MaintenanceViewModel(ILibreNmsClient client, IDialogService dialogs, INavigationService navigation, TimeProvider time)
    {
        _client = client;
        _dialogs = dialogs;
        _navigation = navigation;
        _time = time;
        _selectedBehavior = Behaviors[0];

        // "Later" starts at the next quarter hour, so it's never already past.
        var now = _time.GetLocalNow().DateTime;
        var next = now.Date.AddMinutes(Math.Ceiling((now.TimeOfDay.TotalMinutes + 1) / 15) * 15);
        _startDate = next.Date;
        _startTime = next.TimeOfDay;
    }

    public int DeviceId { get; private set; }

    public string DeviceName { get; private set; } = string.Empty;

    public bool IsScheduledForLater => !StartNow;

    public IReadOnlyList<MaintenanceBehaviorOption> Behaviors { get; } =
    [
        new(MaintenanceBehavior.SkipAlerts),
        new(MaintenanceBehavior.MuteAlerts),
        new(MaintenanceBehavior.RunAlerts),
    ];

    public IReadOnlyList<DurationPreset> Presets { get; } =
    [
        new("30m", 0, 30),
        new("1h", 1, 0),
        new("2h", 2, 0),
        new("4h", 4, 0),
        new("8h", 8, 0),
        new("1 day", 24, 0),
    ];

    /// <summary>0, 5, ... 55 - the minutes picker's choices.</summary>
    public IReadOnlyList<int> MinuteChoices { get; } = Enumerable.Range(0, 12).Select(i => i * 5).ToList();

    public string DurationText => DurationHours == 0 ? $"{DurationMinutes} min"
        : DurationMinutes == 0 ? $"{DurationHours} h"
        : $"{DurationHours} h {DurationMinutes} min";

    public void Initialize(int deviceId, string deviceName)
    {
        DeviceId = deviceId;
        DeviceName = deviceName;
        OnPropertyChanged(nameof(DeviceName));
    }

    [RelayCommand]
    private void ApplyPreset(DurationPreset? preset)
    {
        if (preset is null)
        {
            return;
        }

        DurationHours = preset.Hours;
        DurationMinutes = preset.Minutes;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (DurationHours < 0 || DurationMinutes is < 0 or >= 60 || (DurationHours == 0 && DurationMinutes == 0))
        {
            ErrorMessage = "Choose a duration of at least a minute.";
            return;
        }

        string? start = null;
        if (!StartNow)
        {
            var at = StartDate.Date + StartTime;
            if (at < _time.GetLocalNow().DateTime.AddMinutes(-1))
            {
                ErrorMessage = "That start time has already passed - choose a later one, or start now.";
                return;
            }

            start = at.ToString("yyyy-MM-dd HH:mm:00", CultureInfo.InvariantCulture);
        }

        var request = new DeviceMaintenanceRequest
        {
            Title = string.IsNullOrWhiteSpace(MaintenanceTitle) ? null : MaintenanceTitle.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            Start = start,
            Duration = $"{DurationHours}:{DurationMinutes:D2}",
            Behavior = (int)SelectedBehavior.Value,
        };

        string? message = null;
        if (await RunAsync(async () => message = await _client.Devices.ScheduleMaintenanceAsync(DeviceId, request)))
        {
            // LibreNMS's own confirmation ("will begin maintenance mode at ...").
            await _dialogs.AlertAsync("Maintenance scheduled", string.IsNullOrWhiteSpace(message) ? $"Maintenance scheduled for {DeviceName}." : message);
            await _navigation.GoToAsync(Routes.Back);
        }
    }

    [RelayCommand]
    private Task CancelAsync() => _navigation.GoToAsync(Routes.Back);
}
