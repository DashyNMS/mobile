using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Devices;

namespace DashyNMS.Mobile.Dashboard;

public sealed record RankChoice(RankBy RankBy, string Label);

/// <summary>
/// Sets up a Top interfaces, Top errors or Top devices card (#103): how many
/// rows, what it ranks by and, for errors, whether error-free ports are left
/// out - desktop's widget options - plus a title of its own. Kept in the
/// card's own <see cref="DashboardWidget"/>, in desktop's fields, so the
/// card is the same in both apps.
/// </summary>
public sealed partial class TopCardSetUpViewModel : ViewModelBase
{
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private int _selectedCount = TopCards.DefaultCount;

    [ObservableProperty]
    private RankChoice _selectedRank;

    [ObservableProperty]
    private bool _hideQuiet = true;

    /// <summary>The card's own title; blank for the kind's ("Top errors").</summary>
    [ObservableProperty]
    private string _cardTitle = string.Empty;

    [ObservableProperty]
    private string _kindTitle = string.Empty;

    [ObservableProperty]
    private bool _isErrors;

    public TopCardSetUpViewModel(ISettingsStore settings, INavigationService navigation)
    {
        _settings = settings;
        _navigation = navigation;
        _selectedRank = Ranks[0];
    }

    /// <summary>Which card this sets up.</summary>
    public string? WidgetId { get; set; }

    public int[] Counts { get; } = [.. TopCards.CountChoices];

    public IReadOnlyList<RankChoice> Ranks { get; } =
    [
        new(RankBy.Total, "In and out together"),
        new(RankBy.In, "In"),
        new(RankBy.Out, "Out"),
    ];

    // A picker whose choices are being refilled briefly sends back null.
    partial void OnSelectedRankChanged(RankChoice value)
    {
        if (value is null)
        {
            SelectedRank = Ranks[0];
        }
    }

    /// <summary>Carries on from the card's set-up so far.</summary>
    public void Load()
    {
        if (Widget() is not { } widget)
        {
            return;
        }

        KindTitle = DashboardLayout.KindOf(widget.WidgetType).Title;
        IsErrors = widget.WidgetType == DashboardLayout.TopErrors;
        SelectedCount = TopCards.CountOf(widget);
        SelectedRank = Ranks.First(r => r.RankBy == widget.TopRankBy);
        HideQuiet = widget.TopHideQuiet;
        CardTitle = DashboardLayout.HasOwnTitle(widget) ? widget.Title : string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Widget() is { } widget)
        {
            widget.TopCount = SelectedCount;
            widget.TopRankBy = SelectedRank.RankBy;
            widget.TopHideQuiet = HideQuiet;
            widget.Title = string.IsNullOrWhiteSpace(CardTitle) ? DashboardLayout.KindOf(widget.WidgetType).Title : CardTitle.Trim();
            _settings.Save();
        }

        await _navigation.GoToAsync(Routes.Back);
    }

    /// <summary>The card being set up, or null if it has gone - then there's nothing to save.</summary>
    private DashboardWidget? Widget() =>
        DashboardLayout.Find(_settings.Current, WidgetId) is { } widget && TopCards.IsTop(widget.WidgetType) ? widget : null;
}
