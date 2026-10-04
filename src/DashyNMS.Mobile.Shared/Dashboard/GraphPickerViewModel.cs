using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Dashboard;

public sealed record GraphRangeChoice(GraphTimeRangePreset Preset, string Label);

/// <summary>
/// Sets up a dashboard Graph card, as desktop's Graph widget: a device, one
/// of its graphs (desktop's device-wide, health and wireless listings merged),
/// a time range and, if wanted, its own title. Kept in the card's own
/// <see cref="DashboardWidget"/> - there can be several (#87).
/// </summary>
public sealed partial class GraphPickerViewModel : ViewModelBase
{
    /// <summary>A device search shows at most this many.</summary>
    internal const int MaxDevices = 50;

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private IReadOnlyList<DeviceItem> _devices = [];

    [ObservableProperty]
    private string _deviceSearch = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDevice))]
    [NotifyPropertyChangedFor(nameof(ChoosingDevice))]
    private DeviceItem? _selectedDevice;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private GraphType? _selectedGraph;

    [ObservableProperty]
    private GraphRangeChoice _selectedRange;

    /// <summary>The card's own title ("WAN traffic"); blank for the graph and its device (#87).</summary>
    [ObservableProperty]
    private string _cardTitle = string.Empty;

    public GraphPickerViewModel(ILibreNmsClient client, ISettingsStore settings, INavigationService navigation)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
        _selectedRange = Ranges[1];
    }

    /// <summary>Which card this sets up - from Customise or the card itself (#87).</summary>
    public string? WidgetId { get; set; }

    /// <summary>The ranges a Graph card can show - here, and from the card's own range chip (#140).</summary>
    public static IReadOnlyList<GraphRangeChoice> RangeChoices { get; } =
    [
        new(GraphTimeRangePreset.Hour, "Hour"),
        new(GraphTimeRangePreset.Day, "Day"),
        new(GraphTimeRangePreset.Week, "Week"),
        new(GraphTimeRangePreset.Month, "Month"),
        new(GraphTimeRangePreset.Year, "Year"),
    ];

    public IReadOnlyList<GraphRangeChoice> Ranges => RangeChoices;

    public BulkObservableCollection<DeviceItem> Devices { get; } = new();

    public BulkObservableCollection<GraphType> Graphs { get; } = new();

    public bool HasDevice => SelectedDevice is not null;

    public bool ChoosingDevice => SelectedDevice is null;

    partial void OnDeviceSearchChanged(string value) => FilterDevices();

    partial void OnSelectedRangeChanged(GraphRangeChoice value)
    {
        if (value is null)
        {
            SelectedRange = Ranges[1];
        }
    }

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        // Carry on from the card's set-up so far: its range and title now, its graph once loaded.
        if (Widget() is { } existing)
        {
            SelectedRange = Ranges.FirstOrDefault(r => r.Preset == existing.GraphTimeRangePreset) ?? Ranges[1];
            CardTitle = existing.GraphName is not null && !IsAutomaticTitle(existing) ? existing.Title : string.Empty;
        }

        var style = _settings.Current.DeviceNameStyle;
        _devices = (await _client.Devices.ListAsync())
            .Select(d => new DeviceItem(d, style))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Carry on from the card's current set-up.
        if (Widget() is { GraphDeviceId: { } id } widget && _devices.FirstOrDefault(d => d.DeviceId == id) is { } device)
        {
            await ChooseDeviceAsync(device);
            SelectedGraph = Graphs.FirstOrDefault(g => g.Name == widget.GraphName);
        }

        FilterDevices();
    });

    [RelayCommand]
    private async Task ChooseDeviceAsync(DeviceItem? device)
    {
        if (device is null)
        {
            return;
        }

        SelectedDevice = device;
        SelectedGraph = null;
        await RunAsync(async () =>
        {
            var deviceWide = _client.Graphs.ListAsync(device.DeviceId);
            var health = _client.Graphs.ListHealthAsync(device.DeviceId);
            await Task.WhenAll(deviceWide, health);
            Graphs.ReplaceAll(deviceWide.Result.Concat(health.Result)
                .OrderBy(g => g.Description, StringComparer.OrdinalIgnoreCase)
                .ToList());
        });
    }

    [RelayCommand]
    private void ChangeDevice()
    {
        SelectedDevice = null;
        SelectedGraph = null;
        Graphs.ReplaceAll([]);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (SelectedDevice is not { } device || SelectedGraph is not { } graph)
        {
            return;
        }

        var widget = Widget() ?? DashboardLayout.Add(_settings.Current, DashboardLayout.Graph);
        widget.GraphDeviceId = device.DeviceId;
        widget.GraphName = graph.Name;
        widget.GraphTimeRangePreset = SelectedRange.Preset;
        widget.Title = string.IsNullOrWhiteSpace(CardTitle) ? AutomaticTitle(graph.Description, device.Name) : CardTitle.Trim();
        _settings.Save();
        await _navigation.GoToAsync(Routes.Back);
    }

    private bool CanSave() => SelectedGraph is not null;

    private void FilterDevices()
    {
        var term = DeviceSearch.Trim();
        Devices.ReplaceAll(_devices
            .Where(d => term.Length == 0 || d.Matches(term))
            .Take(MaxDevices)
            .ToList());
    }

    /// <summary>"Traffic · core-sw-01" - what a card is called until it's given a title of its own.</summary>
    internal static string AutomaticTitle(string? graph, string device) => $"{graph} · {device}";

    /// <summary>The card being set up, or null if it has gone (or none was named) - saving then adds one.</summary>
    private DashboardWidget? Widget() =>
        DashboardLayout.Find(_settings.Current, WidgetId) is { WidgetType: DashboardLayout.Graph } widget ? widget : null;

    /// <summary>Whether the card's title is the one made from its graph and device, not one typed in.</summary>
    private static bool IsAutomaticTitle(DashboardWidget widget) =>
        !DashboardLayout.HasOwnTitle(widget) || widget.Title.Contains(" · ", StringComparison.Ordinal);
}
