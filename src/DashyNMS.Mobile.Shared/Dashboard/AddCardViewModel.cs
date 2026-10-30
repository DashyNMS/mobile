using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Dashboard;

/// <summary>One card in the card picker: what it's called, what it shows, and whether it can be added now.</summary>
public sealed partial class CardChoice : ObservableObject
{
    public CardChoice(DashboardCardKind kind, int onDashboard, string? unavailableReason, string? suggestedBecause = null)
    {
        Kind = kind;
        OnDashboard = onDashboard;
        UnavailableReason = unavailableReason;
        SuggestedBecause = unavailableReason is null && onDashboard == 0 ? suggestedBecause : null;
    }

    public DashboardCardKind Kind { get; }

    public string Name => Kind.Title;

    public CardPreview Preview => Kind.Preview;

    /// <summary>How many are on the dashboard already.</summary>
    public int OnDashboard { get; }

    /// <summary>Why it can't be added ("On your dashboard"), or null when it can.</summary>
    public string? UnavailableReason { get; }

    public bool IsAvailable => UnavailableReason is null;

    /// <summary>Why it fits your set-up ("Graylog is connected"), as desktop's Suggested (#145); null when it isn't suggested.</summary>
    public string? SuggestedBecause { get; }

    public bool IsSuggested => SuggestedBecause is not null;

    /// <summary>
    /// What it shows - or why it can't be added, why it's suggested, or that
    /// there's one already and another can go with it.
    /// </summary>
    public string Subtitle => UnavailableReason
        ?? SuggestedBecause
        ?? (OnDashboard > 0 ? string.Create(CultureInfo.CurrentCulture, $"{OnDashboard} on your dashboard · add another") : Kind.Description);

    /// <summary>Some already, and room for more - the subtitle's in the accent.</summary>
    public bool HasSome => IsAvailable && OnDashboard > 0;

    /// <summary>The subtitle's in the accent: there are some already, or it's suggested.</summary>
    public bool IsAccented => HasSome || IsSuggested;

    [ObservableProperty]
    private bool _isSelected;

    public bool Matches(string term) =>
        Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
        || Kind.Description.Contains(term, StringComparison.CurrentCultureIgnoreCase)
        || Kind.Category.Contains(term, StringComparison.CurrentCultureIgnoreCase)
        || (Kind.DesktopTitle?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false);
}

/// <summary>A category chip in the card picker, with how many cards it holds.</summary>
public sealed partial class CardCategory : ObservableObject
{
    public CardCategory(string name, int count)
    {
        Name = name;
        Count = count;
    }

    public string Name { get; }

    public int Count { get; }

    /// <summary>"Traffic 3" - the chip.</summary>
    public string Label => string.Create(CultureInfo.CurrentCulture, $"{Name} {Count}");

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Add card (#140): desktop's widget picker (its #204) on a phone - every
/// card, filed by desktop's categories as chips, each with a description and
/// a hint of its shape; searchable; and a card that can't be added greyed out
/// with the reason. A card already on the dashboard can't be added again,
/// unless it's one of those there can be several of.
/// </summary>
/// <remarks>
/// As desktop's since its #277 (#145): tick several to add them together,
/// and a Suggested category picks out what fits your set-up. The new cards go
/// where the picker was opened for - below a card on Edit dashboard, else at
/// the bottom - announced with Undo, the first outlined for a moment. A
/// single Sensors or Graph card goes straight on to its set-up, having
/// nothing to show without it; a Top card shows at once.
/// </remarks>
public sealed partial class AddCardViewModel : ViewModelBase
{
    public const string AllCategory = "All";

    /// <summary>Desktop's: cards that fit your set-up and aren't on the dashboard yet.</summary>
    public const string SuggestedCategory = "Suggested";

    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly DashboardToast _toast;
    private readonly DeviceBookmarks _bookmarks;
    private IReadOnlyList<CardChoice> _catalogue = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    private string _category = AllCategory;

    /// <summary>The ticked cards, in the order they were ticked - the order they're added in.</summary>
    private readonly List<CardChoice> _ticked = [];

    private readonly DesktopNMS.Core.Services.IDashboardLayoutService _layout;

    public AddCardViewModel(ISettingsStore settings, INavigationService navigation, DashboardToast toast, DeviceBookmarks bookmarks, DesktopNMS.Core.Services.IDashboardLayoutService? layout = null)
    {
        _settings = settings;
        _layout = layout ?? new DesktopNMS.Core.Services.DashboardLayoutService(settings);
        _navigation = navigation;
        _toast = toast;
        _bookmarks = bookmarks;
        Load();
    }

    public BulkObservableCollection<CardCategory> Categories { get; } = new();

    public BulkObservableCollection<CardChoice> Cards { get; } = new();

    public bool HasNoMatches => Cards.Count == 0;

    /// <summary>Where the cards go among those shown, counted from the top; null for the bottom.</summary>
    public int? Position { get; set; }

    /// <summary>The ticked cards, in the order they were ticked.</summary>
    public IReadOnlyList<CardChoice> Ticked => _ticked;

    public bool CanAdd => _ticked.Count > 0;

    /// <summary>"Add Top errors", "Add 3 cards" - the button says what it adds.</summary>
    public string AddText => _ticked.Count switch
    {
        0 => "Choose cards",
        1 => "Add " + _ticked[0].Name,
        var n => string.Create(CultureInfo.CurrentCulture, $"Add {n} cards"),
    };

    /// <summary>Every card, as the dashboard now stands, with nothing ticked - on Suggested when there's something to suggest.</summary>
    public void Load()
    {
        var settings = _settings.Current;
        _catalogue = DashboardLayout.Kinds
            .Select(kind => new CardChoice(kind, DashboardLayout.CountOf(settings, kind.Type), Unavailable(kind, settings), Suggestion(kind, settings)))
            .ToList();
        _ticked.Clear();

        var suggested = _catalogue.Count(c => c.IsSuggested);
        Categories.ReplaceAll(new[] { new CardCategory(AllCategory, _catalogue.Count) }
            .Concat(suggested > 0 ? [new CardCategory(SuggestedCategory, suggested)] : [])
            .Concat(DashboardLayout.Categories.Select(c => new CardCategory(c, _catalogue.Count(k => k.Kind.Category == c))))
            .ToList());
        _category = suggested > 0 ? SuggestedCategory : AllCategory;
        Refilter();
        TickedChanged();
    }

    partial void OnSearchTextChanged(string value) => Refilter();

    [RelayCommand]
    private void ChooseCategory(CardCategory? category)
    {
        if (category is null)
        {
            return;
        }

        _category = category.Name;
        Refilter();
    }

    /// <summary>A tap ticks it, or unticks it; a card that can't be added stays unticked.</summary>
    [RelayCommand]
    private void Pick(CardChoice? choice)
    {
        if (choice is not { IsAvailable: true })
        {
            return;
        }

        choice.IsSelected = !choice.IsSelected;
        if (choice.IsSelected)
        {
            _ticked.Add(choice);
        }
        else
        {
            _ticked.Remove(choice);
        }

        TickedChanged();
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        if (_ticked.Count == 0)
        {
            return;
        }

        var before = _toast.Before();
        var added = new List<DashboardWidget>();
        foreach (var choice in _ticked)
        {
            // A card added (#219): just that one was - never which.
            Diagnostics.FeatureUse.Count(Diagnostics.MetricArea.CardAdded);
            added.Add(DashboardLayout.Add(_settings.Current, choice.Kind.Type, Position is { } at ? at + added.Count : null));
        }

        _layout.Restore(_settings.Current.DashboardWidgets.ToList());

        var what = added.Count == 1 ? _ticked[0].Name : string.Create(CultureInfo.CurrentCulture, $"{added.Count} cards");
        _toast.Show(what + (Position is null ? " added at the bottom" : " added"), before, added[0].Id);

        await _navigation.GoToAsync(Routes.Back);
        if (added is [{ WidgetType: DashboardLayout.Sensors or DashboardLayout.Graph } widget])
        {
            await _navigation.GoToAsync(
                CustomiseDashboardViewModel.SetUpRoute(widget.WidgetType),
                new Dictionary<string, object> { [Routes.WidgetIdParameter] = widget.Id });
        }
    }

    [RelayCommand]
    private Task CancelAsync() => _navigation.GoToAsync(Routes.Back);

    private string? Unavailable(DashboardCardKind kind, AppSettings settings) =>
        !kind.AllowsSeveral && DashboardLayout.CountOf(settings, kind.Type) > 0 ? "On your dashboard"
        : kind.Type == DashboardLayout.PinnedDevices && !_bookmarks.PinningEnabled ? "Turn on pinned devices in Settings, Devices first"
        : kind.Type == DashboardLayout.Graylog && !IsGraylogSetUp(settings) ? "Set up Graylog in Settings first"
        : null;

    /// <summary>Desktop's suggestions: pinned devices once you've pinned some, Graylog once it's connected.</summary>
    private static string? Suggestion(DashboardCardKind kind, AppSettings settings) => kind.Type switch
    {
        DashboardLayout.PinnedDevices when settings.PinnedDevices.Count is var n and > 0 =>
            string.Create(CultureInfo.CurrentCulture, $"You've pinned {n} device{(n == 1 ? string.Empty : "s")}"),
        DashboardLayout.Graylog when IsGraylogSetUp(settings) => "Graylog is connected",
        _ => null,
    };

    /// <summary>Switched on with a server - what Graylog's set-up needs before a card can show anything.</summary>
    private static bool IsGraylogSetUp(AppSettings settings) =>
        settings.Graylog is { Enabled: true, Server: { } server } && !string.IsNullOrWhiteSpace(server);

    private void TickedChanged()
    {
        OnPropertyChanged(nameof(AddText));
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(Ticked));
        AddCommand.NotifyCanExecuteChanged();
    }

    private void Refilter()
    {
        foreach (var category in Categories)
        {
            category.IsSelected = category.Name == _category;
        }

        // Ticks stay when the category or search changes, so cards from
        // several categories can go in together.
        var term = SearchText.Trim();
        Cards.ReplaceAll(_catalogue
            .Where(c => _category == AllCategory
                || (_category == SuggestedCategory ? c.IsSuggested : c.Kind.Category == _category))
            .Where(c => term.Length == 0 || c.Matches(term))
            .ToList());

        OnPropertyChanged(nameof(HasNoMatches));
    }
}
