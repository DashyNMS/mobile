using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using DashyNMS.Mobile.Alerts;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		// A notification tap that launched the app.
		OpenFromNotification(Intent);
	}

	protected override void OnNewIntent(Intent? intent)
	{
		base.OnNewIntent(intent);

		// A notification tap while the app was already running.
		OpenFromNotification(intent);
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

		var router = IPlatformApplication.Current?.Services.GetService<NotificationRouter>();
		_ = router?.OpenAsync(new NotificationTarget(deviceId));
	}
}
