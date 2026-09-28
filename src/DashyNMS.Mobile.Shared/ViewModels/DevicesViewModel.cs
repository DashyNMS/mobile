using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

public enum DeviceFilter
{
    All,
    Down,
    Up,
}

/// <summary>Every device, searchable, down devices first.</summary>
public sealed partial class DevicesViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly INavigationService _navigation;
    private IReadOnlyList<DeviceItem> _all = Array.Empty<DeviceItem>();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private DeviceFilter _filter = DeviceFilter.All;

    public DevicesViewModel(ILibreNmsClient client, INavigationService navigation)
    {
        _client = client;
        _navigation = navigation;
    }

    public ObservableCollection<DeviceItem> Devices { get; } = new();

    public IReadOnlyList<DeviceFilter> Filters { get; } = Enum.GetValues<DeviceFilter>();

    /// <summary>"12 of 340 devices".</summary>
    public string CountText => Devices.Count == _all.Count
        ? $"{_all.Count} devices"
        : $"{Devices.Count} of {_all.Count} devices";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnFilterChanged(DeviceFilter value) => ApplyFilter();

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var devices = await _client.Devices.ListAsync();
        _all = devices
            .Select(d => new DeviceItem(d))
            .OrderBy(d => d.State == DeviceState.Down ? 0 : 1)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ApplyFilter();
    });

    [RelayCommand]
    private Task OpenDeviceAsync(DeviceItem? item) => item is null
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = item.DeviceId });

    private void ApplyFilter()
    {
        var text = SearchText.Trim();
        var shown = _all.Where(d => Filter switch
            {
                DeviceFilter.Down => d.State == DeviceState.Down,
                DeviceFilter.Up => d.State == DeviceState.Up,
                _ => true,
            })
            .Where(d => text.Length == 0 || d.Matches(text));

        Devices.Clear();
        foreach (var item in shown)
        {
            Devices.Add(item);
        }

        OnPropertyChanged(nameof(CountText));
    }
}
