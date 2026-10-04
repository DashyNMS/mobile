using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Dashboard;

/// <summary>One card on the Customise page: shown or not, and where.</summary>
public sealed partial class DashboardCardOption : ObservableObject
{
    public DashboardCardOption(DashboardWidget widget, bool isShown)
    {
        Widget = widget;
        Kind = DashboardLayout.KindOf(widget.WidgetType);
        _isShown = isShown;
    }

    /// <summary>The card itself - its own title and set-up go with it (#87).</summary>
    public DashboardWidget Widget { get; }

    public DashboardCardKind Kind { get; }

    /// <summary>Its own title once given one ("Core switch temps"), else the kind's.</summary>
    public string Title => DashboardLayout.HasOwnTitle(Widget) ? Widget.Title : Kind.Title;

    public string Description => Kind.Description;

    /// <summary>Sensors and Graph have something to choose.</summary>
    public bool CanSetUp => Kind.AllowsSeveral;

    /// <summary>
    /// Sensors and Graph cards are added, so they're removed rather than
    /// switched off; every other kind has its switch.
    /// </summary>
    public bool CanRemove => Kind.AllowsSeveral;

    public bool CanSwitch => !CanRemove;

    [ObservableProperty]
    private bool _isShown;

    /// <summary>Its title may have changed in its set-up.</summary>
    public void Refresh() => OnPropertyChanged(nameof(Title));
}

/// <summary>
/// Which dashboard cards show, and in what order - the phone's version of
/// desktop's dashboard edit mode, without the resizing a one-column screen
/// doesn't need. Sensors and Graph cards can be added as often as wanted,
/// each with its own title and set-up (#87). Changes save as they're made.
/// </summary>
public sealed partial class CustomiseDashboardViewModel : ViewModelBase
{
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly IDialogService? _dialogs;

    public CustomiseDashboardViewModel(ISettingsStore settings, INavigationService navigation, IDialogService? dialogs = null)
    {
        _settings = settings;
        _navigation = navigation;
        _dialogs = dialogs;
        Build(DashboardLayout.Current(settings.Current));
    }

    public ObservableCollection<DashboardCardOption> Cards { get; } = new();

    /// <summary>Back from a card's set-up: its title may be new.</summary>
    public void Refresh()
    {
        foreach (var card in Cards)
        {
            card.Refresh();
        }
    }

    [RelayCommand]
    private void MoveUp(DashboardCardOption? card) => Move(card, -1);

    [RelayCommand]
    private void MoveDown(DashboardCardOption? card) => Move(card, +1);

    /// <summary>Another Sensors card, after the cards showing, then straight to choosing its sensors.</summary>
    [RelayCommand]
    private Task AddSensorsAsync() => AddAsync(DashboardLayout.Sensors);

    /// <summary>Another Graph card, then straight to choosing its graph.</summary>
    [RelayCommand]
    private Task AddGraphAsync() => AddAsync(DashboardLayout.Graph);

    /// <summary>
    /// Another card of a kind there can be several of - asked which. Sensors
    /// and Graph go straight to their set-up, having nothing to show without
    /// it; a Top card shows at once with desktop's defaults (#103).
    /// </summary>
    [RelayCommand]
    private async Task AddCardAsync()
    {
        var kinds = DashboardLayout.Kinds.Where(k => k.AllowsSeveral).ToList();
        var chosen = _dialogs is null ? null : await _dialogs.ChooseAsync("Add a card", kinds.Select(k => k.Title).ToList());
        if (kinds.FirstOrDefault(k => k.Title == chosen) is { } kind)
        {
            await AddAsync(kind.Type);
        }
    }

    /// <summary>A card's own set-up: its sensors or graph, and its title. Shown first if it wasn't.</summary>
    [RelayCommand]
    private Task SetUpAsync(DashboardCardOption? card)
    {
        if (card is null || !card.CanSetUp)
        {
            return Task.CompletedTask;
        }

        card.IsShown = true;
        return OpenSetUpAsync(card.Widget);
    }

    /// <summary>An added Sensors or Graph card taken off the dashboard, its set-up with it - after asking.</summary>
    [RelayCommand]
    private async Task RemoveAsync(DashboardCardOption? card)
    {
        if (card is null || !card.CanRemove)
        {
            return;
        }

        if (_dialogs is not null
            && !await _dialogs.ConfirmDestructiveAsync("Remove card", $"Remove the card \"{card.Title}\" from the dashboard? Its set-up goes with it. {Confirmations.CannotBeUndone}", "Remove"))
        {
            return;
        }

        Cards.Remove(card);
        Save();
    }

    /// <summary>
    /// Back to the dashboard as it came: the default cards, and no Sensors or
    /// Graph cards - after asking, and saying what goes (#146). It used to
    /// happen at a tap, taking every added card's set-up with it.
    /// </summary>
    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        if (_dialogs is not null && !await _dialogs.ConfirmDestructiveAsync("Standard dashboard", ResetMessage(), "Reset"))
        {
            return;
        }

        Build(DashboardLayout.DefaultTypes.Select(DashboardLayout.New).ToList());
        Save();
    }

    /// <summary>What resetting loses: the added cards by count, else just the order and which show.</summary>
    internal string ResetMessage()
    {
        var added = Cards.Count(c => c.CanRemove);
        var lost = added switch
        {
            0 => "Your card order and which cards show go back to how they came.",
            1 => "Your added card and its set-up go, and the cards go back to how they came.",
            _ => $"Your {added} added cards and their set-up go, and the cards go back to how they came.",
        };
        return $"Go back to the standard dashboard? {lost} {Confirmations.CannotBeUndone}";
    }

    private async Task AddAsync(string type)
    {
        var widget = DashboardLayout.New(type);
        var option = Watch(new DashboardCardOption(widget, isShown: true));
        Cards.Insert(Cards.Count(c => c.IsShown), option);
        Save();
        if (!TopCards.IsTop(type))
        {
            await OpenSetUpAsync(widget);
        }
    }

    private Task OpenSetUpAsync(DashboardWidget widget) => _navigation.GoToAsync(
        widget.WidgetType switch
        {
            DashboardLayout.Sensors => Routes.PickSensors,
            DashboardLayout.Graph => Routes.PickGraph,
            _ => Routes.TopCardSetUp,
        },
        new Dictionary<string, object> { [Routes.WidgetIdParameter] = widget.Id });

    /// <summary>
    /// The cards showing, in their order; then each once-only kind that isn't.
    /// Sensors and Graph cards come from the Add buttons, so they're only
    /// listed once added.
    /// </summary>
    private void Build(IReadOnlyList<DashboardWidget> shown)
    {
        Cards.Clear();
        foreach (var widget in shown)
        {
            Cards.Add(Watch(new DashboardCardOption(widget, isShown: true)));
        }

        foreach (var kind in DashboardLayout.Kinds.Where(k => !k.AllowsSeveral && shown.All(w => w.WidgetType != k.Type)))
        {
            Cards.Add(Watch(new DashboardCardOption(DashboardLayout.New(kind.Type), isShown: false)));
        }
    }

    private DashboardCardOption Watch(DashboardCardOption option)
    {
        option.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DashboardCardOption.IsShown))
            {
                Save();
            }
        };
        return option;
    }

    private void Move(DashboardCardOption? card, int by)
    {
        if (card is null)
        {
            return;
        }

        var from = Cards.IndexOf(card);
        var to = from + by;
        if (from < 0 || to < 0 || to >= Cards.Count)
        {
            return;
        }

        Cards.Move(from, to);
        Save();
    }

    private void Save()
    {
        DashboardLayout.Save(_settings.Current, Cards.Where(c => c.IsShown).Select(c => c.Widget));
        _settings.Save();
    }
}
