using CommunityToolkit.Mvvm.ComponentModel;
using DesktopNMS.Core.Api;

namespace DashyNMS.Mobile.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>How long typing has to pause before a search box filters - see <see cref="WhenTypingPauses"/>.</summary>
    internal static TimeSpan DefaultSearchDelay { get; set; } = TimeSpan.FromMilliseconds(250);

    private CancellationTokenSource? _typing;

    /// <summary>This view model's <see cref="DefaultSearchDelay"/>; zero filters on every keystroke.</summary>
    internal TimeSpan SearchDelay { get; set; } = DefaultSearchDelay;

    public bool IsNotBusy => !IsBusy;

    /// <summary>
    /// Runs <paramref name="apply"/> once typing pauses, on the thread that
    /// typed. Filtering (and reloading the list) on every keystroke made
    /// searching a few hundred devices lag behind the keyboard.
    /// </summary>
    protected void WhenTypingPauses(Action apply)
    {
        _typing?.Cancel();
        if (SearchDelay <= TimeSpan.Zero)
        {
            _typing = null;
            apply();
            return;
        }

        var typing = _typing = new CancellationTokenSource();
        var scheduler = SynchronizationContext.Current is null
            ? TaskScheduler.Current
            : TaskScheduler.FromCurrentSynchronizationContext();

        _ = Task.Delay(SearchDelay, typing.Token).ContinueWith(
            _ => apply(),
            typing.Token,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            scheduler);
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// Runs <paramref name="work"/> with <see cref="IsBusy"/> set, turning a
    /// failure into <see cref="ErrorMessage"/> rather than an unhandled
    /// exception. Returns false when it failed.
    /// </summary>
    protected async Task<bool> RunAsync(Func<Task> work)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await work();
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = Describe(ex);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal static string Describe(Exception ex) => ex switch
    {
        LibreNmsApiException api => api.Message,
        GraylogApiException graylog => graylog.ToUserMessage(),
        HttpRequestException => "Couldn't reach the LibreNMS server. Check your connection and try again.",
        TimeoutException => "LibreNMS took too long to answer.",
        _ => ex.Message,
    };
}
