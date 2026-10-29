using DashyNMS.Mobile.Alerts;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using UserNotifications;

namespace DashyNMS.Mobile;

/// <summary>Alert notifications on iOS, as local notifications.</summary>
public sealed class IosAlertNotifier : IAlertNotifier
{
    /// <summary>Notification user info key carrying the device id, for tap handling.</summary>
    internal const string DeviceIdKey = "deviceId";
    internal const string AlertIdKey = "alertId"; // Previous app versions only.
    internal const string AlertKeyKey = "alertKey";
    internal const string AlertGroupKey = "alertGroupKey";

    public async Task<bool> RequestPermissionAsync()
    {
        // iOS only shows the prompt the first time; after that this just
        // reports the user's choice.
        var (granted, _) = await UNUserNotificationCenter.Current.RequestAuthorizationAsync(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge);
        return granted;
    }

    public async Task ShowAsync(AlertNotification notification)
    {
        var content = new UNMutableNotificationContent
        {
            Title = notification.Title,
            Body = notification.Detail is null ? notification.Body : notification.Body + "\n" + notification.Detail,
            ThreadIdentifier = "alerts",
        };

        // A partial recovery or additional fault updates the existing group
        // in the notification list without interrupting the user again.
        if (notification.IsUpdate)
        {
            content.InterruptionLevel = UNNotificationInterruptionLevel.Passive;
        }

        // Recoveries and acknowledgements arrive silently, as on desktop.
        if (notification.IsProblem && !notification.IsUpdate)
        {
            content.Sound = UNNotificationSound.Default;
        }

        var userInfo = new NSMutableDictionary();
        if (notification.DeviceId is { } deviceId)
        {
            userInfo[new NSString(DeviceIdKey)] = NSNumber.FromInt32(deviceId);
        }

        if (notification.AlertId is { } alertId)
        {
            userInfo[new NSString(AlertIdKey)] = NSNumber.FromInt32(alertId);
        }

        if (notification.Key is { } key) userInfo[new NSString(AlertKeyKey)] = new NSString(key.ToString());
        if (notification.Group is { } group) userInfo[new NSString(AlertGroupKey)] = new NSString(group.ToString());
        content.UserInfo = userInfo;

        // Same identifier as an earlier notification for this alert: iOS replaces it.
        var request = UNNotificationRequest.FromIdentifier(notification.Tag, content, trigger: null);
        await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
    }

    public void RemoveAll()
    {
        UNUserNotificationCenter.Current.RemoveAllDeliveredNotifications();
        UNUserNotificationCenter.Current.RemoveAllPendingNotificationRequests();
    }

    public void Remove(string tag)
    {
        UNUserNotificationCenter.Current.RemoveDeliveredNotifications([tag]);
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests([tag]);
    }
}

/// <summary>Shows notifications while the app is open, and handles taps.</summary>
internal sealed class NotificationCenterDelegate : UNUserNotificationCenterDelegate
{
    public override void WillPresentNotification(
        UNUserNotificationCenter center,
        UNNotification notification,
        Action<UNNotificationPresentationOptions> completionHandler)
    {
        // iOS hides notifications from the app in front unless asked; show them
        // like any other, since the in-app timer is what raises most of them.
        completionHandler(notification.Request.Content.InterruptionLevel == UNNotificationInterruptionLevel.Passive
            ? UNNotificationPresentationOptions.List
            : UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.List | UNNotificationPresentationOptions.Sound);
    }

    public override void DidReceiveNotificationResponse(
        UNUserNotificationCenter center,
        UNNotificationResponse response,
        Action completionHandler)
    {
        var userInfo = response.Notification.Request.Content.UserInfo;
        int? deviceId = userInfo?.ObjectForKey(new NSString(IosAlertNotifier.DeviceIdKey)) is NSNumber number
            ? number.Int32Value
            : null;
        int? alertId = userInfo?.ObjectForKey(new NSString(IosAlertNotifier.AlertIdKey)) is NSNumber alertNumber
            ? alertNumber.Int32Value
            : null;

        var router = IPlatformApplication.Current?.Services.GetService<NotificationRouter>();
        _ = router?.OpenAsync(NotificationTarget.FromPayload(deviceId, alertId,
            userInfo?.ObjectForKey(new NSString(IosAlertNotifier.AlertKeyKey))?.ToString(),
            userInfo?.ObjectForKey(new NSString(IosAlertNotifier.AlertGroupKey))?.ToString()));

        completionHandler();
    }
}
