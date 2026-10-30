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
    }

    public string DeviceName { get; private set; } = "Device";

    public BulkObservableCollection<UnimusBackupItem> Backups { get; } = new();

    public bool IsEmpty => _checked && Backups.Count == 0 && !HasError;

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
        _deviceId = deviceId;
        DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Device" : deviceName;
        OnPropertyChanged(nameof(DeviceName));
    }

    public Task EnsureLoadedAsync() => _checked ? Task.CompletedTask : LoadAsync();

    /// <summary>Pull to refresh: Unimus's device list too, in case the device was added since.</summary>
    [RelayCommand]
    private Task RefreshAsync()
    {
        _setup.ForgetDevices();
        return LoadAsync();
    }

    private Task LoadAsync() => RunAsync(async () =>
    {
        try
        {
            IsComparing = false;
            var device = await _client.Devices.GetAsync(_deviceId.ToString(CultureInfo.InvariantCulture))
                ?? throw new InvalidOperationException("LibreNMS no longer has this device.");

            var match = await _setup.FindAsync(device);
            _unimusDeviceId = match?.Id;

            var backups = match is null
                ? []
                : (await _unimus.GetBackupsAsync(match.Id, 0, PageSize)).Backups;

            // The newest is the current one: Unimus's validUntil isn't a
            // "superseded at" time, as desktop found.
            var ordered = backups.OrderByDescending(b => b.ValidSince).ToList();
            Backups.ReplaceAll(ordered.Select((b, i) => new UnimusBackupItem(b, isCurrent: i == 0)));
        }
        catch (UnimusApiException)
        {
            Backups.ReplaceAll([]);
            throw;
        }
        finally
        {
            _checked = true;
            OnPropertyChanged(nameof(EmptyText));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(CanCompare));
            OnPropertyChanged(nameof(CompareHint));
        }
    });

    /// <summary>Reads a backup, or in Compare mode chooses it - a third choice replaces the first.</summary>
    [RelayCommand]
    private Task OpenAsync(UnimusBackupItem? item)
    {
        if (item is null || _unimusDeviceId is not { } unimusId)
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
        foreach (var item in Backups)
        {
            item.IsSelected = false;
        }

        IsComparing = !IsComparing;
    }

    private bool CanCompareSelected() => IsComparing && Selected.Count == 2;

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
        ToggleCompare();
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
    internal long SelectedAt { get; private set; }

    public string DateText => UnimusText.Date(Backup.ValidSinceUtc);

    /// <summary>"Current", or "Binary" for one only Unimus can show.</summary>
    public string StatusText => (IsCurrent ? "Current" : "Previous") + (Backup.IsText ? string.Empty : " · binary");

    partial void OnIsSelectedChanged(bool value) => SelectedAt = value ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
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
