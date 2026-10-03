using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Graylog;

/// <summary>
/// The Graylog page's Device chip's chooser (#117). A network has too many
/// devices to scroll through, so before anything's typed it suggests the
/// likely ones - those sending the messages on screen, busiest first, then
/// pinned and recently viewed devices. Typing searches every device, as the
/// Devices tab does; an address no device has can still be searched for.
/// </summary>
public sealed partial class GraylogDevicePickerViewModel : ViewModelBase
{
    /// <summary>Enough to find one by eye; past that, type more of its name.</summary>
    internal const int MaxShown = 50;

    /// <summary>The busiest senders on screen - the rest are a search away.</summary>
    internal const int MaxSenders = 10;

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly DeviceBookmarks _bookmarks;
    private readonly INavigationService _navigation;
    private GraylogViewModel? _list;
    private IReadOnlyList<DeviceItem> _devices = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    public GraylogDevicePickerViewModel(ILibreNmsClient client, ISettingsStore settings, DeviceBookmarks bookmarks, INavigationService navigation)
    {
        _client = client;
        _settings = settings;
        _bookmarks = bookmarks;
        _navigation = navigation;
    }

    public BulkObservableCollection<GraylogDeviceGroup> Groups { get; } = new();

    /// <summary>"Any device" at the top - only once there's a device to go back from.</summary>
    public bool CanChooseAny => _list?.HasDeviceFilter == true;

    public bool IsEmpty => !IsBusy && Groups.Count == 0;

    public string EmptyText => string.IsNullOrWhiteSpace(SearchText)
        ? "No devices to suggest yet. Search for one by name, hostname or IP."
        : "No device matches. Search by name, hostname or IP.";

    /// <summary>The Graylog list whose Device chip this sets.</summary>
    public async Task LoadAsync(GraylogViewModel list)
    {
        _list = list;
        OnPropertyChanged(nameof(CanChooseAny));

        var style = _settings.Current.DeviceNameStyle;
        var devices = list.KnownDevices;
        if (devices.Count == 0)
        {
            await RunAsync(async () => devices = await _client.Devices.ListAsync());
        }

        _devices = devices
            .Select(d => new DeviceItem(d, style))
            .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        Filter();
    }

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(Filter);

    [RelayCommand]
    private Task ChooseAsync(GraylogDeviceChoice? choice) => choice is null ? Task.CompletedTask : SetAsync(choice.Filter);

    [RelayCommand]
    private Task ChooseAnyAsync() => SetAsync(null);

    private async Task SetAsync(GraylogDeviceFilter? filter)
    {
        _list?.ShowDevice(filter);
        await _navigation.GoToAsync(Routes.Back);
    }

    private void Filter()
    {
        var term = SearchText.Trim();
        Groups.ReplaceAll(term.Length == 0 ? Suggestions() : Matches(term));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    private IEnumerable<GraylogDeviceGroup> Suggestions()
    {
        var byId = _devices.ToDictionary(d => d.DeviceId);
        var groups = new List<GraylogDeviceGroup>();

        var senders = (_list?.Senders() ?? [])
            .Take(MaxSenders)
            .Select(s => s.Filter.DeviceId is { } id && byId.TryGetValue(id, out var device)
                ? Choice(device, s.Count)
                : new GraylogDeviceChoice(s.Filter, "Not in LibreNMS", RowStatus.Inactive, s.Count))
            .ToList();
        Add(groups, "In these messages", senders);

        var pinned = _bookmarks.PinnedIds;
        Add(groups, "Pinned", _devices.Where(d => pinned.Contains(d.DeviceId)).Select(d => Choice(d)));

        Add(groups, "Recently viewed", _bookmarks.RecentlyViewed
            .Where(r => !pinned.Contains(r.DeviceId) && byId.ContainsKey(r.DeviceId))
            .Select(r => Choice(byId[r.DeviceId])));

        // Nothing to go on yet: the first devices, so the page isn't blank.
        if (groups.Count == 0)
        {
            Add(groups, "Devices", _devices.Take(MaxShown).Select(d => Choice(d)));
        }

        return groups;
    }

    private IEnumerable<GraylogDeviceGroup> Matches(string term)
    {
        var groups = new List<GraylogDeviceGroup>();
        var matches = _devices.Where(d => d.Matches(term)).ToList();
        Add(groups, "Devices", matches.Take(MaxShown).Select(d => Choice(d)));

        // Syslog from something LibreNMS doesn't poll - search its address as typed.
        if (LooksLikeAddress(term) && !matches.Any(d => SameAddress(d.Device, term)))
        {
            Add(groups, "Not in LibreNMS", [new GraylogDeviceChoice(GraylogDeviceFilter.ForAddress(term), "Search Graylog for this address", RowStatus.Inactive)]);
        }

        return groups;
    }

    private static GraylogDeviceChoice Choice(DeviceItem device, int count = 0) =>
        new(GraylogDeviceFilter.ForDevice(device.DeviceId, device.Name), device.Details, device.State, count);

    private static void Add(List<GraylogDeviceGroup> groups, string name, IEnumerable<GraylogDeviceChoice> choices)
    {
        var group = new GraylogDeviceGroup(name, choices);
        if (group.Count > 0)
        {
            groups.Add(group);
        }
    }

    /// <summary>An IP address or a hostname - one word with a dot or colon in it.</summary>
    internal static bool LooksLikeAddress(string term) =>
        IPAddress.TryParse(term, out _)
        || (Uri.CheckHostName(term) == UriHostNameType.Dns && term.Contains('.', StringComparison.Ordinal));

    private static bool SameAddress(Device device, string term) =>
        new[] { device.Ip, device.Hostname, device.SysName }.Any(a => string.Equals(a?.Trim(), term, StringComparison.OrdinalIgnoreCase));
}
