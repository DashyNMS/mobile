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

    public bool IsNotBusy => !IsBusy;

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
