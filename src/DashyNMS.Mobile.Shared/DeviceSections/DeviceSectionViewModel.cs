using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopNMS.Core.Api;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.DeviceSections;

/// <summary>One Device View section (sensors, ports, ARP...): grouped rows with search.</summary>
public sealed partial class DeviceSectionViewModel : ViewModelBase, IRefreshable
{
    private readonly DeviceSectionLoader _loader;
    private readonly INavigationService _navigation;
    private IReadOnlyList<SectionGroup> _all = [];
    private bool _loaded;
    private readonly HashSet<long> _collapsedInventory = [];

    public bool IsNeighbours => Section == DeviceSection.Neighbours;
    public string GraphDeviceName => _deviceName ?? $"Device {DeviceId}";

    public bool IsInventory => Section == DeviceSection.Inventory;

    [ObservableProperty]
    private bool _showInventoryPorts;

    partial void OnShowInventoryPortsChanged(bool value) => ApplyFilter();

    public string ShowInventoryPortsText => $"Show ports ({_all.SelectMany(g => g).Count(r => r.IsInventoryPort)})";
    public string InventorySummary => string.Join(" · ", _all.SelectMany(g => g)
        .GroupBy(r => r.Value ?? "Unknown").OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}"));

    [RelayCommand]
    private void ExpandInventory()
    {
        _collapsedInventory.Clear();
        ApplyFilter();
    }

    [RelayCommand]
    private void CollapseInventory()
    {
        foreach (var row in _all.SelectMany(g => g))
        {
            if (row.InventoryIndex is { } index) _collapsedInventory.Add(index);
        }
        ApplyFilter();
    }

    [RelayCommand]
    private void ToggleInventory(SectionRow? row)
    {
        if (row?.InventoryIndex is not { } index || !row.CanExpand) return;
        if (!_collapsedInventory.Add(index)) _collapsedInventory.Remove(index);
        ApplyFilter();
    }
    private string? _deviceName;

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Whether the search field is down (#349): opened from the glass button
    /// by the tab bar, or the header's icon where there's no glass, as on
    /// Devices and Alerts. Every section has it, however short.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFiltersText))]
    private bool _isSearchOpen;

    /// <summary>
    /// Whether Ports' Sort &amp; filter card is up (#350): its sort and
    /// grouping, opened from the glass button by the tab bar (or the
    /// header's icon) as on the Devices tab. Search and the card are one at
    /// a time.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFiltersText))]
    private bool _isFilterPanelOpen;

    partial void OnIsSearchOpenChanged(bool value)
    {
        if (value)
        {
            IsFilterPanelOpen = false;
        }
    }

    partial void OnIsFilterPanelOpenChanged(bool value)
    {
        if (value)
        {
            IsSearchOpen = false;
        }
    }

    /// <summary>Ports sorted or grouped other than by port ID: the glass Sort &amp; filter icon shows it filled.</summary>
    public bool HasPanelFilters => IsPorts
        && (SelectedPortSort.Sort != PortArrangement.SortOptions[0].Sort || SelectedPortGrouping.Grouping != PortArrangement.GroupingOptions[0].Grouping);

    /// <summary>
    /// Once search and the card are put away, what narrows or orders the list:
    /// Matching "ge-0" · Sorted by Speed (fastest first) · Grouped by VLAN - so
    /// a short list always says why.
    /// </summary>
    public string FiltersText
    {
        get
        {
            var parts = new List<string>();
            if (SearchText.Trim() is { Length: > 0 } term)
            {
                parts.Add($"Matching \u201c{term}\u201d");
            }

            if (IsPorts && SelectedPortSort.Sort != PortArrangement.SortOptions[0].Sort)
            {
                parts.Add("Sorted by " + SelectedPortSort.Label);
            }

            if (IsPorts && SelectedPortGrouping.Grouping != PortArrangement.GroupingOptions[0].Grouping)
            {
                parts.Add("Grouped " + char.ToLowerInvariant(SelectedPortGrouping.Label[0]) + SelectedPortGrouping.Label[1..]);
            }

            return string.Join(" \u00b7 ", parts);
        }
    }

    public bool HasFiltersText => !IsSearchOpen && !IsFilterPanelOpen && FiltersText.Length > 0;

    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>The Ports page's order (#92): by port ID until the user picks otherwise, then remembered.</summary>
    [ObservableProperty]
    private PortSortOption _selectedPortSort;

    [ObservableProperty]
    private PortGroupingOption _selectedPortGrouping;

    private readonly IAppPreferences _preferences;

    public DeviceSectionViewModel(DeviceSectionLoader loader, INavigationService navigation, IAppPreferences? preferences = null)
    {
        _loader = loader;
        _navigation = navigation;
        _preferences = preferences ?? new InMemoryPreferences();
        _selectedPortSort = PortArrangement.SortOptions.FirstOrDefault(o => o.Sort.ToString() == _preferences.Get(PortSortKey))
            ?? PortArrangement.SortOptions[0];
        _selectedPortGrouping = PortArrangement.GroupingOptions.FirstOrDefault(o => o.Grouping.ToString() == _preferences.Get(PortGroupingKey))
            ?? PortArrangement.GroupingOptions[0];
    }

    private const string PortSortKey = "ports.sort";
    private const string PortGroupingKey = "ports.grouping";

    public IReadOnlyList<PortSortOption> PortSortOptions => PortArrangement.SortOptions;

    public IReadOnlyList<PortGroupingOption> PortGroupingOptions => PortArrangement.GroupingOptions;

    /// <summary>The Ports page has its sort and group choices; other sections keep their own order.</summary>
    public bool IsPorts => Section == DeviceSection.Ports;

    partial void OnSelectedPortSortChanged(PortSortOption value)
    {
        // A picker whose choices are being refilled briefly sends back null.
        if (value is null)
        {
            SelectedPortSort = PortArrangement.SortOptions[0];
            return;
        }

        _preferences.Set(PortSortKey, value.Sort.ToString());
        OnFiltersChanged();
        ApplyFilter();
    }

    partial void OnSelectedPortGroupingChanged(PortGroupingOption value)
    {
        if (value is null)
        {
            SelectedPortGrouping = PortArrangement.GroupingOptions[0];
            return;
        }

        _preferences.Set(PortGroupingKey, value.Grouping.ToString());
        OnFiltersChanged();
        ApplyFilter();
    }

    public int DeviceId { get; private set; }

    public DeviceSection Section { get; private set; }

    public BulkObservableCollection<SectionGroup> Groups { get; } = new();

    /// <summary>What the search button and field say: "Search sensors", but "Search ARP" and "Search VLANs".</summary>
    public string SearchLabel
    {
        get
        {
            var title = DeviceSectionInfo.For(Section).Title;
            return "Search " + (title.Length > 1 && char.IsLower(title[1]) ? char.ToLowerInvariant(title[0]) + title[1..] : title);
        }
    }

    public bool IsEmpty => !HasError && _loaded && Groups.Count == 0 && !IsBusy;

    public string EmptyText => FailureOutcome is { } failure ? VisibleTotals.Describe(failure, 0, DeviceSectionInfo.For(Section).Title.ToLowerInvariant()) : _all.Count == 0
        ? VisibleTotals.Describe(SourceOutcome.Empty, 0, DeviceSectionInfo.For(Section).Title.ToLowerInvariant())
        : "Nothing matches.";

    partial void OnSearchTextChanged(string value)
    {
        OnFiltersChanged();
        WhenTypingPauses(ApplyFilter);
    }

    private void OnFiltersChanged()
    {
        OnPropertyChanged(nameof(HasPanelFilters));
        OnPropertyChanged(nameof(FiltersText));
        OnPropertyChanged(nameof(HasFiltersText));
    }

    public Task LoadAsync(int deviceId, DeviceSection section, string? deviceName = null)
    {
        if (DeviceId != deviceId || Section != section)
        {
            _collapsedInventory.Clear();
            ShowInventoryPorts = false;
            _all = [];
            Groups.ReplaceAll([]);
            _loaded = false;
        }
        DeviceId = deviceId;
        Section = section;
        OnPropertyChanged(nameof(IsPorts));
        OnPropertyChanged(nameof(IsInventory));
        OnPropertyChanged(nameof(IsNeighbours));
        OnPropertyChanged(nameof(SearchLabel));
        OnFiltersChanged();
        _deviceName = deviceName;
        OnPropertyChanged(nameof(GraphDeviceName));
        var info = DeviceSectionInfo.For(section);
        Title = deviceName is null ? info.Title : $"{info.Title} · {deviceName}";
        return RefreshAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        _all = await _loader.LoadAsync(Section, DeviceId);
        _loaded = true;
        OnPropertyChanged(nameof(InventorySummary));
        OnPropertyChanged(nameof(ShowInventoryPortsText));
        ApplyFilter();

        // Kept readings after a slow sensor list say how old they are.
        if (Section == DeviceSection.Sensors)
        {
            ErrorMessage = _loader.SensorStatus;
        }
    });

    /// <summary>The header's search icon, where there's no glass: opens search, or closes it and clears it.</summary>
    [RelayCommand]
    private void ToggleSearch()
    {
        if (IsSearchOpen)
        {
            SearchText = string.Empty;
            IsSearchOpen = false;
        }
        else
        {
            IsSearchOpen = true;
        }
    }

    /// <summary>The header's Sort &amp; filter icon and the card's Done, where there's no glass, and the glass button.</summary>
    [RelayCommand]
    private void ToggleFilterPanel() => IsFilterPanelOpen = !IsFilterPanelOpen;

    /// <summary>
    /// Done searching - the keyboard's Search key, the keyboard put away, or
    /// the list scrolled: the field goes, what was typed stays.
    /// </summary>
    public void DismissSearch() => IsSearchOpen = false;

    /// <summary>A neighbour that's a LibreNMS device opens that device; a port opens its graphs.</summary>
    [RelayCommand]
    private Task OpenLinkAsync(SectionRow? row)
    {
        if (row?.LinkDeviceId is { } id)
        {
            return _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = id });
        }

        if (row?.LinkPortIfName is { } ifName)
        {
            return _navigation.GoToAsync(Routes.DeviceGraphs, new Dictionary<string, object>
            {
                [Routes.DeviceIdParameter] = DeviceId,
                [Routes.PortParameter] = ifName,
                [Routes.PortNameParameter] = row.Title,
                [Routes.DeviceNameParameter] = _deviceName ?? string.Empty,
            });
        }

        return Task.CompletedTask;
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();

        if (IsInventory)
        {
            Groups.ReplaceAll(_all.Select(g => new SectionGroup(g.Name,
                InventoryRows.Visible(g.ToList(), _collapsedInventory, ShowInventoryPorts, term)))
                .Where(g => g.Count > 0));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyText));
            return;
        }

        // Ports in the order and groups chosen; everything else as loaded.
        var source = IsPorts
            ? PortArrangement.Arrange(_all.SelectMany(g => g), SelectedPortSort.Sort, SelectedPortGrouping.Grouping)
            : _all;

        Groups.ReplaceAll(term.Length == 0
            ? source
            : source
                .Select(group => new SectionGroup(group.Name, group.Where(r => r.Matches(term))))
                .Where(group => group.Count > 0)
                .ToList());

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }
}
