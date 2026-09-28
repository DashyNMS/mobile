namespace DashyNMS.Mobile.Alerts;

/// <summary>The platform's notifications (Android NotificationManager, iOS UNUserNotificationCenter).</summary>
public interface IAlertNotifier
{
    /// <summary>Asks the user for permission if the platform needs it and hasn't been asked. True when notifications can be shown.</summary>
    Task<bool> RequestPermissionAsync();

    Task ShowAsync(AlertNotification notification);

    /// <summary>Takes away the notification with <paramref name="tag"/>, if it's still showing.</summary>
    void Remove(string tag);
}

/// <summary>
/// Runs <see cref="AlertWatcher.CheckAsync"/> while the app isn't open -
/// WorkManager on Android, background app refresh on iOS. Both decide the
/// exact timing themselves; see the README for what to expect.
/// </summary>
public interface IBackgroundAlertScheduler
{
    /// <summary>Starts background checks, or leaves them as they are if already scheduled.</summary>
    void Schedule();

    void Cancel();
}
