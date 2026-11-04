using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>One page in More: its title and line, and whether it's on the tab bar.</summary>
public sealed partial class MoreItem : ObservableObject
{
    public MoreItem(AppPage page)
    {
        Page = page;
    }

    public AppPage Page { get; }

    public string Title => AppPages.Title(Page);

    public string Description => AppPages.Description(Page);

    /// <summary>Only ambiguous destination names need supporting copy in the catalogue.</summary>
    public string Summary => Page is AppPage.Health or AppPage.Map or AppPage.NetworkMap or AppPage.Neighbours or AppPage.Graylog
        ? Description : string.Empty;

    public bool HasSummary => Summary.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinDescription))]
    [NotifyPropertyChangedFor(nameof(PinLabel))]
    [NotifyPropertyChangedFor(nameof(PinHint))]
    [NotifyPropertyChangedFor(nameof(PinState))]
    [NotifyPropertyChangedFor(nameof(OpenDescription))]
    [NotifyPropertyChangedFor(nameof(OpenHint))]
    private bool _isPinned;

    /// <summary>Whether the action can change pins rather than explain the capacity limit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinState))]
    [NotifyPropertyChangedFor(nameof(OpenDescription))]
    [NotifyPropertyChangedFor(nameof(PinHint))]
    [NotifyPropertyChangedFor(nameof(PinDescription))]
    private bool _canToggle;

    public string PinState => IsPinned ? "Pinned to the tab bar" : CanToggle
        ? "Not pinned" : "Not pinned. Unpin another page to make room.";

    public string OpenDescription => string.Join(". ", new[] { Title, Summary, IsPinned ? "Pinned" : "Not pinned" }.Where(text => text.Length > 0));

    public string OpenHint => IsPinned ? "Switches to this page's tab" : "Opens this page. Back returns to More";

    /// <summary>For screen readers: what the pin button will do.</summary>
    public string PinDescription => IsPinned ? $"Unpin {Title} from the tab bar. Pinned."
        : CanToggle ? $"Pin {Title} to the tab bar. Not pinned."
        : $"{Title}. Not pinned. Tab bar full. Explains how to make room.";

    public string PinLabel => IsPinned ? "Unpin" : "Pin";

    public string PinHint => CanToggle ? "Changes the tab bar immediately" : string.Empty;
}

/// <summary>A More heading and its pages.</summary>
public sealed record MoreGroup(string Name, IReadOnlyList<MoreItem> Items);

/// <summary>
/// The More tab (#68): every page, grouped, with a pin beside each for the
/// tab bar. Up to <see cref="TabPins.MaxPinned"/> can be pinned; Dashboard
/// and More always stay.
/// </summary>
public sealed partial class MoreViewModel : ViewModelBase
{
    private readonly TabPins _pins;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IReadOnlyList<MoreItem> _items;

    public MoreViewModel(TabPins pins, INavigationService navigation, IDialogService dialogs)
    {
        _pins = pins;
        _navigation = navigation;
        _dialogs = dialogs;
        _items = AppPages.All.Select(page => new MoreItem(page)).ToList();
        Groups = _items.GroupBy(item => AppPages.Group(item.Page)).Select(g => new MoreGroup(g.Key, g.ToList())).ToList();
        Update();
    }

    public IReadOnlyList<MoreGroup> Groups { get; }

    /// <summary>"2 of 3 pinned".</summary>
    public string PinnedText => $"{_pins.Pinned.Count} of {TabPins.MaxPinned} pinned";

    public string CapacityText => PinnedText + (_pins.CanPinMore ? "." : ". Unpin a page to make room.");

    /// <summary>Its tab when pinned, pushed onto More when not.</summary>
    [RelayCommand]
    private Task OpenAsync(MoreItem item) => _navigation.GoToAsync(
        _pins.IsPinned(item.Page) ? Routes.Main + "/" + AppPages.TabRoute(item.Page) : AppPages.PushRoute(item.Page));

    [RelayCommand]
    private async Task TogglePinAsync(MoreItem item)
    {
        if (!_pins.Toggle(item.Page))
        {
            await _dialogs.AlertAsync(
                "The tab bar is full",
                $"Unpin a page first. Dashboard and More always stay, with room for {TabPins.MaxPinned} more.");
            return;
        }

        Update();
    }

    /// <summary>Also when More shows, in case the pins changed elsewhere.</summary>
    public void Update()
    {
        var canPinMore = _pins.CanPinMore;
        foreach (var item in _items)
        {
            item.IsPinned = _pins.IsPinned(item.Page);
            item.CanToggle = item.IsPinned || canPinMore;
        }

        OnPropertyChanged(nameof(PinnedText));
        OnPropertyChanged(nameof(CapacityText));
    }
}
