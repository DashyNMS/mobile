using DashyNMS.Mobile.Alerts;
using Microsoft.Extensions.Logging;
using UIKit;
using UserNotifications;

namespace DashyNMS.Mobile;

/// <summary>
/// The open-alert count on the app icon. Needs the notification permission's
/// badge option, which <see cref="IosAlertNotifier.RequestPermissionAsync"/> asks for.
/// </summary>
public sealed class IosAppBadge : IAppBadge
{
    private readonly ILogger<IosAppBadge> _logger;

    public IosAppBadge(ILogger<IosAppBadge> logger) => _logger = logger;

    public bool IsSupported => true;

    public void SetCount(int count)
    {
        count = Math.Max(0, count);

        // iOS 16 moved the badge to the notification centre, which is safe
        // from any thread (a background refresh runs off the main thread).
        if (OperatingSystem.IsIOSVersionAtLeast(16))
        {
            UNUserNotificationCenter.Current.SetBadgeCount(count, error =>
            {
                if (error is not null)
                {
                    _logger.LogDebug("Could not set the app badge: {Error}", error.LocalizedDescription);
                }
            });
            return;
        }

        // iOS 15: UIApplication's property, main thread only.
        UIApplication.SharedApplication.InvokeOnMainThread(() =>
            UIApplication.SharedApplication.ApplicationIconBadgeNumber = count);
    }
}
