using System.Globalization;
using System.Net;
using System.Net.Sockets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Graylog;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Graylog;

/// <summary>
/// Graylog messages, found the way LibreNMS's Graylog pages find them (see
/// <see cref="GraylogQuery"/>): by time range, stream, level and message
/// text, newest first. Backs two pages, as desktop's view model does:
/// <list type="bullet">
/// <item>Alerts, Logs, Graylog - every device's messages, like LibreNMS's Overview, Graylog.</item>
/// <item>A device's Graylog section (<see cref="Initialise"/> with a device) - only messages from its addresses, like LibreNMS's device Graylog tab.</item>
/// </list>
/// </summary>
/// <remarks>
/// Pages are appended on "Load more" rather than numbered, as the other
/// mobile lists do. Desktop's auto-update isn't here - pull to refresh.
/// </remarks>
public sealed partial class GraylogViewModel : ViewModelBase
{
    internal const int PageSize = 50;

    /// <summary>Far enough back for a phone; past this, desktop or Graylog itself.</summary>
    internal const int MaxMessages = 1000;

    /// <summary>Longest query sent - Graylog's search is a GET, and servers and proxies commonly cap a request line at 8 KB (desktop's limit).</summary>
    internal const int MaxQueryLength = 3500;

    private const string NewestFirst = "timestamp:desc";

    /// <summary>How long to wait for the hostname lookup LibreNMS does (gethostbyname) before carrying on without it.</summary>
    private static readonly TimeSpan HostnameLookupTimeout = TimeSpan.FromSeconds(3);

    private readonly GraylogSetup _setup;
    private readonly IGraylogApi _graylog;
    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly List<GraylogMessageItem> _loaded = new();
    private IReadOnlyList<GraylogStream> _streams = [];
    private IReadOnlyList<Device>? _devices;
    private Dictionary<string, (string Name, int? DeviceId)>? _byAddress;
    private IReadOnlyList<string>? _deviceAddresses;
    private CancellationTokenSource? _loadCts;
    private bool _hasLoaded;
    private bool _suppressReload;
    private long _totalResults;
    private string? _deviceName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotConfigured))]
    private bool _isConfigured = true;

    [ObservableProperty]
    private int _rangeIndex;

    [ObservableProperty]
    private int _levelIndex;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public GraylogViewModel(
        GraylogSetup setup,
        IGraylogApi graylog,
        ILibreNmsClient client,
        ISettingsStore settings,
        INavigationService navigation)
    {
        _setup = setup;
        _graylog = graylog;
        _client = client;
        _settings = settings;
        _navigation = navigation;

        // LibreNMS's fleet page has no level filter until one is picked.
        _levelIndex = 0;
    }

    /// <summary>The device this page is fixed to, or null for every device.</summary>
    public int? DeviceId { get; private set; }

    public bool IsDevice => DeviceId is not null;

    public string Title => _deviceName is { Length: > 0 } name ? $"Graylog · {name}" : "Graylog";

    public bool IsNotConfigured => !IsConfigured;

    public BulkObservableCollection<GraylogMessageItem> Messages { get; } = new();

    /// <summary>LibreNMS's time ranges, "All time" first.</summary>
    public IReadOnlyList<string> RangeLabels { get; } = GraylogQuery.Ranges.Select(r => r.Label).ToList();

    /// <summary>"Any level", then "(0) Emergency" to "(7) Debug" - each includes every more severe level.</summary>
    public IReadOnlyList<string> LevelLabels { get; } =
        new[] { "Any level" }.Concat(Enumerable.Range(0, 8).Select(l => GraylogQuery.LevelText(l))).ToList();

    /// <summary>"All streams", then each enabled stream the account can read.</summary>
    public IReadOnlyList<string> StreamLabels { get; private set; } = ["All streams"];

    public int StreamIndex
    {
        get => _streamIndex;
        set
        {
            // A Picker resets to -1 when its list is replaced; keep the choice.
            if (value < 0 || value >= StreamLabels.Count || !SetProperty(ref _streamIndex, value))
            {
                return;
            }

            ReloadFromStart();
        }
    }

    private int _streamIndex;

    public bool CanLoadMore => _hasLoaded && !IsBusy && _loaded.Count < _totalResults && _loaded.Count < MaxMessages;

    public bool IsEmpty => _hasLoaded && IsConfigured && !IsBusy && !HasError && Messages.Count == 0;

    public string EmptyText => IsDevice
        ? "No Graylog messages for this device match these filters."
        : "No Graylog messages match these filters.";

    /// <summary>"50 of 1,234 messages".</summary>
    public string SummaryText => !_hasLoaded || HasError ? string.Empty
        : _totalResults == 0 ? string.Empty
        : string.Create(CultureInfo.CurrentCulture, $"{_loaded.Count:N0} of {_totalResults:N0} {(_totalResults == 1 ? "message" : "messages")}");

    /// <summary>
    /// The device's addresses stand in for LibreNMS's gethostbyname - swapped
    /// in tests so they needn't wait on DNS.
    /// </summary>
    internal Func<string?, CancellationToken, Task<string?>> ResolveHostname { get; set; } = ResolveHostnameAsync;

    /// <summary>Every device's messages, or with <paramref name="deviceId"/> only that device's.</summary>
    public void Initialise(int? deviceId, string? deviceName)
    {
        if (DeviceId == deviceId && _deviceName == deviceName)
        {
            return;
        }

        DeviceId = deviceId;
        _deviceName = deviceName;
        _deviceAddresses = null;
        _hasLoaded = false;

        // A device starts from LibreNMS's device-page level; the fleet from any level.
        _suppressReload = true;
        LevelIndex = deviceId is null ? 0 : Math.Clamp(_settings.Current.Graylog.DeviceLogLevel, 0, 7) + 1;
        _suppressReload = false;

        OnPropertyChanged(nameof(IsDevice));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(EmptyText));
    }

    /// <summary>First load when the page shows; later visits keep what's there.</summary>
    public Task EnsureLoadedAsync() => _hasLoaded ? Task.CompletedTask : LoadAsync(append: false);

    partial void OnRangeIndexChanged(int value) => ReloadFromStart();

    partial void OnLevelIndexChanged(int value) => ReloadFromStart();

    /// <summary>Clearing the search box shows everything again without needing the search button.</summary>
    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ReloadFromStart();
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync(append: false);

    /// <summary>The search box's search button - not every keystroke, since each one is a Graylog query.</summary>
    [RelayCommand]
    private Task SearchAsync() => LoadAsync(append: false);

    [RelayCommand]
    private Task LoadMoreAsync() => CanLoadMore ? LoadAsync(append: true) : Task.CompletedTask;

    [RelayCommand]
    private void Toggle(GraylogMessageItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.IsExpanded = !item.IsExpanded;

        // CollectionView grows a cell whose content grows but doesn't shrink
        // it again; putting the item back makes it measure the cell afresh.
        var index = Messages.IndexOf(item);
        if (index >= 0)
        {
            Messages[index] = item;
        }
    }

    /// <summary>The device a message came from, when LibreNMS knows it.</summary>
    [RelayCommand]
    private Task OpenDeviceAsync(GraylogMessageItem? item) => item?.DeviceId is { } id && id != DeviceId
        ? _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = id })
        : Task.CompletedTask;

    [RelayCommand]
    private Task OpenSettingsAsync() => _navigation.GoToAsync(Routes.GraylogSettings);

    private void ReloadFromStart()
    {
        if (!_suppressReload && _hasLoaded)
        {
            _ = LoadAsync(append: false);
        }
    }

    private async Task LoadAsync(bool append)
    {
        // A newer search always wins over one still on its way.
        _loadCts?.Cancel();
        using var cts = new CancellationTokenSource();
        _loadCts = cts;

        await RunAsync(async () =>
        {
            IsConfigured = await _setup.EnsureConfiguredAsync();
            if (!IsConfigured)
            {
                _hasLoaded = true;
                Show([], 0, append: false);
                return;
            }

            var token = cts.Token;
            var firstLoad = _devices is null;
            _devices ??= await _client.Devices.ListAsync(token);
            _byAddress ??= AddressLookup(_devices, _settings.Current.DeviceNameStyle);

            if (firstLoad)
            {
                _ = LoadStreamsAsync();
            }

            IReadOnlyList<string>? addresses = null;
            if (DeviceId is { } deviceId)
            {
                addresses = _deviceAddresses ??= await DeviceAddressesAsync(deviceId, token);
                if (addresses.Count == 0)
                {
                    // Without an address the query would match every device's
                    // messages - never show those as this device's.
                    ErrorMessage = "LibreNMS has no address or name for this device to search Graylog for.";
                    _hasLoaded = true;
                    Show([], 0, append: false);
                    return;
                }
            }

            var options = _settings.Current.Graylog;
            var level = LevelIndex <= 0 ? (int?)null : LevelIndex - 1;
            var query = GraylogQuery.WithMaxLevel(GraylogQuery.BuildSimpleQuery(SearchText, options.QueryField, addresses?.ToList()), level);

            if (query.Length > MaxQueryLength)
            {
                ErrorMessage = "This device has too many addresses to search Graylog for in one go. Turning off \"Match any address\" in Settings, Graylog searches its main addresses only.";
                _hasLoaded = true;
                Show([], 0, append: false);
                return;
            }

            var range = GraylogQuery.Ranges[Math.Clamp(RangeIndex, 0, GraylogQuery.Ranges.Count - 1)].Seconds;
            var stream = StreamIndex > 0 && StreamIndex <= _streams.Count ? _streams[StreamIndex - 1].Id : null;

            var result = await _graylog.SearchAsync(
                query,
                range,
                PageSize,
                append ? _loaded.Count : 0,
                NewestFirst,
                GraylogQuery.StreamFilter(stream),
                token);

            token.ThrowIfCancellationRequested();

            var zone = GraylogQuery.FindTimeZone(options.Timezone);
            var items = result.Messages
                .Select(m => new GraylogMessageItem(m.Message, zone, DeviceFor))
                .ToList();

            _hasLoaded = true;
            Show(items, result.TotalResults, append);
        });

        if (ReferenceEquals(_loadCts, cts))
        {
            _loadCts = null;
        }

        RaiseListChanged();
    }

    private void Show(IReadOnlyList<GraylogMessageItem> items, long total, bool append)
    {
        if (!append)
        {
            _loaded.Clear();
        }

        _loaded.AddRange(items);
        _totalResults = total;
        Messages.ReplaceAll(_loaded.ToList());
        RaiseListChanged();
    }

    private void RaiseListChanged()
    {
        OnPropertyChanged(nameof(CanLoadMore));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SummaryText));
    }

    /// <summary>Not fatal - every stream can still be searched without the list.</summary>
    private async Task LoadStreamsAsync()
    {
        try
        {
            _streams = (await _graylog.GetStreamsAsync())
                .Where(s => !s.Disabled)
                .OrderBy(s => s.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (GraylogApiException)
        {
            return;
        }

        StreamLabels = new[] { "All streams" }.Concat(_streams.Select(s => s.DisplayText)).ToList();
        OnPropertyChanged(nameof(StreamLabels));

        // Replacing a Picker's list clears its selection; put it back.
        _streamIndex = 0;
        OnPropertyChanged(nameof(StreamIndex));
    }

    /// <summary>The device's addresses for the query field - see <see cref="GraylogQuery.DeviceAddresses"/>.</summary>
    private async Task<IReadOnlyList<string>> DeviceAddressesAsync(int deviceId, CancellationToken cancellationToken)
    {
        var device = _devices?.FirstOrDefault(d => d.DeviceId == deviceId)
                     ?? await _client.Devices.GetAsync(deviceId.ToString(CultureInfo.InvariantCulture), cancellationToken);
        if (device is null)
        {
            return [];
        }

        var resolved = await ResolveHostname(device.Hostname, cancellationToken);

        IReadOnlyList<DeviceIpAddress>? interfaceAddresses = null;
        if (_settings.Current.Graylog.MatchAnyAddress)
        {
            try
            {
                interfaceAddresses = await _client.Ports.ListIpAddressesAsync(deviceId, cancellationToken);
            }
            catch (LibreNmsApiException)
            {
                // Still worth searching by the main addresses alone.
            }
        }

        return GraylogQuery.DeviceAddresses(device, resolved, interfaceAddresses);
    }

    /// <summary>A message's source or origin as a known device's name, or the address itself.</summary>
    private (string Name, int? DeviceId) DeviceFor(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return (string.Empty, null);
        }

        return _byAddress is not null && _byAddress.TryGetValue(address.Trim(), out var known) ? known : (address, null);
    }

    /// <summary>Each device by its IP, hostname and sysName - the addresses syslog senders usually use.</summary>
    internal static Dictionary<string, (string Name, int? DeviceId)> AddressLookup(IEnumerable<Device> devices, DeviceNameStyle style)
    {
        var lookup = new Dictionary<string, (string Name, int? DeviceId)>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in devices)
        {
            var entry = (new DeviceItem(device, style).Name, (int?)device.DeviceId);
            foreach (var address in new[] { device.Ip, device.Hostname, device.SysName })
            {
                if (!string.IsNullOrWhiteSpace(address))
                {
                    lookup.TryAdd(address.Trim(), entry);
                }
            }
        }

        return lookup;
    }

    /// <summary>LibreNMS's <c>gethostbyname($device->hostname)</c> - the first IPv4 address, or null if it doesn't resolve in time.</summary>
    private static async Task<string?> ResolveHostnameAsync(string? hostname, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return null;
        }

        if (IPAddress.TryParse(hostname, out var literal))
        {
            return literal.AddressFamily == AddressFamily.InterNetwork ? literal.ToString() : null;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(HostnameLookupTimeout);

            var addresses = await Dns.GetHostAddressesAsync(hostname, AddressFamily.InterNetwork, timeout.Token);
            return addresses.FirstOrDefault()?.ToString();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
    }
}
