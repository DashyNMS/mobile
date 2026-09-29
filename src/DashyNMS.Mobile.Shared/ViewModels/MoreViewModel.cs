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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinDescription))]
    private bool _isPinned;

    /// <summary>Pinned, or there's room to pin it: the pin button isn't dimmed.</summary>
    [ObservableProperty]
    private bool _canToggle;

    /// <summary>For screen readers: what the pin button will do.</summary>
    public string PinDescription => (IsPinned ? "Unpin " : "Pin ") + Title + (IsPinned ? " from" : " to") + " the tab bar";
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
    }
}
