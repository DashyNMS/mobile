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
    public DashboardCardOption(DashboardCardKind kind, bool isShown)
    {
        Kind = kind;
        _isShown = isShown;
    }

    public DashboardCardKind Kind { get; }

    public string Title => Kind.Title;

    public string Description => Kind.Description;

    /// <summary>Sensors and Graph have something to choose.</summary>
    public bool CanSetUp => Kind.Type is DashboardLayout.Sensors or DashboardLayout.Graph;

    [ObservableProperty]
    private bool _isShown;
}

/// <summary>
/// Which dashboard cards show, and in what order - the phone's version of
/// desktop's dashboard edit mode, without the resizing a one-column screen
/// doesn't need. Changes save as they're made.
/// </summary>
public sealed partial class CustomiseDashboardViewModel : ViewModelBase
{
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;

    public CustomiseDashboardViewModel(ISettingsStore settings, INavigationService navigation)
    {
        _settings = settings;
        _navigation = navigation;

        // Showing cards first, in their order; then the rest, in the catalogue's.
        var shown = DashboardLayout.Current(settings.Current).Select(w => w.WidgetType).ToList();
        foreach (var type in shown.Concat(DashboardLayout.Kinds.Select(k => k.Type).Except(shown)))
        {
            var option = new DashboardCardOption(DashboardLayout.KindOf(type), shown.Contains(type));
            option.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DashboardCardOption.IsShown))
                {
                    Save();
                }
            };
            Cards.Add(option);
        }
    }

    public ObservableCollection<DashboardCardOption> Cards { get; } = new();

    [RelayCommand]
    private void MoveUp(DashboardCardOption? card) => Move(card, -1);

    [RelayCommand]
    private void MoveDown(DashboardCardOption? card) => Move(card, +1);

    [RelayCommand]
    private Task SetUpAsync(DashboardCardOption? card) => card?.Kind.Type switch
    {
        DashboardLayout.Sensors => _navigation.GoToAsync(Routes.PickSensors),
        DashboardLayout.Graph => _navigation.GoToAsync(Routes.PickGraph),
        _ => Task.CompletedTask,
    };

    /// <summary>Back to the dashboard as it came.</summary>
    [RelayCommand]
    private void ResetToDefaults()
    {
        var order = DashboardLayout.DefaultTypes.Concat(DashboardLayout.Kinds.Select(k => k.Type).Except(DashboardLayout.DefaultTypes)).ToList();
        for (var i = 0; i < order.Count; i++)
        {
            var card = Cards.First(c => c.Kind.Type == order[i]);
            Cards.Move(Cards.IndexOf(card), i);
            card.IsShown = DashboardLayout.DefaultTypes.Contains(card.Kind.Type);
        }

        Save();
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
        DashboardLayout.Save(_settings.Current, Cards.Where(c => c.IsShown).Select(c => c.Kind.Type));
        _settings.Save();
    }
}
