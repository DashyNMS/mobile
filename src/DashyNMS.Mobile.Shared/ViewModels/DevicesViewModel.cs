using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

public enum DeviceSort
{
    Name,
    Status,
    IpAddress,
    Uptime,
    Location,
    Os,
    Hardware,
}

public sealed record SortOption(DeviceSort Sort, string Label);

/// <summary>One choice in a Type / Location / Group filter: <see cref="Key"/> null is "all".</summary>
public sealed record FacetOption(string? Key, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Every device, as desktop's Devices tab: state chips (up, down,
/// maintenance, disabled), search, Type / Location / Group filters, a choice
/// of sort, pinned devices on top, and the recently viewed strip.
/// </summary>
/// <remarks>
/// Desktop's facets are multi-select checkbox lists; here each is a single
/// choice, which suits a phone. Maintenance is looked up after the list is
/// already shown - see <see cref="MaintenanceScan"/>, shared with the dashboard.
/// </remarks>
public sealed partial class DevicesViewModel : ViewModelBase
{
    /// <summary>The Group filter's "Not in a group" choice.</summary>
    internal const string NoGroupKey = "\0none";

    private readonly ILibreNmsClient _client;
    private readonly INavigationService _navigation;
    private readonly IDialogService? _dialogs;
    private readonly ISettingsStore _settings;
    private readonly DeviceBookmarks _bookmarks;
    private readonly MaintenanceScan _maintenance;
    private List<DeviceItem> _all = [];

    private int? _selectedId;
    private IReadOnlyDictionary<int, IReadOnlyList<string>> _groups = new Dictionary<int, IReadOnlyList<string>>();
    private IReadOnlySet<int> _maintenanceIds = new HashSet<int>();
    private bool _resetting;

    /// <summary>The state chips as the user last left them (#81).</summary>
    private readonly ChipMemory _chips;

    /// <summary>
    /// Showing a shortcut - a dashboard count, or a group or location - not
    /// the user's own chips: not saved, and <see cref="ShowSavedFilter"/> puts
    /// theirs back.
    /// </summary>
    private bool _shortcut;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showUp = true;

    [ObservableProperty]
    private bool _showDown = true;

    [ObservableProperty]
    private bool _showMaintenance = true;

    /// <summary>Disabled and ignored together, as desktop's chip.</summary>
    [ObservableProperty]
    private bool _showDisabled = true;

    [ObservableProperty]
    private SortOption _selectedSort;

    [ObservableProperty]
    private FacetOption _selectedType = AllTypes;

    [ObservableProperty]
    private FacetOption _selectedLocation = AllLocations;

    [ObservableProperty]
    private FacetOption _selectedGroup = AllGroups;

    /// <summary>The Filters panel (sort, type, location, group) is open.</summary>
    [ObservableProperty]
    private bool _isFilterPanelOpen;

    public DevicesViewModel(
        ILibreNmsClient client,
        INavigationService navigation,
        ISettingsStore settings,
        DeviceBookmarks bookmarks,
        TimeProvider time,
        MaintenanceScan? maintenance = null,
        IAppPreferences? preferences = null,
        IDialogService? dialogs = null)
    {
        _dialogs = dialogs;
        _client = client;
        _navigation = navigation;
        _settings = settings;
        _bookmarks = bookmarks;
        _maintenance = maintenance ?? new MaintenanceScan(client, time);
        _chips = new ChipMemory(preferences ?? new InMemoryPreferences(), "devices");
        _showUp = _chips.Get("up");
        _showDown = _chips.Get("down");
        _showMaintenance = _chips.Get("maintenance");
        _showDisabled = _chips.Get("disabled");
        _selectedSort = SortOptions[0];
        _bookmarks.Changed += (_, _) => OnBookmarksChanged();
        RebuildRecent();

        Selection.Changed += (_, _) =>
        {
            MarkSelected();
            OnPropertyChanged(nameof(SelectAllText));
        };
    }

    /// <summary>Ticking several devices to pin, rediscover or put in maintenance at once (#85).</summary>
    public BulkSelection Selection { get; } = new("device");

    /// <summary>"Select all" for what the filters show now, or "Select none".</summary>
    public string SelectAllText => Selection.ToggleAllText(ShownIds());

    private static FacetOption AllTypes { get; } = new(null, "All types");

    private static FacetOption AllLocations { get; } = new(null, "All locations");

    private static FacetOption AllGroups { get; } = new(null, "All groups");

    public BulkObservableCollection<DeviceItem> Devices { get; } = new();

    public BulkObservableCollection<RecentlyViewedDevice> RecentlyViewed { get; } = new();

    public IReadOnlyList<SortOption> SortOptions { get; } =
    [
        new(DeviceSort.Name, "Name"),
        new(DeviceSort.Status, "Status (down first)"),
        new(DeviceSort.IpAddress, "IP address"),
        new(DeviceSort.Uptime, "Uptime (shortest first)"),
        new(DeviceSort.Location, "Location"),
        new(DeviceSort.Os, "OS"),
        new(DeviceSort.Hardware, "Hardware"),
    ];

    public BulkObservableCollection<FacetOption> TypeOptions { get; } = [AllTypes];

    public BulkObservableCollection<FacetOption> LocationOptions { get; } = [AllLocations];

    public BulkObservableCollection<FacetOption> GroupOptions { get; } = [AllGroups];

    public int UpCount => _all.Count(d => d.State == DeviceState.Up);

    public int DownCount => _all.Count(d => d.State == DeviceState.Down);

    public int MaintenanceCount => _all.Count(d => d.State == DeviceState.Maintenance);

    public int DisabledCount => _all.Count(d => d.State is DeviceState.Disabled or DeviceState.Ignored);

    /// <summary>"340 devices", or "12 of 340 devices" when filtered.</summary>
    public string CountText => Devices.Count == _all.Count
        ? $"{_all.Count} {Plural(_all.Count)}"
        : $"{Devices.Count} of {_all.Count} {Plural(_all.Count)}";

    /// <summary>Anything narrowing the list - shows Clear.</summary>
    public bool HasActiveFilters =>
        !ShowUp || !ShowDown || !ShowMaintenance || !ShowDisabled
        || SelectedType.Key is not null || SelectedLocation.Key is not null || SelectedGroup.Key is not null
        || !string.IsNullOrWhiteSpace(SearchText);

    /// <summary>A Type / Location / Group filter or a non-default sort - lights up the Filters button.</summary>
    public bool HasPanelFilters =>
        SelectedType.Key is not null || SelectedLocation.Key is not null || SelectedGroup.Key is not null
        || SelectedSort.Sort != DeviceSort.Name;

    /// <summary>The strip shows while browsing, not while searching.</summary>
    public bool ShowRecentlyViewed => RecentlyViewed.Count > 0 && string.IsNullOrWhiteSpace(SearchText);

    public bool PinningEnabled => _bookmarks.PinningEnabled;

    public string EmptyText => _all.Count == 0 ? "No devices." : "No devices match these filters.";

    /// <summary>Group membership and maintenance, loaded after the list itself - awaited by tests.</summary>
    internal Task Extras { get; private set; } = Task.CompletedTask;

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(ShowRecentlyViewed));
        WhenTypingPauses(ApplyFilter);
    }

    partial void OnShowUpChanged(bool value) => OnChipChanged();

    partial void OnShowDownChanged(bool value) => OnChipChanged();

    partial void OnShowMaintenanceChanged(bool value) => OnChipChanged();

    partial void OnShowDisabledChanged(bool value) => OnChipChanged();

    private void OnChipChanged()
    {
        if (!_resetting)
        {
            SaveChips();
        }

        ApplyFilter();
    }

    private void SaveChips()
    {
        if (_shortcut)
        {
            return;
        }

        _chips.Set("up", ShowUp);
        _chips.Set("down", ShowDown);
        _chips.Set("maintenance", ShowMaintenance);
        _chips.Set("disabled", ShowDisabled);
    }

    /// <summary>
    /// Back to the user's own chips after a shortcut, with the other filters
    /// cleared - the page calls this when the tab is opened without one.
    /// Nothing to do otherwise.
    /// </summary>
    public void ShowSavedFilter()
    {
        if (!_shortcut)
        {
            return;
        }

        _shortcut = false;
        _resetting = true;
        ShowUp = _chips.Get("up");
        ShowDown = _chips.Get("down");
        ShowMaintenance = _chips.Get("maintenance");
        ShowDisabled = _chips.Get("disabled");
        SelectedType = AllTypes;
        SelectedLocation = AllLocations;
        SelectedGroup = AllGroups;
        SearchText = string.Empty;
        _resetting = false;
        ApplyFilter();
    }

    // A picker whose choices are being refilled briefly sends back null;
    // treat that as "all" (or the default sort) rather than as a choice.
    partial void OnSelectedSortChanged(SortOption value)
    {
        if (value is null)
        {
            SelectedSort = SortOptions[0];
            return;
        }

        ApplyFilter();
    }

    partial void OnSelectedTypeChanged(FacetOption value)
    {
        if (value is null)
        {
            SelectedType = AllTypes;
            return;
        }

        ApplyFilter();
    }

    partial void OnSelectedLocationChanged(FacetOption value)
    {
        if (value is null)
        {
            SelectedLocation = AllLocations;
            return;
        }

        ApplyFilter();
    }

    partial void OnSelectedGroupChanged(FacetOption value)
    {
        if (value is null)
        {
            SelectedGroup = AllGroups;
            return;
        }

        ApplyFilter();
    }

    // A chip tapped on a shortcut makes what's showing the user's own.
    [RelayCommand]
    private void ToggleUp()
    {
        _shortcut = false;
        ShowUp = !ShowUp;
    }

    [RelayCommand]
    private void ToggleDown()
    {
        _shortcut = false;
        ShowDown = !ShowDown;
    }

    [RelayCommand]
    private void ToggleMaintenance()
    {
        _shortcut = false;
        ShowMaintenance = !ShowMaintenance;
    }

    [RelayCommand]
    private void ToggleDisabled()
    {
        _shortcut = false;
        ShowDisabled = !ShowDisabled;
    }

    [RelayCommand]
    private void ToggleFilterPanel() => IsFilterPanelOpen = !IsFilterPanelOpen;

    /// <summary>Everything back to showing all devices (sort stays as chosen).</summary>
    [RelayCommand]
    private void ClearFilters()
    {
        _shortcut = false;
        _resetting = true;
        ShowUp = ShowDown = ShowMaintenance = ShowDisabled = true;
        SelectedType = AllTypes;
        SelectedLocation = AllLocations;
        SelectedGroup = AllGroups;
        SearchText = string.Empty;
        _resetting = false;
        SaveChips();
        ApplyFilter();
    }

    /// <summary>
    /// Just one group's or location's devices - what Groups &amp; locations opens
    /// the list with. Every other filter goes back to all; the choice holds
    /// through the refresh that follows, and picks up its count once the
    /// facet's choices are (re)built.
    /// </summary>
    public void ShowOnly(string? group = null, string? location = null)
    {
        _shortcut = true;
        _resetting = true;
        ShowUp = ShowDown = ShowMaintenance = ShowDisabled = true;
        SelectedType = AllTypes;
        SelectedGroup = group is null ? AllGroups : GroupOptions.FirstOrDefault(o => o.Key == group) ?? new FacetOption(group, group);
        SelectedLocation = location is null
            ? AllLocations
            : LocationOptions.FirstOrDefault(o => string.Equals(o.Key, location, StringComparison.OrdinalIgnoreCase)) ?? new FacetOption(location, location);
        SearchText = string.Empty;
        IsFilterPanelOpen = true;
        _resetting = false;
        ApplyFilter();
    }

    /// <summary>
    /// Only devices in <paramref name="state"/> - just that state chip on,
    /// every other filter cleared - for the dashboard's device counts (#34).
    /// Disabled covers ignored devices too, as its chip does.
    /// </summary>
    public void ShowOnlyState(DeviceState state)
    {
        _shortcut = true;
        _resetting = true;
        ShowUp = state == DeviceState.Up;
        ShowDown = state == DeviceState.Down;
        ShowMaintenance = state == DeviceState.Maintenance;
        ShowDisabled = state is DeviceState.Disabled or DeviceState.Ignored;
        SelectedType = AllTypes;
        SelectedGroup = AllGroups;
        SelectedLocation = AllLocations;
        SearchText = string.Empty;
        _resetting = false;
        ApplyFilter();
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var devices = await _client.Devices.ListAsync();
        var style = _settings.Current.DeviceNameStyle;
        var pinned = _bookmarks.PinnedIds;

        _all = devices
            .Select(d => new DeviceItem(d, style)
            {
                IsPinned = pinned.Contains(d.DeviceId),
                IsUnderMaintenance = _maintenanceIds.Contains(d.DeviceId),
            })
            .ToList();
        MarkSelected();

        RebuildFacet(TypeOptions, AllTypes, _all.Select(d => d.Device.Type), TypeLabel, v => SelectedType = v, SelectedType);
        RebuildFacet(LocationOptions, AllLocations, _all.Select(d => d.Device.LocationName()), v => Blank(v), v => SelectedLocation = v, SelectedLocation);
        ApplyFilter();

        // The list is up; the slower extras fill in behind it.
        Extras = LoadExtrasAsync();
    });

    /// <summary>
    /// Highlights the device whose detail is showing beside the list (#88),
    /// or none. Kept across refreshes, which make new rows.
    /// </summary>
    public void Select(int? deviceId)
    {
        _selectedId = deviceId;
        MarkSelected();
    }

    private void MarkSelected()
    {
        foreach (var item in _all)
        {
            item.IsSelected = item.DeviceId == _selectedId;
            item.IsTicked = Selection.Contains(item.DeviceId);
        }
    }

    [RelayCommand]
    private Task OpenDeviceAsync(DeviceItem? item)
    {
        if (item is null)
        {
            return Task.CompletedTask;
        }

        // Selecting: a tap ticks rather than opens.
        if (Selection.IsSelecting)
        {
            Selection.Toggle(item.DeviceId);
            return Task.CompletedTask;
        }

        return OpenAsync(item.DeviceId);
    }

    [RelayCommand]
    private void StartSelecting() => Selection.Start();

    [RelayCommand]
    private void StopSelecting() => Selection.Stop();

    [RelayCommand]
    private void SelectAll() => Selection.ToggleAll(ShownIds());

    /// <summary>Pins every ticked device - on this phone only, as one pin is.</summary>
    [RelayCommand]
    private void PinSelected() => SetPinnedSelected(pinned: true);

    [RelayCommand]
    private void UnpinSelected() => SetPinnedSelected(pinned: false);

    private void SetPinnedSelected(bool pinned)
    {
        if (!PinningEnabled)
        {
            return;
        }

        var items = Ticked().Where(d => d.IsPinned != pinned).ToList();
        foreach (var item in items)
        {
            _bookmarks.SetPinned(item.DeviceId, item.Name, pinned);
        }

        Selection.Stop();
        Selection.Report(items.Count == 0
            ? (pinned ? "They're all pinned already." : "None of them are pinned.")
            : new BulkResult<DeviceItem>(items, []).Describe(pinned ? "Pinned" : "Unpinned", "device"));
    }

    /// <summary>Asks LibreNMS to rediscover every ticked device, after asking once.</summary>
    [RelayCommand]
    private async Task RediscoverSelectedAsync()
    {
        var items = Ticked();
        if (items.Count == 0)
        {
            return;
        }

        var confirmed = _dialogs is null || await _dialogs.ConfirmAsync(
            "Rediscover devices",
            items.Count == 1 ? "Ask LibreNMS to rediscover 1 device?" : $"Ask LibreNMS to rediscover {items.Count} devices?",
            "Rediscover",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        await Selection.RunAsync(items, "Rediscovering", "Rediscovery requested for", d => d.DeviceId, d => d.Name, d => _client.Devices.DiscoverAsync(d.DeviceId));
    }

    /// <summary>One maintenance window for every ticked device: the form, with them all named.</summary>
    [RelayCommand]
    private async Task ScheduleMaintenanceSelectedAsync()
    {
        var devices = Ticked().Select(d => (d.DeviceId, d.Name)).ToList();
        if (devices.Count == 0)
        {
            return;
        }

        Selection.Stop();
        await _navigation.GoToAsync(Routes.Maintenance, new Dictionary<string, object> { [Routes.DevicesParameter] = devices });
    }

    /// <summary>The ticked devices still listed.</summary>
    private List<DeviceItem> Ticked() => _all.Where(d => Selection.Contains(d.DeviceId)).ToList();

    private List<int> ShownIds() => Devices.Select(d => d.DeviceId).ToList();

    [RelayCommand]
    private Task OpenRecentAsync(RecentlyViewedDevice? recent) => recent is null ? Task.CompletedTask : OpenAsync(recent.DeviceId);

    [RelayCommand]
    private void TogglePin(DeviceItem? item)
    {
        if (item is null || !PinningEnabled)
        {
            return;
        }

        _bookmarks.SetPinned(item.DeviceId, item.Name, !item.IsPinned);
    }

    private Task OpenAsync(int deviceId) =>
        _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = deviceId });

    private async Task LoadExtrasAsync()
    {
        await Task.WhenAll(LoadGroupsAsync(), ScanMaintenanceAsync());
        UpdateCounts();
        ApplyFilter();
    }

    private async Task LoadGroupsAsync()
    {
        try
        {
            _groups = await _client.DeviceGroups.GetMembershipByDeviceAsync();
        }
        catch (Exception)
        {
            // Groups are a nicety: without them the Group filter just stays at "All".
            return;
        }

        var keys = _all.SelectMany(d => GroupsOrNone(d.DeviceId));
        RebuildFacet(GroupOptions, AllGroups, keys, k => k == NoGroupKey ? "Not in a group" : k, v => SelectedGroup = v, SelectedGroup);
    }

    private async Task ScanMaintenanceAsync()
    {
        _maintenanceIds = await _maintenance.ScanAsync(_all.Select(d => d.Device).ToList());
        ApplyMaintenance();
    }

    private void ApplyMaintenance()
    {
        foreach (var device in _all)
        {
            device.IsUnderMaintenance = _maintenanceIds.Contains(device.DeviceId);
        }
    }

    private void OnBookmarksChanged()
    {
        var pinned = _bookmarks.PinnedIds;
        foreach (var device in _all)
        {
            device.IsPinned = pinned.Contains(device.DeviceId);
        }

        RebuildRecent();
        ApplyFilter();
    }

    private void RebuildRecent()
    {
        RecentlyViewed.ReplaceAll(_bookmarks.RecentlyViewed);

        OnPropertyChanged(nameof(ShowRecentlyViewed));
    }

    private bool Allows(DeviceItem device)
    {
        var stateAllowed = device.State switch
        {
            DeviceState.Up => ShowUp,
            DeviceState.Down => ShowDown,
            DeviceState.Maintenance => ShowMaintenance,
            _ => ShowDisabled,
        };

        if (!stateAllowed
            || (SelectedType.Key is { } type && !string.Equals(device.Device.Type ?? string.Empty, type, StringComparison.OrdinalIgnoreCase))
            || (SelectedLocation.Key is { } location && !string.Equals(device.Device.LocationName() ?? string.Empty, location, StringComparison.Ordinal))
            || (SelectedGroup.Key is { } group && !GroupsOrNone(device.DeviceId).Contains(group)))
        {
            return false;
        }

        var term = SearchText.Trim();
        return term.Length == 0 || device.Matches(term);
    }

    private void ApplyFilter()
    {
        if (_resetting)
        {
            return;
        }

        Devices.ReplaceAll(_all.Where(Allows).OrderByDescending(d => d.IsPinned).ThenBy(d => d, Comparer));
        OnPropertyChanged(nameof(SelectAllText));

        UpdateCounts();
    }

    private void UpdateCounts()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(HasPanelFilters));
        OnPropertyChanged(nameof(UpCount));
        OnPropertyChanged(nameof(DownCount));
        OnPropertyChanged(nameof(MaintenanceCount));
        OnPropertyChanged(nameof(DisabledCount));
    }

    private IEnumerable<string> GroupsOrNone(int deviceId) =>
        _groups.TryGetValue(deviceId, out var names) && names.Count > 0 ? names : [NoGroupKey];

    private IComparer<DeviceItem> Comparer => SelectedSort.Sort switch
    {
        DeviceSort.Status => By(d => StatusRank(d.State)),
        DeviceSort.IpAddress => Comparer<DeviceItem>.Create(CompareIp),
        DeviceSort.Uptime => By(d => d.Device.State == DeviceState.Up ? d.Device.Uptime : long.MaxValue),
        DeviceSort.Location => ByText(d => d.Device.LocationName()),
        DeviceSort.Os => ByText(d => d.Device.Os),
        DeviceSort.Hardware => ByText(d => d.Device.Hardware),
        _ => Comparer<DeviceItem>.Create((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name)),
    };

    /// <summary>Sorts by <paramref name="key"/>, then by name.</summary>
    private static IComparer<DeviceItem> By<T>(Func<DeviceItem, T> key) => Comparer<DeviceItem>.Create((a, b) =>
    {
        var byKey = Comparer<T>.Default.Compare(key(a), key(b));
        return byKey != 0 ? byKey : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
    });

    /// <summary>Text, with blanks last rather than first.</summary>
    private static IComparer<DeviceItem> ByText(Func<DeviceItem, string?> key) => Comparer<DeviceItem>.Create((a, b) =>
    {
        var x = key(a);
        var y = key(b);
        var blanks = string.IsNullOrWhiteSpace(x).CompareTo(string.IsNullOrWhiteSpace(y));
        if (blanks != 0)
        {
            return blanks;
        }

        var byKey = StringComparer.OrdinalIgnoreCase.Compare(x, y);
        return byKey != 0 ? byKey : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
    });

    /// <summary>Down first - the ones that need you - then maintenance, up, disabled, ignored.</summary>
    private static int StatusRank(DeviceState state) => state switch
    {
        DeviceState.Down => 0,
        DeviceState.Maintenance => 1,
        DeviceState.Up => 2,
        DeviceState.Disabled => 3,
        _ => 4,
    };

    /// <summary>Numerically (10.0.0.9 before 10.0.0.10), IPv4 before IPv6, anything unparseable last.</summary>
    private static int CompareIp(DeviceItem a, DeviceItem b)
    {
        var x = IpKey(a.Device.Ip);
        var y = IpKey(b.Device.Ip);

        if (x is null || y is null)
        {
            var missing = (x is null).CompareTo(y is null);
            return missing != 0 ? missing : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
        }

        var length = x.Length.CompareTo(y.Length);
        if (length != 0)
        {
            return length;
        }

        for (var i = 0; i < x.Length; i++)
        {
            var part = x[i].CompareTo(y[i]);
            if (part != 0)
            {
                return part;
            }
        }

        return StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
    }

    private static byte[]? IpKey(string? ip) =>
        IPAddress.TryParse(ip, out var address) ? address.GetAddressBytes() : null;

    /// <summary>
    /// Refills a facet's choices ("Router (12)"), most common first, keeping
    /// the current choice if it's still there.
    /// </summary>
    private static void RebuildFacet(
        BulkObservableCollection<FacetOption> options,
        FacetOption all,
        IEnumerable<string?> values,
        Func<string, string> label,
        Action<FacetOption> select,
        FacetOption current)
    {
        var counted = values
            .Select(v => v ?? string.Empty)
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => label(g.Key), StringComparer.OrdinalIgnoreCase)
            .Select(g => new FacetOption(g.Key, $"{label(g.Key)} ({g.Count()})"))
            .ToList();

        options.ReplaceAll(counted.Prepend(all));

        // The same choice, with its refreshed count - or back to all if it's gone.
        select(current.Key is null
            ? all
            : counted.FirstOrDefault(o => string.Equals(o.Key, current.Key, StringComparison.OrdinalIgnoreCase)) ?? all);
    }

    /// <summary>As desktop: "Unspecified" for a blank type, capitalised otherwise.</summary>
    private static string TypeLabel(string type) =>
        string.IsNullOrWhiteSpace(type) ? "Unspecified" : char.ToUpperInvariant(type[0]) + type[1..];

    private static string Blank(string value) => string.IsNullOrWhiteSpace(value) ? "Unspecified" : value;

    private static string Plural(int count) => count == 1 ? "device" : "devices";
}
