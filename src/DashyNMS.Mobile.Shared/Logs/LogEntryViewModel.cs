using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Logs;

/// <summary>
/// One event log or alert log entry on its own page (#118), or beside the
/// list on a larger screen (#88): the full message, its device and time,
/// every detail, and the ways to narrow the list to entries like it.
/// </summary>
/// <remarks>The entry comes from the list rather than being fetched again - LibreNMS has no endpoint for one entry.</remarks>
public sealed partial class LogEntryViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private LogsViewModel? _list;

    /// <summary>Beside the list there's no page to go back from - "Only" just sets the chip.</summary>
    public bool IsBesideList { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEntry), nameof(HasDevice), nameof(HasRule), nameof(CanShowOnlyDevice), nameof(CanShowOnlyKind),
        nameof(ShowOnlyDeviceText), nameof(ShowOnlyKindText), nameof(HasNarrowing))]
    private LogEntryItem? _entry;

    public LogEntryViewModel(INavigationService navigation)
    {
        _navigation = navigation;
    }

    public bool HasEntry => Entry is not null;

    /// <summary>LibreNMS knows the device - and it isn't the one whose Event log this came from.</summary>
    public bool HasDevice => Entry is { HasDevice: true } entry && entry.DeviceId != _list?.DeviceId;

    /// <summary>An alert log entry's rule, for Open rule.</summary>
    public bool HasRule => Entry is { IsAlert: true, RuleId: > 0 };

    public bool CanShowOnlyDevice => Entry is { } entry && _list?.CanShowOnlyDevice(entry) == true;

    public bool CanShowOnlyKind => Entry is { } entry && _list?.CanShowOnlyKind(entry) == true;

    /// <summary>Either of the two - their card hides with neither.</summary>
    public bool HasNarrowing => CanShowOnlyDevice || CanShowOnlyKind;

    /// <summary>"Only core-sw-01's events".</summary>
    public string ShowOnlyDeviceText => Entry is { } entry
        ? $"Only {entry.DeviceName}'s {(entry.IsAlert ? "alerts" : "events")}"
        : string.Empty;

    /// <summary>"Only interface events", "Only recovered alerts".</summary>
    public string ShowOnlyKindText => Entry switch
    {
        { IsAlert: true } entry => $"Only {entry.StatusText.ToLower(CultureInfo.CurrentCulture)} alerts",
        { Type: { } type } => $"Only {type} events",
        _ => string.Empty,
    };

    /// <summary>The entry tapped, and the list it was tapped in.</summary>
    public void Load(LogEntryItem entry, LogsViewModel? list)
    {
        _list = list;
        Entry = entry;
    }

    [RelayCommand]
    private Task OpenDeviceAsync() => Entry is { } entry && HasDevice
        ? _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = entry.DeviceId })
        : Task.CompletedTask;

    [RelayCommand]
    private Task OpenRuleAsync() => Entry is { } entry && HasRule
        ? _navigation.GoToAsync(Routes.AlertRule, new Dictionary<string, object> { [Routes.RuleIdParameter] = entry.RuleId })
        : Task.CompletedTask;

    [RelayCommand]
    private Task ShowOnlyDeviceAsync() => NarrowAsync(CanShowOnlyDevice, list => list.ShowOnlyDevice(Entry!));

    [RelayCommand]
    private Task ShowOnlyKindAsync() => NarrowAsync(CanShowOnlyKind, list => list.ShowOnlyKind(Entry!));

    /// <summary>Sets the list's chip and goes back to the list.</summary>
    private async Task NarrowAsync(bool can, Action<LogsViewModel> narrow)
    {
        if (!can || _list is null)
        {
            return;
        }

        narrow(_list);
        OnPropertyChanged(nameof(CanShowOnlyDevice));
        OnPropertyChanged(nameof(CanShowOnlyKind));
        OnPropertyChanged(nameof(HasNarrowing));

        if (!IsBesideList)
        {
            await _navigation.GoToAsync(Routes.Back);
        }
    }
}
