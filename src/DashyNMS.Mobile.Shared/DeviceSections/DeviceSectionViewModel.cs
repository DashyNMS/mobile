using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.DeviceSections;

/// <summary>One Device View section (sensors, ports, ARP...): grouped rows with search.</summary>
public sealed partial class DeviceSectionViewModel : ViewModelBase
{
    private readonly DeviceSectionLoader _loader;
    private readonly INavigationService _navigation;
    private IReadOnlyList<SectionGroup> _all = [];
    private bool _loaded;
    private string? _deviceName;

    [ObservableProperty]
    private string _searchText = string.Empty;

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
        ApplyFilter();
    }

    public int DeviceId { get; private set; }

    public DeviceSection Section { get; private set; }

    public BulkObservableCollection<SectionGroup> Groups { get; } = new();

    /// <summary>Long lists (ports, FDB, ARP) get a search box; short ones don't need it.</summary>
    public bool ShowSearch => _all.Sum(g => g.Count) > 12;

    public bool IsEmpty => _loaded && Groups.Count == 0 && !IsBusy;

    public string EmptyText => _all.Count == 0
        ? $"LibreNMS has no {DeviceSectionInfo.For(Section).Title.ToLowerInvariant()} for this device."
        : "Nothing matches.";

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(ApplyFilter);

    public Task LoadAsync(int deviceId, DeviceSection section, string? deviceName = null)
    {
        DeviceId = deviceId;
        Section = section;
        OnPropertyChanged(nameof(IsPorts));
        _deviceName = deviceName;
        var info = DeviceSectionInfo.For(section);
        Title = deviceName is null ? info.Title : $"{info.Title} · {deviceName}";
        return RefreshAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        _all = await _loader.LoadAsync(Section, DeviceId);
        _loaded = true;
        ApplyFilter();
        OnPropertyChanged(nameof(ShowSearch));
    });

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
