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
    public CardChoice(DashboardCardKind kind, int onDashboard, string? unavailableReason)
    {
        Kind = kind;
        OnDashboard = onDashboard;
        UnavailableReason = unavailableReason;
    }

    public DashboardCardKind Kind { get; }

    public string Name => Kind.Title;

    public CardPreview Preview => Kind.Preview;

    /// <summary>How many are on the dashboard already.</summary>
    public int OnDashboard { get; }

    /// <summary>Why it can't be added ("On your dashboard"), or null when it can.</summary>
    public string? UnavailableReason { get; }

    public bool IsAvailable => UnavailableReason is null;

    /// <summary>
    /// What it shows - or why it can't be added, or that there's one already
    /// and another can go with it.
    /// </summary>
    public string Subtitle => UnavailableReason
        ?? (OnDashboard > 0 ? string.Create(CultureInfo.CurrentCulture, $"{OnDashboard} on your dashboard · add another") : Kind.Description);

    /// <summary>Some already, and room for more - the subtitle's in the accent.</summary>
    public bool HasSome => IsAvailable && OnDashboard > 0;

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
/// The new card goes at the bottom, announced with Undo and outlined there
/// for a moment. Sensors and Graph cards go straight on to their set-up,
/// having nothing to show without it; a Top card shows at once.
/// </remarks>
public sealed partial class AddCardViewModel : ViewModelBase
{
    public const string AllCategory = "All";

    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly DashboardToast _toast;
    private readonly DeviceBookmarks _bookmarks;
    private IReadOnlyList<CardChoice> _catalogue = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddText), nameof(CanAdd))]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private CardChoice? _selected;

    private string _category = AllCategory;

    public AddCardViewModel(ISettingsStore settings, INavigationService navigation, DashboardToast toast, DeviceBookmarks bookmarks)
    {
        _settings = settings;
        _navigation = navigation;
        _toast = toast;
        _bookmarks = bookmarks;
        Load();
    }

    public BulkObservableCollection<CardCategory> Categories { get; } = new();

    public BulkObservableCollection<CardChoice> Cards { get; } = new();

    public bool HasNoMatches => Cards.Count == 0;

    public bool CanAdd => Selected is { IsAvailable: true };

    /// <summary>"Add Top errors" - the button names what it adds.</summary>
    public string AddText => Selected is { } choice ? "Add " + choice.Name : "Choose a card";

    /// <summary>Every card, as the dashboard now stands.</summary>
    public void Load()
    {
        var settings = _settings.Current;
        _catalogue = DashboardLayout.Kinds
            .Select(kind => new CardChoice(kind, DashboardLayout.CountOf(settings, kind.Type), Unavailable(kind, settings)))
            .ToList();

        Categories.ReplaceAll(new[] { new CardCategory(AllCategory, _catalogue.Count) }
            .Concat(DashboardLayout.Categories.Select(c => new CardCategory(c, _catalogue.Count(k => k.Kind.Category == c))))
            .ToList());
        Refilter();
    }

    partial void OnSearchTextChanged(string value) => Refilter();

    partial void OnSelectedChanged(CardChoice? oldValue, CardChoice? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
        }
    }

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

    /// <summary>A tap picks it; a card that can't be added stays unpicked.</summary>
    [RelayCommand]
    private void Pick(CardChoice? choice)
    {
        if (choice is { IsAvailable: true })
        {
            Selected = choice;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        if (Selected is not { IsAvailable: true } choice)
        {
            return;
        }

        var before = _toast.Before();
        var widget = DashboardLayout.Add(_settings.Current, choice.Kind.Type);
        _settings.Save();
        _toast.Show($"{choice.Name} added at the bottom", before, widget.Id);

        await _navigation.GoToAsync(Routes.Back);
        if (widget.WidgetType is DashboardLayout.Sensors or DashboardLayout.Graph)
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
        : null;

    private void Refilter()
    {
        foreach (var category in Categories)
        {
            category.IsSelected = category.Name == _category;
        }

        var term = SearchText.Trim();
        Cards.ReplaceAll(_catalogue
            .Where(c => _category == AllCategory || c.Kind.Category == _category)
            .Where(c => term.Length == 0 || c.Matches(term))
            .ToList());

        // Keep the pick if it's still showing, otherwise the first that can be added - as desktop's.
        if (Selected is null || !Cards.Contains(Selected))
        {
            Selected = Cards.FirstOrDefault(c => c.IsAvailable);
        }

        OnPropertyChanged(nameof(HasNoMatches));
    }
}
