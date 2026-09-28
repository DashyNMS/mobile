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
    internal const string AlertIdKey = "alertId";

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

        // Recoveries and acknowledgements arrive silently, as on desktop.
        if (notification.IsProblem)
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

        content.UserInfo = userInfo;

        // Same identifier as an earlier notification for this alert: iOS replaces it.
        var request = UNNotificationRequest.FromIdentifier(notification.Tag, content, trigger: null);
        await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
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
        completionHandler(UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.List | UNNotificationPresentationOptions.Sound);
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
        _ = router?.OpenAsync(new NotificationTarget(deviceId, alertId));

        completionHandler();
    }
}
