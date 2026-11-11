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
/// A separate window rather than a view added to the app's: presented sheets sit
/// in their own transition views, and the cover must stay above all of them.
/// It is never made key, so the app's window keeps focus and its first
/// responder, and the composer's keyboard and the glass placement (#227) are
/// as they were once it is gone. It doesn't take touches either, should one
/// arrive before it is hidden.
/// </remarks>
internal static class IosSnapshotCover
{
	private static UIWindow? _cover;

	public static void Show(Microsoft.Maui.Controls.Window window)
	{
		if (_cover is not null) return;
		if (window.Handler?.PlatformView is not UIWindow host || host.WindowScene is not { } scene) return;

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

		_cover = new UIWindow(scene)
		{
			RootViewController = controller,
			WindowLevel = UIWindowLevel.Alert + 1,
			UserInteractionEnabled = false,
			// Matches the app's own light or dark choice rather than the system's.
			OverrideUserInterfaceStyle = host.OverrideUserInterfaceStyle,
		};
		_cover.Hidden = false;
	}

	public static void Hide()
	{
		if (_cover is not { } cover) return;
		_cover = null;
		cover.Hidden = true;
		cover.RootViewController = null;
		cover.Dispose();
	}
}
