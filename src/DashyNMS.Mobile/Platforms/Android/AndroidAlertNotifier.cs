using Android.App;
using Android.Content;
using AndroidX.Core.App;
using DashyNMS.Mobile.Alerts;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile;

/// <summary>
/// Alert notifications on Android, one channel per kind so each can be
/// tuned (sound, vibration, pop-on-screen) in the system settings - Android's
/// equivalent of desktop's per-severity notification options.
/// </summary>
public sealed class AndroidAlertNotifier : IAlertNotifier
{
    /// <summary>Intent extras MainActivity reads when a notification is tapped.</summary>
    internal const string ExtraFromNotification = "net.pckp.dashynms.fromNotification";
    internal const string ExtraDeviceId = "net.pckp.dashynms.deviceId";

    private const string CriticalChannel = "alerts-critical";
    private const string WarningChannel = "alerts-warning";
    private const string UpdatesChannel = "alerts-updates";

    private static Context AppContext => global::Android.App.Application.Context;

    public async Task<bool> RequestPermissionAsync()
    {
        EnsureChannels();

        // Android 13 made notifications a runtime permission; before that
        // they're allowed unless the user turned them off.
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.PostNotifications>);
            if (status != PermissionStatus.Granted)
            {
                return false;
            }
        }

        return NotificationManagerCompat.From(AppContext)!.AreNotificationsEnabled();
    }

    public Task ShowAsync(AlertNotification notification)
    {
        EnsureChannels();

        var manager = NotificationManagerCompat.From(AppContext)!;
        if (!manager.AreNotificationsEnabled())
        {
            return Task.CompletedTask;
        }

        var text = notification.Detail is null ? notification.Body : notification.Body + "\n" + notification.Detail;

        // Each builder call returns the same builder; set one by one rather
        // than chain, as the bindings declare every return as nullable.
        var builder = new NotificationCompat.Builder(AppContext, ChannelFor(notification));
        builder.SetSmallIcon(Resource.Drawable.ic_stat_dashynms);
        builder.SetContentTitle(notification.Title);
        builder.SetContentText(notification.Body);
        builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(text));
        builder.SetColor(ColourFor(notification.Severity));
        builder.SetCategory(NotificationCompat.CategoryStatus);
        builder.SetPriority(notification.IsProblem && notification.Severity == AlertSeverity.Critical
            ? NotificationCompat.PriorityHigh
            : NotificationCompat.PriorityDefault);
        builder.SetAutoCancel(true);
        builder.SetContentIntent(TapIntent(notification));

        try
        {
            // Tagged, so a later notification for the same alert replaces this one.
            manager.Notify(notification.Tag, 0, builder.Build()!);
        }
        catch (Java.Lang.SecurityException)
        {
            // Permission withdrawn between the check and now.
        }

        return Task.CompletedTask;
    }

    public void Remove(string tag) => NotificationManagerCompat.From(AppContext)!.Cancel(tag, 0);

    private static PendingIntent TapIntent(AlertNotification notification)
    {
        var intent = new Intent(AppContext, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        intent.PutExtra(ExtraFromNotification, true);
        if (notification.DeviceId is { } deviceId)
        {
            intent.PutExtra(ExtraDeviceId, deviceId);
        }

        // One request code per tag, or every notification would share (and
        // overwrite) a single PendingIntent's extras.
        return PendingIntent.GetActivity(
            AppContext,
            notification.Tag.GetHashCode(StringComparison.Ordinal),
            intent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
    }

    private static string ChannelFor(AlertNotification notification) =>
        !notification.IsProblem ? UpdatesChannel
        : notification.Severity == AlertSeverity.Critical ? CriticalChannel
        : WarningChannel;

    private static int ColourFor(AlertSeverity severity) => global::Android.Graphics.Color.ParseColor(severity switch
    {
        AlertSeverity.Critical => "#DA3633",
        AlertSeverity.Warning => "#DB9A04",
        AlertSeverity.Ok => "#2EA043",
        _ => "#3B82F6",
    }).ToArgb();

    /// <summary>Creating a channel that already exists is a no-op, so this is safe to call every time.</summary>
    private static void EnsureChannels()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var manager = (NotificationManager?)AppContext.GetSystemService(Context.NotificationService);
        if (manager is null)
        {
            return;
        }

        manager.CreateNotificationChannel(new NotificationChannel(CriticalChannel, "Critical alerts", NotificationImportance.High)
        {
            Description = "New and reopened critical alerts.",
        });
        manager.CreateNotificationChannel(new NotificationChannel(WarningChannel, "Warnings", NotificationImportance.Default)
        {
            Description = "New and reopened warnings.",
        });
        manager.CreateNotificationChannel(new NotificationChannel(UpdatesChannel, "Alert updates", NotificationImportance.Low)
        {
            Description = "Alerts recovering or being acknowledged.",
        });
    }
}
