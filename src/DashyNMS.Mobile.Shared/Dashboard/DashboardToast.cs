using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Dashboard;

/// <summary>
/// What just changed on the dashboard, said in a toast with Undo (#140):
/// "Wireless removed", "Top errors added", "Starter dashboard added". One
/// for the app, so a card added from the card picker is still announced on
/// the page it goes back to - and outlined there for a moment, as desktop
/// outlines a widget just added from its picker.
/// </summary>
/// <remarks>
/// Undo puts the whole card list back as it was, set-up and all, which is
/// why removing a card no longer asks first.
/// </remarks>
public sealed partial class DashboardToast : ObservableObject
{
    private readonly ISettingsStore _settings;
    private IReadOnlyList<DashboardWidget>? _before;
    private CancellationTokenSource? _showing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShowing))]
    private string? _message;

    /// <summary>The card just added, outlined for a moment where it lands.</summary>
    [ObservableProperty]
    private string? _highlightId;

    public DashboardToast(ISettingsStore settings)
    {
        _settings = settings;
    }

    /// <summary>How long a toast stays - long enough to reach Undo.</summary>
    internal TimeSpan ShowFor { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a new card stays outlined.</summary>
    internal TimeSpan HighlightFor { get; set; } = TimeSpan.FromSeconds(2.5);

    public bool IsShowing => Message is not null;

    /// <summary>Undo put the cards back: pages showing them read them again.</summary>
    public event EventHandler? LayoutRestored;

    /// <summary>
    /// The list as it is, before a change - call, make the change, then
    /// <see cref="Show"/>.
    /// </summary>
    public IReadOnlyList<DashboardWidget> Before() => DashboardLayout.Snapshot(_settings.Current);

    /// <summary>Says what changed, with Undo back to <paramref name="before"/>.</summary>
    public void Show(string message, IReadOnlyList<DashboardWidget> before, string? highlightId = null)
    {
        _showing?.Cancel();
        var showing = _showing = new CancellationTokenSource();

        _before = before;
        Message = message;
        HighlightId = highlightId;

        After(HighlightFor, showing.Token, () => HighlightId = null);
        After(ShowFor, showing.Token, Dismiss);
    }

    [RelayCommand]
    private void Undo()
    {
        if (_before is not { } before)
        {
            return;
        }

        DashboardLayout.Restore(_settings.Current, before);
        _settings.Save();
        Dismiss();
        LayoutRestored?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Dismiss()
    {
        _showing?.Cancel();
        _before = null;
        Message = null;
        HighlightId = null;
    }

    /// <summary>Runs <paramref name="action"/> later on the thread that asked, unless a newer toast came first.</summary>
    private static void After(TimeSpan delay, CancellationToken token, Action action)
    {
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        var scheduler = SynchronizationContext.Current is null
            ? TaskScheduler.Current
            : TaskScheduler.FromCurrentSynchronizationContext();

        _ = Task.Delay(delay, token).ContinueWith(_ => action(), token, TaskContinuationOptions.OnlyOnRanToCompletion, scheduler);
    }
}
