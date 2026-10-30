using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using DashyNMS.Mobile.Alerts;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
// Desktop's sign-in QR code, opened from the camera (#171).
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "dashynms", DataHost = "sign-in")]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		// A notification tap or a sign-in link that launched the app.
		OpenFromNotification(Intent);
		OpenSignInLink(Intent);
	}

	protected override void OnNewIntent(Intent? intent)
	{
		base.OnNewIntent(intent);

		// A notification tap or a sign-in link while the app was already running.
		OpenFromNotification(intent);
		OpenSignInLink(intent);
	}

	private static void OpenSignInLink(Intent? intent)
	{
		if (intent?.Action != Intent.ActionView || !Adapters.SignInLinkLauncher.TryOpen(intent.DataString))
		{
			return;
		}

		// Handled once: the activity being recreated (rotation) mustn't ask again.
		intent.SetData(null);
	}

	private static void OpenFromNotification(Intent? intent)
	{
		if (intent?.GetBooleanExtra(AndroidAlertNotifier.ExtraFromNotification, false) != true)
		{
			return;
		}

		// Handled once: the activity being recreated (rotation) shouldn't reopen it.
		intent.RemoveExtra(AndroidAlertNotifier.ExtraFromNotification);

		int? deviceId = intent.HasExtra(AndroidAlertNotifier.ExtraDeviceId)
			? intent.GetIntExtra(AndroidAlertNotifier.ExtraDeviceId, 0)
			: null;
		int? alertId = intent.HasExtra(AndroidAlertNotifier.ExtraAlertId)
			? intent.GetIntExtra(AndroidAlertNotifier.ExtraAlertId, 0)
			: null;

		var router = IPlatformApplication.Current?.Services.GetService<NotificationRouter>();
		_ = router?.OpenAsync(NotificationTarget.FromPayload(deviceId, alertId, intent.GetStringExtra(AndroidAlertNotifier.ExtraAlertKey), intent.GetStringExtra(AndroidAlertNotifier.ExtraAlertGroupKey)));
	}
}
