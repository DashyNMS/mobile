using AndroidX.Work;
using DashyNMS.Mobile.Alerts;

namespace DashyNMS.Mobile;

/// <summary>
/// Background alert checks through WorkManager: every 15 minutes (Android's
/// minimum for periodic work), only with a network, and deferred further by
/// Doze and battery saver as Android sees fit.
/// </summary>
public sealed class AndroidAlertScheduler : IBackgroundAlertScheduler
{
    private const string WorkName = "dashynms-alert-check";

    public void Schedule()
    {
        var constraints = new Constraints.Builder()
            .SetRequiredNetworkType(NetworkType.Connected!)
            .Build();

        var request = new PeriodicWorkRequest.Builder(typeof(AlertCheckWorker), TimeSpan.FromMinutes(15))
            .SetConstraints(constraints)
            .Build();

        // Keep: re-scheduling on every launch mustn't restart the 15-minute clock.
        WorkManager.GetInstance(global::Android.App.Application.Context)
            .EnqueueUniquePeriodicWork(WorkName, ExistingPeriodicWorkPolicy.Keep!, request);
    }

    public void Cancel() =>
        WorkManager.GetInstance(global::Android.App.Application.Context).CancelUniqueWork(WorkName);
}
