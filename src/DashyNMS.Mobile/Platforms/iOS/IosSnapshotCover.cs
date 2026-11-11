using UIKit;

namespace DashyNMS.Mobile;

/// <summary>
/// What the app switcher shows instead of a single-chat conversation (#230):
/// the app's name on the system background, in a window of its own above the
/// app's, so it also covers the chat's sheets. Nothing from the chat or the
/// network is on it. Shown and hidden on the main thread, as the snapshot
/// shield decides (<see cref="Intelligence.IntelligenceSnapshotShield"/>).
/// </summary>
/// <remarks>
/// <para>
/// A separate window rather than a view added to the app's: presented sheets sit
/// in their own transition views, and the cover must stay above all of them.
/// It is never made key and doesn't take touches, should one arrive before it
/// is hidden.
/// </para>
/// <para>
/// The keyboard is the exception: iOS draws it in a system window above any
/// level an app window can take, so a cover can't hide its predictive bar,
/// which can show words from the private draft. Before the cover goes up, the
/// composer - or any field in the chat's sheets - is made to end editing,
/// without animation, which takes the keyboard down. It isn't brought back on
/// return: the user taps the composer again, and the draft is still there.
/// Restoring it automatically could raise the keyboard over a page the user
/// has since left, and the composer's glass (#227) already settles at rest
/// when the keyboard goes, as it does for any dismissal. Whether the keyboard
/// is fully gone by the time iOS takes the snapshot needs an installed build to
/// confirm.
/// </para>
/// </remarks>
internal static class IosSnapshotCover
{
	private static UIWindow? _cover;

	/// <summary>
	/// Ends editing in the app's windows, then puts the cover up. True once the
	/// cover is on screen, or already was; false when there is no native window
	/// or scene to cover yet, so the shield leaves itself uncovered and tries
	/// again. An exception leaves no cover behind and is the shield's to handle.
	/// </summary>
	public static bool Show(Microsoft.Maui.Controls.Window window)
	{
		if (_cover is not null) return true;
		if (window.Handler?.PlatformView is not UIWindow host || host.WindowScene is not { } scene) return false;
		EndEditing(host, scene);

		var controller = new UIViewController();
		var view = controller.View!;
		view.BackgroundColor = UIColor.SystemBackground;
		var title = new UILabel
		{
			Text = "DashyNMS",
			Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Title2),
			AdjustsFontForContentSizeCategory = true,
			TextColor = UIColor.Label,
			TextAlignment = UITextAlignment.Center,
			Lines = 0,
		};
		var note = new UILabel
		{
			Text = "Hidden while single-chat mode is on",
			Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Subheadline),
			AdjustsFontForContentSizeCategory = true,
			TextColor = UIColor.SecondaryLabel,
			TextAlignment = UITextAlignment.Center,
			Lines = 0,
		};
		var stack = new UIStackView([title, note])
		{
			Axis = UILayoutConstraintAxis.Vertical,
			Spacing = 6,
			Alignment = UIStackViewAlignment.Center,
			TranslatesAutoresizingMaskIntoConstraints = false,
		};
		view.AddSubview(stack);
		var guide = view.SafeAreaLayoutGuide;
		NSLayoutConstraint.ActivateConstraints(
		[
			stack.CenterYAnchor.ConstraintEqualTo(guide.CenterYAnchor),
			stack.LeadingAnchor.ConstraintGreaterThanOrEqualTo(guide.LeadingAnchor, 24),
			stack.TrailingAnchor.ConstraintLessThanOrEqualTo(guide.TrailingAnchor, -24),
			stack.CenterXAnchor.ConstraintEqualTo(guide.CenterXAnchor),
		]);

		var cover = new UIWindow(scene)
		{
			RootViewController = controller,
			WindowLevel = UIWindowLevel.Alert + 1,
			UserInteractionEnabled = false,
			// Matches the app's own light or dark choice rather than the system's.
			OverrideUserInterfaceStyle = host.OverrideUserInterfaceStyle,
		};
		try
		{
			cover.Hidden = false;
		}
		catch
		{
			Dispose(cover);
			throw;
		}

		// Recorded only once it is showing, so Hide never misses one and a failure leaves none.
		_cover = cover;
		return true;
	}

	/// <summary>
	/// Takes the keyboard down: every window of the scene, so a field in a sheet
	/// counts as well as the composer. Without animation, so it is gone as soon
	/// as iOS allows rather than sliding out under the snapshot.
	/// </summary>
	private static void EndEditing(UIWindow host, UIWindowScene scene)
	{
		UIView.PerformWithoutAnimation(() =>
		{
			host.EndEditing(true);
			foreach (var other in scene.Windows)
				if (!ReferenceEquals(other, host)) other.EndEditing(true);
		});
	}

	public static void Hide()
	{
		if (_cover is not { } cover) return;
		_cover = null;
		Dispose(cover);
	}

	private static void Dispose(UIWindow cover)
	{
		cover.Hidden = true;
		cover.RootViewController = null;
		cover.Dispose();
	}
}
