using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Dashboard;

/// <summary>One card on the Edit dashboard page.</summary>
public sealed partial class DashboardCardOption : ObservableObject
{
    public DashboardCardOption(DashboardWidget widget)
    {
        Widget = widget;
        Kind = DashboardLayout.KindOf(widget.WidgetType);
    }

    /// <summary>The card itself - its own title and set-up go with it (#87).</summary>
    public DashboardWidget Widget { get; }

    public DashboardCardKind Kind { get; }

    /// <summary>Its own title once given one ("Core switch temps"), else the kind's.</summary>
    public string Title => DashboardLayout.TitleOf(Widget);

    /// <summary>
    /// What it shows: "Sensors · 3 sensors", "Top 5, by traffic in" - or,
    /// for a card with nothing to set up, what the kind is.
    /// </summary>
    public string Subtitle => Widget.WidgetType switch
    {
        DashboardLayout.Sensors => Widget.Sensors.Count switch
        {
            0 => "Sensors · none chosen yet",
            1 => "Sensors · 1 sensor",
            var n => string.Create(CultureInfo.CurrentCulture, $"Sensors · {n} sensors"),
        },
        DashboardLayout.Graph => Widget.GraphName is null ? "Graph · not chosen yet"
            : Widget.GraphPortIfName is { } port ? $"Graph · {Widget.GraphName} · {port}" // a port's (#172)
            : "Graph · " + Widget.GraphName,
        _ when TopCards.IsTop(Widget.WidgetType) => TopCards.Summary(Widget),
        _ => Kind.Description,
    };

    /// <summary>Sensors, Graph and Top cards have something to choose.</summary>
    public bool CanSetUp => Kind.AllowsSeveral;

    /// <summary>Just added: outlined for a moment, as on the dashboard.</summary>
    [ObservableProperty]
    private bool _isHighlighted;

    /// <summary>Back from its set-up: its title and what it shows may be new.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
    }
}

/// <summary>
/// Edit dashboard (#140): the cards on the dashboard, in order - dragged to
/// reorder, removed with Undo, set up - and Add card, desktop's widget
/// picker. The phone's version of desktop's edit mode, without the resizing
/// a one-column screen doesn't need. Changes save as they're made.
/// </summary>
/// <remarks>
/// Every card can be removed, not switched off as it used to be. Removing
/// the last leaves an empty dashboard, which shows the welcome card - so
/// there's no separate "start again".
/// </remarks>
public sealed partial class CustomiseDashboardViewModel : ViewModelBase
{
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly DashboardToast _toast;

    public CustomiseDashboardViewModel(ISettingsStore settings, INavigationService navigation, DashboardToast toast)
    {
        _settings = settings;
        _navigation = navigation;
        _toast = toast;
        Refresh();
    }

    public ObservableCollection<DashboardCardOption> Cards { get; } = new();

    public DashboardToast Toast => _toast;

    public bool IsEmpty => Cards.Count == 0;

    /// <summary>
    /// The page is showing: the cards as saved - back from a card's set-up or
    /// the card picker, they may have changed - and Undo and the outline followed.
    /// </summary>
    public void Attach()
    {
        Detach();
        _toast.LayoutRestored += OnLayoutRestored;
        _toast.PropertyChanged += OnToastChanged;
        Refresh();
    }

    /// <summary>The page has gone: the app-wide toast no longer needs it.</summary>
    public void Detach()
    {
        _toast.LayoutRestored -= OnLayoutRestored;
        _toast.PropertyChanged -= OnToastChanged;
    }

    /// <summary>The cards as saved.</summary>
    public void Refresh()
    {
        Cards.Clear();
        foreach (var widget in DashboardLayout.Current(_settings.Current))
        {
            Cards.Add(new DashboardCardOption(widget) { IsHighlighted = widget.Id == _toast.HighlightId });
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>The card picker - desktop's "Add widget" (#140).</summary>
    [RelayCommand]
    private Task AddCardAsync() => _navigation.GoToAsync(Routes.AddCard);

    /// <summary>After the list was dragged into a new order.</summary>
    [RelayCommand]
    private void SaveOrder() => Save();

    /// <summary>A card's own set-up: its sensors, graph or ranking, and its title.</summary>
    [RelayCommand]
    private Task SetUpAsync(DashboardCardOption? card) => card is { CanSetUp: true }
        ? _navigation.GoToAsync(SetUpRoute(card.Widget.WidgetType), new Dictionary<string, object> { [Routes.WidgetIdParameter] = card.Widget.Id })
        : Task.CompletedTask;

    /// <summary>Off the dashboard straight away, its set-up with it - with Undo in the toast rather than asking first.</summary>
    [RelayCommand]
    private void Remove(DashboardCardOption? card)
    {
        if (card is null || !Cards.Contains(card))
        {
            return;
        }

        var before = _toast.Before();
        Cards.Remove(card);
        Save();
        OnPropertyChanged(nameof(IsEmpty));
        _toast.Show($"{card.Title} removed", before);
    }

    /// <summary>Where a card of <paramref name="type"/> is set up.</summary>
    internal static string SetUpRoute(string type) => type switch
    {
        DashboardLayout.Sensors => Routes.PickSensors,
        DashboardLayout.Graph => Routes.PickGraph,
        _ => Routes.TopCardSetUp,
    };

    private void Save()
    {
        DashboardLayout.Save(_settings.Current, Cards.Select(c => c.Widget));
        _settings.Save();
    }

    private void OnLayoutRestored(object? sender, EventArgs e) => Refresh();

    // The outline goes after a moment.
    private void OnToastChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardToast.HighlightId))
        {
            foreach (var card in Cards)
            {
                card.IsHighlighted = card.Widget.Id == _toast.HighlightId;
            }
        }
    }
}
