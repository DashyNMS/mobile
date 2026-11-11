using Android.Views;

namespace DashyNMS.Mobile;

/// <summary>
/// Android's side of the single-chat snapshot shield (#230): the activity's
/// window is marked secure while the conversation is on screen in that mode, so
/// recents shows a blank thumbnail and screenshots are refused. Android takes the
/// thumbnail as the activity pauses, too late to draw a cover as iOS does.
/// </summary>
/// <remarks>
/// Intelligence is hidden on Android for 2.0, so in practice this never turns on
/// yet; it is wired so the head builds and behaves once Android has the chat.
/// MAUI shows modal pages in dialogs with windows of their own, which this
/// doesn't reach; the chat's sheets need the same flag when Android gets them.
/// </remarks>
internal static class AndroidSnapshotSecurity
{
	public static void Apply(bool secure)
	{
		if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window is not { } window) return;
		if (secure) window.AddFlags(WindowManagerFlags.Secure);
		else window.ClearFlags(WindowManagerFlags.Secure);
	}
}
