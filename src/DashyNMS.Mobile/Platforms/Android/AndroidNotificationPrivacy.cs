using DashyNMS.Mobile.Alerts;

namespace DashyNMS.Mobile;

/// <summary>
/// Android's "Hide alert details on the lock screen" setting (#8), kept on
/// the phone. On unless turned off: Android otherwise shows a notification's
/// device and rule to anyone who picks the phone up.
/// </summary>
public sealed class AndroidNotificationPrivacy : INotificationPrivacy
{
    private const string Key = "notifications.hide-lock-screen-details";

    /// <summary>For the notifier, which the background alert check also builds.</summary>
    internal static bool Hide => Preferences.Default.Get(Key, true);

    public bool CanHideLockScreenDetails => true;

    public bool HideLockScreenDetails
    {
        get => Hide;
        set => Preferences.Default.Set(Key, value);
    }
}
