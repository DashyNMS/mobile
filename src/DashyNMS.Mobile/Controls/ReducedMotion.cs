namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Whether the phone asks for less movement: iOS's Reduce Motion, or
/// Android's "Remove animations" (animations scaled to nothing). MAUI has no
/// cross-platform switch for it.
/// </summary>
internal static class ReducedMotion
{
    public static bool IsOn
    {
        get
        {
            try
            {
#if IOS
                return UIKit.UIAccessibility.IsReduceMotionEnabled;
#elif ANDROID
                var resolver = Android.App.Application.Context.ContentResolver;
                return Android.Provider.Settings.Global.GetFloat(resolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1f) == 0f;
#else
                return false;
#endif
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
