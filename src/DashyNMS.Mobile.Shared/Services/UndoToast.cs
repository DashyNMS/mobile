using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// "Not notified: BGP session down on fw-01 · Undo" (#167): what a change
/// just did, with Undo, rather than asking first - as the dashboard's toast
/// does (#140). One per page that makes such changes.
/// </summary>
public sealed partial class UndoToast : ObservableObject
{
    private Action? _undo;
    private CancellationTokenSource? _showing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShowing))]
    private string? _message;

    /// <summary>How long a toast stays - long enough to reach Undo.</summary>
    internal TimeSpan ShowFor { get; set; } = TimeSpan.FromSeconds(5);

    public bool IsShowing => Message is not null;

    /// <summary>Says what changed, with Undo running <paramref name="undo"/>.</summary>
    public void Show(string message, Action undo)
    {
        _showing?.Cancel();
        var showing = _showing = new CancellationTokenSource();
        _undo = undo;
        Message = message;

        if (ShowFor <= TimeSpan.Zero)
        {
            return;
        }

        var scheduler = SynchronizationContext.Current is null
            ? TaskScheduler.Current
            : TaskScheduler.FromCurrentSynchronizationContext();
        _ = Task.Delay(ShowFor, showing.Token).ContinueWith(_ => Dismiss(), showing.Token, TaskContinuationOptions.OnlyOnRanToCompletion, scheduler);
    }

    [RelayCommand]
    private void Undo()
    {
        var undo = _undo;
        Dismiss();
        undo?.Invoke();
    }

    [RelayCommand]
    public void Dismiss()
    {
        _showing?.Cancel();
        _undo = null;
        Message = null;
    }
}
