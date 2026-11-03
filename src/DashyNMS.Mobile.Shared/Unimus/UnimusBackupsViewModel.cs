using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Unimus;

/// <summary>
/// A device's Config backups (#21): its Unimus backups, newest first, as
/// desktop's Unimus tab. Tap one to read it; Compare picks two to diff.
/// </summary>
public sealed partial class UnimusBackupsViewModel : ViewModelBase, IRefreshable
{
    /// <summary>A page of history - plenty on a phone, and what desktop shows.</summary>
    internal const int PageSize = 50;

    private readonly ILibreNmsClient _client;
    private readonly UnimusSetup _setup;
    private readonly IUnimusApi _unimus;
    private readonly INavigationService _navigation;
    private int _deviceId;
    private int? _unimusDeviceId;
    private bool _checked;
    private int _generation;
    private int _loadedRevision;

    public string DeviceIdentity => $"LibreNMS device {_deviceId}" + (_unimusDeviceId is { } id ? $" · Unimus device {id}" : string.Empty);
    public string HistoryText => !_checked ? "Loading backup history…" : Backups.Count > 0
        ? $"{Backups.Count} {(Backups.Count == 1 ? "backup" : "backups")} loaded" + (Backups.Count == PageSize ? $" · latest {PageSize}" : string.Empty) : "Backup history";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompareText), nameof(CompareHint))]
    [NotifyCanExecuteChangedFor(nameof(CompareSelectedCommand))]
    private bool _isComparing;

    public UnimusBackupsViewModel(ILibreNmsClient client, UnimusSetup setup, IUnimusApi unimus, INavigationService navigation)
    {
        _client = client;
        _setup = setup;
        _unimus = unimus;
        _navigation = navigation;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsBusy))
            {
                OnPropertyChanged(nameof(IsEmpty));
                CompareSelectedCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public string DeviceName { get; private set; } = "Device";

    public BulkObservableCollection<UnimusBackupItem> Backups { get; } = new();

    public bool HasBackups => Backups.Count > 0;

    public bool IsEmpty => !IsBusy && _checked && Backups.Count == 0 && !HasError;

    /// <summary>Why there's nothing: no match in Unimus, or a match with no backups yet.</summary>
    public string EmptyText => _unimusDeviceId is null
        ? "Unimus doesn't have this device. It's matched by host name, then by IP address; if LibreNMS uses short names, add your domain in Settings › Unimus."
        : "Unimus hasn't backed this device up yet.";

    public bool CanCompare => Backups.Count >= 2;

    public string CompareText => IsComparing ? "Cancel" : "Compare";

    /// <summary>What Compare mode is waiting for.</summary>
    public string CompareHint => Selected.Count switch
    {
        0 => "Choose two backups to compare.",
        1 => "Choose one more backup.",
        _ => "Ready to compare.",
    };

    private List<UnimusBackupItem> Selected => Backups.Where(b => b.IsSelected).ToList();

    public void Initialise(int deviceId, string? deviceName)
    {
        if (_deviceId != deviceId)
        {
            _generation++;
            _checked = false;
            _unimusDeviceId = null;
            Backups.ReplaceAll([]);
            IsComparing = false;
        }
        _deviceId = deviceId;
        DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Device" : deviceName;
        OnPropertyChanged(nameof(HistoryText));
        OnPropertyChanged(nameof(HasBackups));
        OnPropertyChanged(nameof(DeviceName));
        OnPropertyChanged(nameof(DeviceIdentity));
    }

    public Task EnsureLoadedAsync() => _checked && _loadedRevision == _setup.Revision ? Task.CompletedTask : LoadAsync();

    /// <summary>Pull to refresh: Unimus's device list too, in case the device was added since.</summary>
    [RelayCommand]
    private Task RefreshAsync()
    {
        if (IsBusy || OpenCommand.IsRunning || CompareSelectedCommand.IsRunning) return Task.CompletedTask;
        _setup.ForgetDevices();
        return LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (IsBusy) return;
        var generation = _generation;
        var revision = _setup.Revision;
        var cancelled = false;
        ClearReplacedConnection();
        await RunAsync(async () =>
        {
            try
            {
                await _setup.EnsureConfiguredAsync();
                revision = _setup.Revision;
                ClearReplacedConnection();
                var device = await _client.Devices.GetAsync(_deviceId.ToString(CultureInfo.InvariantCulture))
                    ?? throw new InvalidOperationException("LibreNMS no longer has this device.");

                var match = await _setup.FindAsync(device);
                if (!_setup.IsConfigured)
                    throw new InvalidOperationException("Unimus is unavailable. Check Settings › Unimus, then retry.");

                var backups = match is null
                    ? []
                    : (await _unimus.GetBackupsAsync(match.Id, 0, PageSize)).Backups;

                // Unimus's validUntil is not a superseded-at time.
                var ordered = backups.OrderByDescending(b => b.ValidSince).ToList();
                if (generation != _generation || revision != _setup.Revision) return;
                var selected = _unimusDeviceId == match?.Id ? Selected.ToDictionary(b => b.Backup.Id) : [];
                _unimusDeviceId = match?.Id;
                _loadedRevision = revision;
                Backups.ReplaceAll(ordered.Select((b, i) => new UnimusBackupItem(b, isCurrent: i == 0 && b.ValidSince is not null)
                {
                    IsSelected = selected.ContainsKey(b.Id),
                    SelectedAt = selected.TryGetValue(b.Id, out var previous) ? previous.SelectedAt : 0,
                }));
                if (!CanCompare) IsComparing = false;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                throw;
            }
            finally
            {
                _checked = !cancelled && generation == _generation && revision == _setup.Revision;
                OnPropertyChanged(nameof(HasBackups));
                OnPropertyChanged(nameof(EmptyText));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(CanCompare));
                OnPropertyChanged(nameof(CompareHint));
                OnPropertyChanged(nameof(DeviceIdentity));
                OnPropertyChanged(nameof(HistoryText));
                CompareSelectedCommand.NotifyCanExecuteChanged();
            }
        });
        if (generation != _generation || revision != _setup.Revision) await LoadAsync();
    }

    /// <summary>History from replaced credentials must never appear as current data.</summary>
    private void ClearReplacedConnection()
    {
        if (_loadedRevision == _setup.Revision) return;
        Backups.ReplaceAll([]);
        _unimusDeviceId = null;
        _checked = false;
        IsComparing = false;
        OnPropertyChanged(nameof(HasBackups));
        OnPropertyChanged(nameof(DeviceIdentity));
        OnPropertyChanged(nameof(HistoryText));
        OnPropertyChanged(nameof(CanCompare));
        CompareSelectedCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Reads a backup, or in Compare mode chooses it - a third choice replaces the first.</summary>
    [RelayCommand]
    private Task OpenAsync(UnimusBackupItem? item)
    {
        if (IsBusy || _loadedRevision != _setup.Revision || item is null || !Backups.Contains(item) || _unimusDeviceId is not { } unimusId)
        {
            return Task.CompletedTask;
        }

        if (!IsComparing)
        {
            return _navigation.GoToAsync(Routes.UnimusBackup, new Dictionary<string, object>
            {
                [Routes.UnimusParameter] = new UnimusBackupRequest(DeviceName, unimusId, item.Backup),
            });
        }

        if (item.IsSelected)
        {
            item.IsSelected = false;
        }
        else
        {
            var selected = Selected;
            if (selected.Count >= 2)
            {
                selected.OrderBy(s => s.SelectedAt).First().IsSelected = false;
            }

            item.IsSelected = true;
        }

        OnPropertyChanged(nameof(CompareHint));
        CompareSelectedCommand.NotifyCanExecuteChanged();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void ToggleCompare()
    {
        if (IsBusy || OpenCommand.IsRunning || CompareSelectedCommand.IsRunning || (!IsComparing && !CanCompare)) return;
        foreach (var item in Backups)
        {
            item.IsSelected = false;
        }

        IsComparing = !IsComparing;
    }

    private bool CanCompareSelected() => !IsBusy && _loadedRevision == _setup.Revision && IsComparing && Selected.Count == 2;

    /// <summary>The older of the two on the left, as a diff reads.</summary>
    [RelayCommand(CanExecute = nameof(CanCompareSelected))]
    private async Task CompareSelectedAsync()
    {
        var pair = Selected.OrderBy(b => b.Backup.ValidSince).ToList();
        if (pair.Count != 2)
        {
            return;
        }

        await _navigation.GoToAsync(Routes.UnimusDiff, new Dictionary<string, object>
        {
            [Routes.UnimusParameter] = new UnimusDiffRequest(DeviceName, pair[0].Backup, pair[1].Backup),
        });
        foreach (var item in Backups) item.IsSelected = false;
        IsComparing = false;
    }
}

/// <summary>One backup in a device's list.</summary>
public sealed partial class UnimusBackupItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public UnimusBackupItem(UnimusBackup backup, bool isCurrent)
    {
        Backup = backup;
        IsCurrent = isCurrent;
    }

    public UnimusBackup Backup { get; }

    public bool IsCurrent { get; }

    /// <summary>When it was chosen in Compare mode, so a third choice replaces the earliest.</summary>
    internal long SelectedAt { get; set; }

    public string DateText => UnimusText.Date(Backup.ValidSinceUtc);
    public string VersionText => $"Backup {Backup.Id}";
    public string ExactDateText => Backup.ValidSinceUtc is { } date
        ? date.ToLocalTime().ToString("d MMM yyyy HH:mm:ss zzz", CultureInfo.CurrentCulture) : "Unknown date";
    public string ContentDescription => $"{VersionText}, {ExactDateText}, {StatusText}, {(IsSelected ? "selected" : "not selected")}";

    /// <summary>"Current", or "Binary" for one only Unimus can show.</summary>
    public string StatusText => (Backup.ValidSince is null ? "Date unavailable" : IsCurrent ? "Current" : "Previous")
        + (Backup.IsText ? string.Empty : string.Equals(Backup.Type, "BINARY", StringComparison.OrdinalIgnoreCase) ? " · binary" : " · format unavailable");

    partial void OnIsSelectedChanged(bool value)
    {
        SelectedAt = value ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        OnPropertyChanged(nameof(ContentDescription));
    }
}

/// <summary>Where a backup or diff page is opened from.</summary>
public sealed record UnimusBackupRequest(string DeviceName, int UnimusDeviceId, UnimusBackup Backup);

/// <summary>Two backups to compare, the older first.</summary>
public sealed record UnimusDiffRequest(string DeviceName, UnimusBackup Original, UnimusBackup Revised);

internal static class UnimusText
{
    /// <summary>"18 Sep 2026 18:01" in the phone's time zone, as desktop shows backups.</summary>
    public static string Date(DateTime? utc) => utc is { } t
        ? t.ToLocalTime().ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture)
        : "Unknown date";
}
