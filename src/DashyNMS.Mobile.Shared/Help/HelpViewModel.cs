using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Help;

namespace DashyNMS.Mobile.Help;

/// <summary>A row of the contents list, or of a search's results.</summary>
/// <param name="summary">For a result, the heading it's under and the words that match.</param>
/// <param name="group">On a group's first row, the group's name, such as "Everyday tasks".</param>
public sealed partial class HelpRow(HelpTopic topic, string title, string summary = "", string? group = null, bool hasLine = false) : ObservableObject
{
    /// <summary>Showing beside the list, where there's room for both.</summary>
    [ObservableProperty]
    private bool _isCurrent;

    public HelpTopic Topic => topic;

    public string Title => title;

    public string Summary => summary;

    public string? Group => group;

    public bool HasGroup => !string.IsNullOrEmpty(group);

    /// <summary>Rows have a line above them, except the first and a group's first.</summary>
    public bool HasLine => hasLine;
}

/// <summary>
/// Help's contents list and search (#668): the user guide for the version
/// that's running. Searching runs on the phone.
/// </summary>
public sealed partial class HelpViewModel : ViewModelBase
{
    private const int MaxResults = 20;

    private readonly HelpGuideService _guides;
    private readonly INavigationService _navigation;
    private readonly IHelpRefreshPolicy _policy;
    private SynchronizationContext? _ui;
    private bool _attached;
    private string? _currentPageId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearching))]
    [NotifyPropertyChangedFor(nameof(ResultsText))]
    [NotifyPropertyChangedFor(nameof(ShowNoResults))]
    private string _searchText = string.Empty;

    public HelpViewModel(HelpGuideService guides, INavigationService navigation, IHelpRefreshPolicy policy)
    {
        _guides = guides;
        _navigation = navigation;
        _policy = policy;
        Show();
    }

    /// <summary>The contents list, or the results while searching.</summary>
    public IReadOnlyList<HelpRow> Rows { get; private set; } = [];

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    /// <summary>Nothing could be read at all, which only a damaged build would do.</summary>
    public bool IsEmpty => !IsSearching && Rows.Count == 0;

    public bool ShowNoResults => IsSearching && Rows.Count == 0;

    /// <summary>Beneath the title while searching, and announced as it changes.</summary>
    public string ResultsText => !IsSearching ? string.Empty : Rows.Count switch
    {
        0 => $"Nothing in the guide matches “{SearchText.Trim()}”.",
        1 => $"1 match for “{SearchText.Trim()}”",
        var count => $"{count} matches for “{SearchText.Trim()}”",
    };

    /// <summary>Which version the help describes, and when it was last refreshed if it has been.</summary>
    public string ReleaseText
    {
        get
        {
            var shown = _guides.Shown;
            var release = shown.Guide.Release is { } known ? $"Help for DashyNMS Mobile {known.Version}" : "Help for this build of DashyNMS Mobile";
            return shown is { Source: HelpGuideSource.Downloaded, TakenAt: { } taken }
                ? $"{release}, updated {taken.ToLocalTime():d MMMM yyyy}"
                : $"{release}, saved in the app";
        }
    }

    /// <summary>
    /// The page is on screen: follow newer pages, and look for them in the
    /// background. Core decides whether one is due.
    /// </summary>
    public void Appearing()
    {
        _ui = SynchronizationContext.Current;
        if (!_attached)
        {
            _attached = true;
            _guides.Changed += OnGuideChanged;
        }

        Show();
        _ = RefreshAsync();
    }

    public void Disappearing()
    {
        _attached = false;
        _guides.Changed -= OnGuideChanged;
    }

    [RelayCommand]
    private Task OpenAsync(HelpRow row) => _navigation.OpenHelpAsync(row.Topic);

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    /// <summary>For the detail pane, which starts at the front page.</summary>
    public HelpTopic? Home => _guides.Current.Home is { } home ? new HelpTopic(home.Id) : null;

    /// <summary>The page showing beside the list, or null when there's no room or nothing open.</summary>
    public string? CurrentPageId
    {
        get => _currentPageId;
        set
        {
            _currentPageId = value;
            MarkCurrent();
        }
    }

    private void MarkCurrent()
    {
        foreach (var row in Rows)
        {
            row.IsCurrent = row.Topic.Anchor is null && string.Equals(row.Topic.PageId, _currentPageId, StringComparison.OrdinalIgnoreCase);
        }
    }

    partial void OnSearchTextChanged(string value) => Show();

    private void Show()
    {
        var guide = _guides.Current;
        Rows = IsSearching ? Results(guide) : Contents(guide);
        MarkCurrent();
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ShowNoResults));
        OnPropertyChanged(nameof(ResultsText));
        OnPropertyChanged(nameof(ReleaseText));
    }

    private static List<HelpRow> Contents(HelpGuide guide) => guide.Contents
        .SelectMany(group => group.Entries.Select((entry, index) => new HelpRow(new HelpTopic(entry.PageId), entry.Title, group: index == 0 ? group.Name : null, hasLine: index > 0)))
        .ToList();

    private List<HelpRow> Results(HelpGuide guide) => guide.Search(SearchText, MaxResults)
        .Select((result, index) => new HelpRow(
            new HelpTopic(result.Page.Id, result.Heading?.Anchor),
            result.Page.Title,
            string.Join("\n", new[] { result.Heading?.Text, result.Passage }.Where(part => !string.IsNullOrEmpty(part))),
            hasLine: index > 0))
        .ToList();

    private async Task RefreshAsync()
    {
        try
        {
            // A copy saved earlier needs no network, so it's used even where refreshing isn't allowed.
            await _guides.InitialiseAsync();
            if (_policy.MayRefresh)
            {
                await _guides.RefreshAsync();
            }

            // Also when the pages are the same: they're now known to be.
            OnPropertyChanged(nameof(ReleaseText));
        }
        catch (OperationCanceledException)
        {
            // The app is closing; the pages already shown stay.
        }
    }

    /// <summary>Raised on the thread that refreshed it.</summary>
    private void OnGuideChanged(object? sender, EventArgs e)
    {
        if (_ui is { } ui)
        {
            ui.Post(_ => { if (_attached) Show(); }, null);
        }
        else if (_attached)
        {
            Show();
        }
    }
}
