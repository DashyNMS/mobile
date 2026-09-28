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

    public DeviceSectionViewModel(DeviceSectionLoader loader, INavigationService navigation)
    {
        _loader = loader;
        _navigation = navigation;
    }

    public int DeviceId { get; private set; }

    public DeviceSection Section { get; private set; }

    public ObservableCollection<SectionGroup> Groups { get; } = new();

    /// <summary>Long lists (ports, FDB, ARP) get a search box; short ones don't need it.</summary>
    public bool ShowSearch => _all.Sum(g => g.Count) > 12;

    public bool IsEmpty => _loaded && Groups.Count == 0 && !IsBusy;

    public string EmptyText => _all.Count == 0
        ? $"LibreNMS has no {DeviceSectionInfo.For(Section).Title.ToLowerInvariant()} for this device."
        : "Nothing matches.";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    public Task LoadAsync(int deviceId, DeviceSection section, string? deviceName = null)
    {
        DeviceId = deviceId;
        Section = section;
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
        Groups.Clear();
        foreach (var group in _all)
        {
            if (term.Length == 0)
            {
                Groups.Add(group);
                continue;
            }

            var rows = group.Where(r => r.Matches(term)).ToList();
            if (rows.Count > 0)
            {
                Groups.Add(new SectionGroup(group.Name, rows));
            }
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }
}
