using Foundation;
using UIKit;

namespace DashyNMS.Mobile;

/// <summary>
/// The app's window scene. Apps built with the iOS 27 SDK must use UIKit's
/// scene lifecycle - without it UIKit stops the app at launch
/// (_UIApplicationEvaluateRuntimeIssueForNoSceneLifecycleAdoption) - so
/// Info.plist's UIApplicationSceneManifest names this, and MAUI's own scene
/// delegate does the rest.
/// </summary>
/// <remarks>
/// With scenes, iOS delivers dashynms:// links (the widgets' taps, and
/// desktop's sign-in QR code from the Camera app, #171) here
/// rather than to <see cref="AppDelegate"/>: while running, through
/// <see cref="OpenUrlContexts"/>; on a cold launch, in the connection
/// options <see cref="WillConnect"/> gets.
/// </remarks>
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
	public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
	{
		base.WillConnect(scene, session, connectionOptions);

		foreach (var context in connectionOptions.UrlContexts ?? new NSSet<UIOpenUrlContext>())
		{
			Open(context.Url);
		}
	}

	/// <summary>UIKit's scene:openURLContexts: - MAUI's scene delegate doesn't have it to override.</summary>
	[Export("scene:openURLContexts:")]
	public void OpenUrlContexts(UIScene scene, NSSet<UIOpenUrlContext> urlContexts)
	{
		foreach (var context in urlContexts)
		{
			Open(context.Url);
		}
	}

	private static void Open(NSUrl? url)
	{
		if (!Adapters.SignInLinkLauncher.TryOpen(url?.AbsoluteString))
		{
			AppDelegate.OpenWidgetLink(url);
		}
	}
}
