using BackgroundTasks;
using DashyNMS.Mobile.Alerts;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using UIKit;
using UserNotifications;

namespace DashyNMS.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
	{
		var launched = base.FinishedLaunching(application, launchOptions);

		// Both have to be set up before launching finishes: iOS delivers a tap
		// that launched the app, and a background refresh it woke the app for,
		// straight afterwards.
		UNUserNotificationCenter.Current.Delegate = new NotificationCenterDelegate();
		BGTaskScheduler.Shared.Register(IosAlertScheduler.TaskId, null, RunAlertCheck);

		return launched;
	}

	/// <summary>
	/// A dashynms:// link - the home-screen widget's taps - goes where a
	/// notification tap would. With the scene lifecycle (see SceneDelegate)
	/// iOS hands links to the scene rather than here, so the scene calls this.
	/// </summary>
	/// <returns>True if it was one of ours.</returns>
	internal static bool OpenWidgetLink(NSUrl? url)
	{
		if (url?.AbsoluteString is not { } text
			|| !Uri.TryCreate(text, UriKind.Absolute, out var uri)
			|| Widgets.WidgetLink.TryParse(uri) is not { } target)
		{
			return false;
		}

		// After the scene has its window - on a cold launch this runs first.
		MainThread.BeginInvokeOnMainThread(() =>
		{
			var router = IPlatformApplication.Current?.Services.GetService<NotificationRouter>();
			_ = router?.OpenAsync(target);
		});
		return true;
	}

	private static void RunAlertCheck(BGTask task)
	{
		var services = IPlatformApplication.Current?.Services;
		var watcher = services?.GetService<AlertWatcher>();
		if (services is null || watcher is null)
		{
			task.SetTaskCompleted(false);
			return;
		}

		// iOS allows about 30 seconds, then calls this and suspends the app.
		var cancellation = new CancellationTokenSource();
		task.ExpirationHandler = cancellation.Cancel;

		_ = Task.Run(async () =>
		{
			var result = await watcher.CheckAsync(cancellation.Token);

			// iOS runs a refresh task once; ask for the next unless there's
			// nothing to check for any more.
			if (result.Outcome != AlertCheckOutcome.NotSignedIn
				&& services.GetRequiredService<AlertWatchCoordinator>().ChecksNeeded)
			{
				services.GetRequiredService<IBackgroundAlertScheduler>().Schedule();
			}

			task.SetTaskCompleted(result.Succeeded);
			cancellation.Dispose();
		});
	}
}
