using Android.Content;
using Android.Runtime;
using AndroidX.Work;
using DashyNMS.Mobile.Alerts;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile;

/// <summary>
/// The work WorkManager runs for <see cref="AndroidAlertScheduler"/>. The
/// process may have been started just for this, but MainApplication (a
/// MauiApplication) has built the app's services before any work runs.
/// </summary>
/// <remarks>
/// A fixed Java name: WorkManager stores the class name with the scheduled
/// work, so it must survive app updates rather than follow the generated one.
/// </remarks>
[Register("net/pckp/dashynms/AlertCheckWorker")]
public sealed class AlertCheckWorker : Worker
{
    public AlertCheckWorker(Context context, WorkerParameters workerParams)
        : base(context, workerParams)
    {
    }

    // Runs on a WorkManager background thread, so blocking here is expected.
    public override Result DoWork()
    {
        var watcher = IPlatformApplication.Current?.Services.GetService<AlertWatcher>();
        if (watcher is null)
        {
            return Result.InvokeRetry()!;
        }

        var result = watcher.CheckAsync().GetAwaiter().GetResult();
        return (result.Succeeded ? Result.InvokeSuccess() : Result.InvokeRetry())!;
    }
}
