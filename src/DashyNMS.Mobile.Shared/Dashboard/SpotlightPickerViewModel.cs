using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Services;
using DesktopNMS.Core.Spotlights;

namespace DashyNMS.Mobile.Dashboard;

/// <summary>One device, group or location the Spotlight picker offers.</summary>
public sealed partial class SpotlightChoice : ObservableObject
{
    public SpotlightChoice(SpotlightTarget target, string? detail = null, bool missing = false)
    {
        Target = target;
        Detail = detail;
        IsMissing = missing;
        AllowsSeveral = SpotlightPickerViewModel.KindOf(target) == SpotlightPickerViewModel.DevicesKind;
    }

    public SpotlightTarget Target { get; }

    /// <summary>Its name, or "Unknown group (Core)" for one the server no longer has.</summary>
    public string Name => IsMissing
        ? SpotlightScope.UnknownName(Target) + (string.IsNullOrWhiteSpace(Target.Name) ? string.Empty : $" ({Target.Name})")
        : Target.Name;

    /// <summary>A device's location, a group's description.</summary>
    public string? Detail { get; }

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    /// <summary>Saved, but the server no longer has it: kept unless unticked, as desktop keeps it.</summary>
    public bool IsMissing { get; }

    /// <summary>
    /// A device is one of several that can be ticked, so its row shows a check
    /// box; a group or location is the single choice, so its row shows a tick.
    /// </summary>
    public bool AllowsSeveral { get; }

    public bool IsSingle => !AllowsSeveral;

    /// <summary>
    /// The whole row is one screen-reader element (#398), so it says what the
    /// tick or check box shows: a visual mark alone told VoiceOver and TalkBack
    /// users nothing about what was chosen.
    /// </summary>
    public string SelectionDescription => string.Join(", ",
        new[] { Name, Detail, IsSelected ? "selected" : "not selected" }.Where(part => !string.IsNullOrWhiteSpace(part)));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionDescription))]
    private bool _isSelected;
}

/// <summary>
/// An Event log or Graylog card's Spotlight (#270, desktop#552): the whole
/// network, one or more devices, a device group or a location - Intelligence's
/// word for what a view is kept to. Saved on the widget, so desktop shows the
/// same.
/// </summary>
/// <remarks>
/// One kind at a time, as desktop's picker: several devices, or one group, or
/// one location. Switching kind starts that kind's choice afresh.
/// </remarks>
public sealed partial class SpotlightPickerViewModel : ViewModelBase
{
    public const int DevicesKind = 0;
    public const int GroupKind = 1;
    public const int LocationKind = 2;

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly IDashboardLayoutService _layout;
    private readonly Dictionary<int, List<SpotlightChoice>> _choices = new();
    private string? _widgetId;
    private bool _loaded;
    private int _loadGeneration;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowsSeveral), nameof(HintText), nameof(IsDevices), nameof(IsGroup), nameof(IsLocation),
        nameof(DevicesDescription), nameof(GroupDescription), nameof(LocationDescription), nameof(SelectedText))]
    private int _kindIndex;

    public SpotlightPickerViewModel(ILibreNmsClient client, ISettingsStore settings, INavigationService navigation, IDashboardLayoutService? layout = null)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
        _layout = layout ?? new DashboardLayoutService(settings);
    }

    public IReadOnlyList<string> Kinds { get; } = ["Devices", "Group", "Location"];

    public BulkObservableCollection<SpotlightChoice> Choices { get; } = new();

    /// <summary>Which segment is chosen, for the page's segmented control.</summary>
    public bool IsDevices => KindIndex == DevicesKind;

    public bool IsGroup => KindIndex == GroupKind;

    public bool IsLocation => KindIndex == LocationKind;

    /// <summary>Each segment says whether it's the chosen one, which its colour alone doesn't tell a screen reader.</summary>
    public string DevicesDescription => SegmentDescription("Devices", IsDevices);

    public string GroupDescription => SegmentDescription("Group", IsGroup);

    public string LocationDescription => SegmentDescription("Location", IsLocation);

    /// <summary>Devices: several. A group or a location: one.</summary>
    public bool AllowsSeveral => KindIndex == DevicesKind;

    /// <summary>No prompt after a failure: there is nothing to choose from until setup is reopened.</summary>
    public string HintText => HasError ? string.Empty
        : AllowsSeveral ? "Choose one or more devices." : KindIndex == GroupKind ? "Choose a device group." : "Choose a location.";

    /// <summary>
    /// How many devices are ticked, searched-out ones included, as Sensors counts
    /// its draft. A group or location is a single choice its tick already shows.
    /// </summary>
    public string SelectedText => HasError || !_loaded || !AllowsSeveral ? string.Empty
        : $"{(_choices.TryGetValue(KindIndex, out var choices) ? choices.Count(c => c.IsSelected) : 0)} selected";

    /// <summary>
    /// The existing route that loads again, stated under the error rather than a
    /// separate retry button, as the other setup pages do.
    /// </summary>
    public string? RecoveryText => HasError ? "To retry, go back and open Spotlight again." : null;

    public bool IsEmpty => _loaded && !IsBusy && Choices.Count == 0 && !HasError;

    public string EmptyText => SearchText.Trim().Length > 0 ? "Nothing matches." : KindIndex switch
    {
        GroupKind => "LibreNMS has no device groups.",
        LocationKind => "LibreNMS has no locations.",
        _ => "LibreNMS has no devices.",
    };

    private DashboardWidget? Widget => _settings.Current.DashboardWidgets.FirstOrDefault(w => w.Id == _widgetId);

    public Task LoadAsync(string widgetId)
    {
        // Reloading starts from what's saved, so nothing half-loaded can be saved,
        // and an earlier load still in flight is ignored when it finishes.
        var generation = ++_loadGeneration;
        _loaded = false;
        _choices.Clear();
        Choices.ReplaceAll([]);
        SaveCommand.NotifyCanExecuteChanged();
        _widgetId = widgetId;
        var saved = Widget?.Spotlight.Where(t => t.Kind is not null).ToList() ?? [];
        KindIndex = saved.FirstOrDefault()?.Kind switch
        {
            SpotlightKind.Group => GroupKind,
            SpotlightKind.Location => LocationKind,
            _ => DevicesKind,
        };

        return RunAsync(async () =>
        {
            var devicesTask = _client.Devices.ListAsync();
            var groupsTask = _client.DeviceGroups.ListAsync();
            var locationsTask = _client.Locations.ListAsync();
            await Task.WhenAll(devicesTask, groupsTask, locationsTask);
            if (generation != _loadGeneration)
            {
                return;
            }

            var style = _settings.Current.DeviceNameStyle;
            _choices[DevicesKind] = devicesTask.Result
                .Select(d => new SpotlightChoice(SpotlightTarget.Device(d.DeviceId, new DeviceItem(d, style).Name), d.LocationName()))
                .OrderBy(c => c.Name, NaturalOrder.Instance)
                .ToList();
            _choices[GroupKind] = groupsTask.Result
                .Select(g => new SpotlightChoice(SpotlightTarget.Group(g.Id, g.Name), g.Description))
                .OrderBy(c => c.Name, NaturalOrder.Instance)
                .ToList();
            _choices[LocationKind] = locationsTask.Result
                .Select(l => new SpotlightChoice(SpotlightTarget.Location(l.Id, l.Name)))
                .OrderBy(c => c.Name, NaturalOrder.Instance)
                .ToList();

            // What's saved is ticked; what the server no longer has stays, at the top, until unticked.
            foreach (var target in saved)
            {
                var kind = KindOf(target);
                if (_choices[kind].FirstOrDefault(c => c.Target.SameAs(target)) is { } choice)
                {
                    choice.IsSelected = true;
                }
                else
                {
                    _choices[kind].Insert(0, new SpotlightChoice(target.Clone(), missing: true) { IsSelected = true });
                }
            }

            _loaded = true;
            SaveCommand.NotifyCanExecuteChanged();
            Refilter();
        });
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsBusy))
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(ErrorMessage))
        {
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(HintText)));
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(RecoveryText)));
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(SelectedText)));
        }
    }

    partial void OnKindIndexChanged(int value)
    {
        // A Spotlight is one kind: what was ticked under another goes.
        foreach (var (kind, choices) in _choices)
        {
            if (kind != value)
            {
                choices.ForEach(c => c.IsSelected = false);
            }
        }

        Refilter();
    }

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(Refilter);

    [RelayCommand]
    private void ChooseKind(string? kind)
    {
        if (int.TryParse(kind, out var index) && index is >= DevicesKind and <= LocationKind)
        {
            KindIndex = index;
        }
    }

    /// <summary>Devices tick and untick; a group or location is the one chosen.</summary>
    [RelayCommand]
    private void Pick(SpotlightChoice? choice)
    {
        if (choice is null)
        {
            return;
        }

        if (!AllowsSeveral && !choice.IsSelected && _choices.TryGetValue(KindIndex, out var others))
        {
            others.ForEach(c => c.IsSelected = false);
        }

        choice.IsSelected = !choice.IsSelected;
        OnPropertyChanged(nameof(SelectedText));
    }

    /// <summary>
    /// Only once the choices have loaded: after a failed load there's nothing
    /// ticked, and saving that would quietly turn the card's saved Spotlight
    /// into the whole network (#398).
    /// </summary>
    private bool CanSave() => _loaded && !IsBusy && !string.IsNullOrEmpty(_widgetId);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (!CanSave())
        {
            return;
        }

        var chosen = _choices.TryGetValue(KindIndex, out var choices)
            ? choices.Where(c => c.IsSelected).Select(c => c.Target).ToList()
            : [];
        _layout.SetSpotlight(_widgetId, chosen);
        await _navigation.GoToAsync(Routes.Back);
    }

    /// <summary>No Spotlight: every device, as a card starts.</summary>
    [RelayCommand]
    private async Task WholeNetworkAsync()
    {
        if (_widgetId is not null)
        {
            _layout.SetSpotlight(_widgetId, []);
        }

        await _navigation.GoToAsync(Routes.Back);
    }

    private static string SegmentDescription(string kind, bool chosen) => chosen ? $"{kind}, selected" : kind;

    internal static int KindOf(SpotlightTarget target) => target.Kind switch
    {
        SpotlightKind.Group => GroupKind,
        SpotlightKind.Location => LocationKind,
        _ => DevicesKind,
    };

    private void Refilter()
    {
        var term = SearchText.Trim();
        var choices = _choices.TryGetValue(KindIndex, out var list) ? list : [];
        Choices.ReplaceAll(choices
            .Where(c => term.Length == 0 || c.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || (c.Detail?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false))
            .ToList());
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(SelectedText));
    }
}
