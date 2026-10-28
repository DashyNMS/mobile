using BackgroundTasks;
using DashyNMS.Mobile.Alerts;
using Foundation;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile;

/// <summary>
/// Background alert checks through background app refresh. iOS decides when
/// (if ever) each one runs - typically when the phone thinks you're likely
/// to open the app - so this is best effort; asking for 15 minutes is only
/// the earliest it may happen. Each run schedules the next (see AppDelegate).
/// </summary>
public sealed class IosAlertScheduler : IBackgroundAlertScheduler
{
    /// <summary>Also listed under BGTaskSchedulerPermittedIdentifiers in Info.plist.</summary>
    public const string TaskId = "net.pckp.DashyNMS.alert-check";

    private readonly ILogger<IosAlertScheduler> _logger;

    public IosAlertScheduler(ILogger<IosAlertScheduler> logger) => _logger = logger;

    public void Schedule()
    {
        var request = new BGAppRefreshTaskRequest(TaskId)
        {
            EarliestBeginDate = NSDate.FromTimeIntervalSinceNow(TimeSpan.FromMinutes(15).TotalSeconds),
        };

        // Replaces any request already pending for this id. Fails on the
        // simulator, which has no background app refresh. iOS 27 replaced
        // the synchronous call with one that reports every failure through
        // a completion handler (#95); iOS 15 to 26 only have the old one.
        if (OperatingSystem.IsIOSVersionAtLeast(27) || OperatingSystem.IsMacCatalystVersionAtLeast(27))
        {
            BGTaskScheduler.Shared.Submit(request, error =>
            {
                if (error is not null)
                {
                    LogFailure(error);
                }
            });
        }
        else if (!BGTaskScheduler.Shared.Submit(request, out var error))
        {
            LogFailure(error);
        }
    }

    private void LogFailure(NSError? error) =>
        _logger.LogWarning("Could not schedule background alert checks: {Error}", error?.LocalizedDescription);

    public void Cancel() => BGTaskScheduler.Shared.Cancel(TaskId);
}
