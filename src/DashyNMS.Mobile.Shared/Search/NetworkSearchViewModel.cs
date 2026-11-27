using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Search;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Search;

/// <summary>A choice of filter chip: a source, with how many results it holds.</summary>
public sealed partial class SearchSourceChip(string key, int count, Action changed) : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = true;

    public string Key => key;

    public int Count => count;

    public string Text => $"{Short(key)} {count.ToString("N0", CultureInfo.CurrentCulture)}";

    public string Description => $"{key}, {count.ToString("N0", CultureInfo.CurrentCulture)} results, {(IsSelected ? "shown" : "hidden")}";

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(Description));
        changed();
    }

    [RelayCommand]
    private void Toggle() => IsSelected = !IsSelected;

    private static string Short(string key) => key switch
    {
        EndpointRowViewModel.SourceNeighbourTables => "ARP",
        EndpointRowViewModel.SourceForwardingTables => "MAC tables",
        EndpointRowViewModel.SourceInterfaceAddresses => "Interfaces",
        _ => key,
    };
}

/// <summary>How a list of results is ordered.</summary>
public enum SearchSort
{
    Address,
    Name,
    Mac,
}

/// <summary>
/// Network Search on the phone (#129): one search that finds an endpoint by
/// address, network, MAC address or name and shows where it's connected, on
/// Core's lookup (core#91) - LibreNMS 26.11's Network Search page
/// (librenms#21113, librenms#21159), as Desktop's (desktop#57) lays it out.
/// </summary>
/// <remarks>
/// <para>
/// It leads with the port an endpoint most likely plugs into, keeps uplinks as
/// context, and says what it couldn't check: a source that was denied,
/// unavailable or failed is shown as such, never folded into "not found".
/// </para>
/// <para>
/// A search started while another runs cancels it. A result from a search the
/// user has moved on from, or from a connection that has since gone, is
/// dropped rather than shown. Rows and cards are built here, so the page only
/// draws them.
/// </para>
/// </remarks>
public sealed partial class NetworkSearchViewModel : ViewModelBase, INetworkSearchActions
{
    /// <summary>A phone reads one page of a subnet at a time; "+N more" opens the rest.</summary>
    private static readonly NetworkSearchOptions Options = new() { MaxSubnetInterfaces = 100, MaxNamedPorts = 120 };

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;
    private readonly IClipboardText? _clipboard;
    private readonly IShareService? _share;
    private readonly DesktopNMS.Core.Services.IDeviceCache? _devices;
    private readonly CorePolling? _polling;
    private readonly ILogger<NetworkSearchViewModel>? _logger;
    private readonly SynchronizationContext? _ui;
    private CancellationTokenSource? _searchCts;
    private int _generation;
    private NetworkSearchResult? _result;
    private object? _resultConnection;
    private MacAddressFormat _macFormat;
    private DeviceNameStyle _deviceNameStyle;
    private IReadOnlyList<EndpointRowViewModel> _allRows = [];

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(PreviousPageCommand))]
    private bool _isSearching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    private EndpointViewModel? _endpoint;

    [ObservableProperty]
    private SearchSort _sort;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourcesButtonText))]
    private bool _sourcesExpanded;

    public NetworkSearchViewModel(
        ILibreNmsClient client,
        ISettingsStore settings,
        ISessionService session,
        INavigationService navigation,
        IClipboardText? clipboard = null,
        IShareService? share = null,
        DesktopNMS.Core.Services.IDeviceCache? devices = null,
        ILogger<NetworkSearchViewModel>? logger = null,
        CorePolling? polling = null)
    {
        _client = client;
        _settings = settings;
        _session = session;
        _navigation = navigation;
        _clipboard = clipboard;
        _share = share;
        _devices = devices;
        _polling = polling;
        _logger = logger;
        _ui = SynchronizationContext.Current;
        _macFormat = settings.Current.NetworkSearchMacFormatValue;
        _deviceNameStyle = settings.Current.DeviceNameStyle;
        Recent = new ObservableCollection<string>(settings.Current.NetworkSearchRecent);
        settings.Changed += OnSettingsChanged;
        session.StateChanged += OnSessionStateChanged;
    }

    /// <summary>Releases settings and session subscriptions when the page is removed.</summary>
    public void Detach()
    {
        _settings.Changed -= OnSettingsChanged;
        _session.StateChanged -= OnSessionStateChanged;
    }

    /// <summary>Re-subscribes when the page returns to view.</summary>
    public void Attach()
    {
        _settings.Changed -= OnSettingsChanged;
        _settings.Changed += OnSettingsChanged;
        _session.StateChanged -= OnSessionStateChanged;
        _session.StateChanged += OnSessionStateChanged;
    }

    /// <summary>Earlier searches, newest first, kept with desktop's in the shared settings.</summary>
    public ObservableCollection<string> Recent { get; }

    public bool HasRecent => Recent.Count > 0 && IsIdle;

    public bool HasError => ErrorMessage is not null;

    /// <summary>Nothing searched yet: the page shows what it can find, and recent searches.</summary>
    public bool IsIdle => _result is null && !IsSearching && !HasError;

    public bool IsSingle => _result?.Outcome == NetworkSearchOutcome.Single && Endpoint is not null;

    public bool IsList => _result?.Outcome is NetworkSearchOutcome.List or NetworkSearchOutcome.Overview;

    public bool IsNotFound => _result?.Outcome == NetworkSearchOutcome.NotFound;

    public bool HasResults => _result is { Endpoints.Count: > 0 };

    /// <summary>The list's rows after the source chips, in the chosen order.</summary>
    public IReadOnlyList<EndpointRowViewModel> Rows { get; private set; } = [];

    public IReadOnlyList<SearchSourceChip> SourceChips { get; private set; } = [];

    public bool HasSourceChips => IsList && SourceChips.Count > 1;

    /// <summary>The row filter hides everything: a different sentence from "found nothing".</summary>
    public bool ShowNoMatchingRows => IsList && _allRows.Count > 0 && Rows.Count == 0;

    public IReadOnlyList<CoverageItemViewModel> Coverage { get; private set; } = [];

    public bool HasCoverage => Coverage.Count > 0;

    public bool HasSourceDetails => Coverage.Count > 1;

    public string SourcesButtonText => SourcesExpanded ? "Hide sources" : "Show sources";

    [RelayCommand]
    private void ToggleSources() => SourcesExpanded = !SourcesExpanded;

    /// <summary>The scope of the completed lookup, independent of the visible rows.</summary>
    public string? CoverageText { get; private set; }

    /// <summary>Distinguishes incomplete sources before the reader reaches an answer.</summary>
    public string? BlindSpotText { get; private set; }

    public bool HasBlindSpots => BlindSpotText is not null;

    /// <summary>"12 endpoints on 192.0.2.0/24", "Showing 1–50 of 312".</summary>
    public string? ListHeading => _result is not { } r || !IsList ? null
        : r.Outcome == NetworkSearchOutcome.Overview
            ? $"Known endpoints, {Range(r)} of {r.Total.ToString("N0", CultureInfo.CurrentCulture)}"
            : $"{r.Total.ToString("N0", CultureInfo.CurrentCulture)} matches for “{r.Query.Text}”" + (r.Total > r.Endpoints.Count ? $", {Range(r)}" : string.Empty);

    /// <summary>"+262 more", where the page ends before the results do (#21159).</summary>
    public string? MoreText => _result is { Remaining: > 0 } r ? $"+{r.Remaining.ToString("N0", CultureInfo.CurrentCulture)} more" : null;

    public bool HasMore => MoreText is not null;

    public bool HasEarlier => _result is { Offset: > 0 };

    public string NotFoundTitle => _result?.IsConfirmedAbsent == true ? $"Nothing found for “{_result.Query.Text}”" : $"“{_result?.Query.Text}” couldn't be confirmed absent";

    public string? IgnoredTermsText => _result?.Query.IgnoredTerms is > 0 and var n
        ? $"{n.ToString(CultureInfo.CurrentCulture)} entries in the list weren't addresses, MACs or names and were left out."
        : null;

    public string SearchedText { get; private set; } = string.Empty;

    /// <summary>How MAC addresses are written, desktop's setting.</summary>
    public MacAddressFormat MacFormat => _macFormat;

    public string SortText => Sort switch
    {
        SearchSort.Name => "Sort: name",
        SearchSort.Mac => "Sort: MAC",
        _ => "Sort: address",
    };

    /// <summary>Runs <paramref name="query"/>, as from a link in a result or a recent search.</summary>
    public Task SearchForAsync(string query)
    {
        Query = query;
        return SearchAsync(query, 0);
    }

    [RelayCommand]
    private Task SearchAsync() => SearchAsync(Query, 0);

    [RelayCommand]
    private Task UseRecentAsync(string query) => SearchForAsync(query);

    [RelayCommand]
    private void ClearRecent()
    {
        _settings.Current.NetworkSearchRecent.Clear();
        _settings.SaveQuietly();
        Recent.Clear();
        OnPropertyChanged(nameof(HasRecent));
    }

    [RelayCommand(CanExecute = nameof(CanPageForward))]
    private Task NextPageAsync() => SearchAsync(_result!.Query.Text, _result.Offset + _result.Endpoints.Count, remember: false, paging: true);

    private bool CanPageForward() => _result?.Remaining > 0 && !IsSearching;

    [RelayCommand(CanExecute = nameof(CanPageBack))]
    private Task PreviousPageAsync() => SearchAsync(_result!.Query.Text, Math.Max(0, _result.Offset - new NetworkSearchOptions().PageSize), remember: false, paging: true);

    private bool CanPageBack() => _result?.Offset > 0 && !IsSearching;

    [RelayCommand]
    private void Cancel()
    {
        _generation++;
        _searchCts?.Cancel();
        if (Endpoint is { IsLoadingConnections: true } endpoint)
        {
            endpoint.ConnectionsCancelled = true;
        }

        IsSearching = false;
    }

    /// <summary>Back to the empty page, with recent searches.</summary>
    [RelayCommand]
    private void Clear()
    {
        Cancel();
        Query = string.Empty;
        ErrorMessage = null;
        Present(null);
    }

    [RelayCommand]
    private void ChooseSort(SearchSort sort)
    {
        Sort = sort;
        ShowRows();
        OnPropertyChanged(nameof(SortText));
    }

    /// <summary>The results as CSV, through the share sheet: desktop's columns.</summary>
    [RelayCommand]
    private async Task ShareAsync()
    {
        if (_share is null || _result is not { } r || r.Endpoints.Count == 0)
        {
            return;
        }

        var endpoints = IsList ? Rows.Select(row => row.Record).ToList() : r.Endpoints;
        var name = $"network-search-{DateTime.Now:yyyy-MM-dd-HHmmss}.csv";
        await _share.ShareTextFileAsync(name, NetworkSearchExport.ToCsv(endpoints, _macFormat), "text/csv", "Network Search results");
    }

    public void OpenDevice(int deviceId) => _ = _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = deviceId });

    public void OpenPort(int deviceId, int portId)
    {
        var port = Endpoint is null ? null : FindPort(portId);
        var parameters = new Dictionary<string, object> { [Routes.DeviceIdParameter] = deviceId };
        if (port?.IfName is { Length: > 0 } ifName)
        {
            parameters[Routes.PortParameter] = ifName;
        }

        _ = _navigation.GoToAsync(Routes.DeviceGraphs, parameters);
    }

    public void Copy(string value) => _ = CopyAsync(value);

    private async Task CopyAsync(string value)
    {
        if (_clipboard is null)
        {
            return;
        }

        await _clipboard.SetTextAsync(value);
        Copied?.Invoke(this, value);
    }

    /// <summary>A value went on the clipboard, for the page to say so.</summary>
    public event EventHandler<string>? Copied;

    /// <summary>Again with the same query and page, when the page is pulled down.</summary>
    [RelayCommand]
    private Task RefreshAsync() => _result is { } r && !IsSearching ? SearchAsync(r.Query.Text, r.Offset, remember: false) : Task.CompletedTask;

    private DesktopNMS.Core.Models.Port? FindPort(int portId)
        => _result?.Endpoints.SelectMany(e => e.Locations.Select(l => l.Port).Concat(e.Neighbours.Select(n => n.Port)).Concat(e.Subnet?.Interfaces.Select(i => i.Port) ?? []))
            .FirstOrDefault(p => p?.PortId == portId);

    private async Task SearchAsync(string query, int offset, bool remember = true, bool paging = false)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        var cts = _searchCts = new CancellationTokenSource();
        var generation = ++_generation;
        var connection = _session.Connection;
        Query = query;
        ErrorMessage = null;
        IsSearching = true;
        try
        {
            var result = await new NetworkSearch(_client, Options).SearchAsync(query, offset, cts.Token);
            if (generation != _generation || !_session.IsConnected || !ReferenceEquals(connection, _session.Connection))
            {
                return;
            }

            if (remember && !result.Query.IsEmpty)
            {
                Remember(result.Query.Text);
            }

            SearchedText = "Searched " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            _resultConnection = connection;
            Present(result, keepCoverage: paging && result.Outcome == _result?.Outcome);
            if (Endpoint is { MonitoredDeviceId: { } deviceId } endpoint)
            {
                await LoadConnectionsAsync(endpoint, deviceId, generation, connection, cts.Token);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Replaced by a newer search, cancelled, or the connection went.
        }
        catch (LibreNmsApiException ex)
        {
            if (generation != _generation) return;
            _logger?.LogWarning("Network search failed: {Message}", ex.Message);
            ErrorMessage = ex.ToUserMessage();
            Present(null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (generation != _generation) return;
            _logger?.LogError(ex, "Network search failed unexpectedly");
            ErrorMessage = "The search couldn't finish. Try again, or check the connection to LibreNMS.";
            Present(null);
        }
        finally
        {
            if (generation == _generation)
            {
                IsSearching = false;
                OnPropertyChanged(nameof(HasRecent));
            }
        }
    }

    private void Present(NetworkSearchResult? result, bool keepCoverage = false)
    {
        var previous = ReferenceEquals(result, _result) ? Endpoint : null;
        if (!keepCoverage && !ReferenceEquals(result, _result))
        {
            PresentCoverage(result);
        }

        _result = result;
        var blind = (result?.Coverage ?? [])
            .Where(c => c.State is SourceState.PermissionDenied or SourceState.Unavailable or SourceState.Failed)
            .Select(c => c.Source)
            .ToHashSet();
        Endpoint = result is { Outcome: NetworkSearchOutcome.Single, Endpoints: [var only] }
            ? new EndpointViewModel(only, _macFormat, this, _settings.Current.ServerTimestampsAreUtc, SearchForAsync, blind, _deviceNameStyle)
            : null;

        // The same result again, in a new MAC format: keep its neighbours.
        if (Endpoint is { } replacement && previous is not null && previous.MonitoredDeviceId == replacement.MonitoredDeviceId)
        {
            replacement.AllConnections = previous.AllConnections;
            replacement.ConnectionsFailed = previous.ConnectionsFailed;
            replacement.ConnectionsCancelled = previous.ConnectionsCancelled;
        }

        _allRows = result is { Outcome: NetworkSearchOutcome.List or NetworkSearchOutcome.Overview }
            ? result.Endpoints.Select(e => new EndpointRowViewModel(e, _macFormat, this, SearchForAsync)).ToList()
            : [];
        var hidden = SourceChips.Where(c => !c.IsSelected).Select(c => c.Key).ToHashSet();
        SourceChips = _allRows.SelectMany(r => r.SourceKeys).GroupBy(k => k)
            .Select(g => new SearchSourceChip(g.Key, g.Count(), ShowRows) { IsSelected = !hidden.Contains(g.Key) })
            .ToList();
        ShowRows();
        foreach (var name in new[]
                 {
                     nameof(IsSingle), nameof(IsList), nameof(IsNotFound), nameof(IsIdle), nameof(HasResults), nameof(SourceChips), nameof(HasSourceChips),
                     nameof(Coverage), nameof(HasCoverage), nameof(CoverageText), nameof(HasSourceDetails), nameof(BlindSpotText), nameof(HasBlindSpots),
                     nameof(ListHeading), nameof(MoreText), nameof(HasMore),
                     nameof(HasEarlier), nameof(NotFoundTitle), nameof(IgnoredTermsText), nameof(SearchedText), nameof(HasRecent),
                 })
        {
            OnPropertyChanged(name);
        }

        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    private void PresentCoverage(NetworkSearchResult? result)
    {
        SourcesExpanded = false;
        Coverage = (result?.Coverage ?? []).Select(source => new CoverageItemViewModel(source)).ToList();
        var checkedSources = Coverage.Where(source => !source.IsBlindSpot && source.State is SourceState.Found or SourceState.NoEntry).ToList();
        var checkedText = checkedSources.Count switch
        {
            0 => null,
            1 when checkedSources[0].Source == SearchSource.Devices && result is { Total: > 0 }
                   && result.Query.Terms.Count > 0 && result.Query.Terms.All(term => term.Kind == SearchTermKind.Name)
                => "Found in monitored devices.",
            1 => $"Checked {checkedSources[0].Label}.",
            _ => $"Checked {checkedSources.Count:N0} sources.",
        };
        var skipped = Coverage.Where(source => source.State == SourceState.NotQueried && !source.IsTruncated)
            .Select(source => source.Label + " were not checked.");
        CoverageText = string.Join(" ", new[] { checkedText }.OfType<string>().Concat(skipped));
        if (CoverageText.Length == 0)
        {
            CoverageText = null;
        }

        var incomplete = Coverage.Where(source => source.IsBlindSpot)
            .Select(source => $"{source.Label}: {source.StateText}.").ToList();
        BlindSpotText = incomplete.Count == 0 ? null
            : string.Join(" ", incomplete) + " A result may be missing.";
    }

    private void ShowRows()
    {
        var shown = SourceChips.Where(c => c.IsSelected).Select(c => c.Key).ToHashSet();
        var rows = _allRows.Where(r => SourceChips.Count <= 1 || r.SourceKeys.Any(shown.Contains));
        Rows = (Sort switch
        {
            SearchSort.Name => rows.OrderBy(r => r.HasName ? 0 : 1).ThenBy(r => r.NameText, NaturalOrder.Instance),
            SearchSort.Mac => rows.OrderByDescending(r => r.HasMac).ThenBy(r => r.MacText, StringComparer.OrdinalIgnoreCase),
            _ => rows.OrderBy(r => r.SortKey, StringComparer.Ordinal),
        }).ToList();
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(ShowNoMatchingRows));
    }

    private async Task LoadConnectionsAsync(EndpointViewModel endpoint, int deviceId, int generation, object? connection, CancellationToken token)
    {
        try
        {
            var ports = _client.Ports.ListForDeviceAsync(deviceId, token);
            var knownDevices = LoadKnownDevicesAsync(token);
            var built = await new DeviceNeighbourLoader(_client, _logger).LoadAsync(
                deviceId,
                ports,
                async _ => _devices is { } cache ? cache.All : (await knownDevices).Values.ToList(),
                token);
            await knownDevices;
            if (generation != _generation || !ReferenceEquals(connection, _session.Connection) || Endpoint?.MonitoredDeviceId != deviceId)
            {
                return;
            }

            var byId = ports.Result.GroupBy(p => p.PortId).ToDictionary(g => g.Key, g => g.First());
            var deviceById = knownDevices.Result;
            var style = _settings.Current.DeviceNameStyle;
            Endpoint.AllConnections = built
                .Select(n =>
                {
                    var device = n.RemoteDeviceId is { } id
                        ? _devices?.Get(id) ?? (deviceById.TryGetValue(id, out var cached) ? cached : null)
                        : null;
                    var name = device is null ? null : style.Resolve(device, device.Hostname);
                    var local = n.LocalPortId is { } portId && byId.TryGetValue(portId, out var port) ? port : null;
                    return new NeighbourItemViewModel(n, device, name, local, endpoint.MonitoredDeviceName ?? string.Empty, OpenDevice);
                })
                .OrderBy(n => n.SortRank)
                .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (generation == _generation && Endpoint is { IsLoadingConnections: true } pending && pending.MonitoredDeviceId == deviceId)
            {
                pending.ConnectionsCancelled = true;
            }
        }
        catch (Exception ex) when (generation == _generation && ex is not OutOfMemoryException)
        {
            _logger?.LogWarning("Network search couldn't read a device's neighbours: {Message}", ex.Message);
            if (ReferenceEquals(connection, _session.Connection) && Endpoint is { } current && current.MonitoredDeviceId == deviceId)
            {
                current.ConnectionsFailed = true;
            }
        }
    }

    /// <summary>Monitored devices for naming and opening neighbour rows, reused from CorePolling when present.</summary>
    private async Task<IReadOnlyDictionary<int, DesktopNMS.Core.Models.Device>> LoadKnownDevicesAsync(CancellationToken token)
    {
        if (_devices is not null || _polling is null)
        {
            return new Dictionary<int, DesktopNMS.Core.Models.Device>();
        }

        var cached = _polling.CachedDeviceNames;
        if (cached.Count > 0)
        {
            return cached;
        }

        try
        {
            var list = await _polling.CachedDevicesAsync(token);
            return list is { Count: > 0 }
                ? list.GroupBy(d => d.DeviceId).ToDictionary(g => g.Key, g => g.First())
                : new Dictionary<int, DesktopNMS.Core.Models.Device>();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger?.LogDebug("Network search couldn't load cached devices for neighbour resolution: {Message}", ex.Message);
            return new Dictionary<int, DesktopNMS.Core.Models.Device>();
        }
    }

    private void Remember(string query)
    {
        _settings.Current.RememberNetworkSearch(query);
        _settings.SaveQuietly();
        Recent.Clear();
        foreach (var q in _settings.Current.NetworkSearchRecent)
        {
            Recent.Add(q);
        }

        OnPropertyChanged(nameof(HasRecent));
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        var format = settings.NetworkSearchMacFormatValue;
        var nameStyle = settings.DeviceNameStyle;
        if (format != _macFormat || nameStyle != _deviceNameStyle)
        {
            Post(() =>
            {
                _macFormat = format;
                _deviceNameStyle = nameStyle;
                OnPropertyChanged(nameof(MacFormat));
                Present(_result);
            });
        }
    }

    /// <summary>Another server or account: its results, and the search, go.</summary>
    private void OnSessionStateChanged(object? sender, EventArgs e) => Post(() =>
    {
        if (_session.IsConnected && ReferenceEquals(_session.Connection, _resultConnection) && !IsSearching)
        {
            return;
        }

        Cancel();
        ErrorMessage = null;
        Query = string.Empty;
        _resultConnection = null;
        Present(null);
        Recent.Clear();
        foreach (var q in _settings.Current.NetworkSearchRecent)
        {
            Recent.Add(q);
        }

        OnPropertyChanged(nameof(HasRecent));
    });

    private void Post(Action action)
    {
        if (_ui is { } ui && SynchronizationContext.Current != ui)
        {
            ui.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }

    private static string Range(NetworkSearchResult r) => r.Endpoints.Count == 0
        ? "none"
        : $"{(r.Offset + 1).ToString("N0", CultureInfo.CurrentCulture)}–{(r.Offset + r.Endpoints.Count).ToString("N0", CultureInfo.CurrentCulture)}";
}
