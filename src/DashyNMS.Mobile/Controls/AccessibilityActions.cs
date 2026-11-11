namespace DashyNMS.Mobile.Controls;

/// <summary>
/// A VoiceOver custom action (the Actions rotor) on a view and everything drawn
/// inside it. MAUI has no custom actions, so this sets UIKit's own on each
/// native view. VoiceOver reads the answer's labels, chips and buttons as
/// separate elements, and any of them must offer the action, not just the
/// container. Call <see cref="Refresh"/> after the content changes, because new
/// child views have no action until then.
/// </summary>
/// <remarks>
/// Elsewhere this does nothing. Intelligence is iOS-only in 2.0.0, and a long
/// press still reaches the same menu through TalkBack's double-tap-and-hold.
/// </remarks>
public sealed class AccessibilityActions(View view, string name, Func<bool> perform)
{
    public View View { get; } = view;

    /// <summary>What VoiceOver reads in the Actions rotor.</summary>
    public string Name { get; } = name;

    /// <summary>Runs the action; false tells VoiceOver it couldn't.</summary>
    public Func<bool> Perform { get; } = perform;

#if IOS
    private UIKit.UIAccessibilityCustomAction? _action;
#endif

    /// <summary>Adds the action to every native view under the root, or removes it when <paramref name="enabled"/> is false.</summary>
    public void Refresh(bool enabled)
    {
#if IOS
        if (View.Handler?.PlatformView is not UIKit.UIView root) return;
        _action ??= new UIKit.UIAccessibilityCustomAction(Name, _ => Perform());
        Apply(root, _action, enabled);
#endif
    }

#if IOS
    private static void Apply(UIKit.UIView native, UIKit.UIAccessibilityCustomAction action, bool enabled)
    {
        var existing = native.AccessibilityCustomActions ?? [];
        var has = existing.Contains(action);
        if (enabled && !has) native.AccessibilityCustomActions = [.. existing, action];
        else if (!enabled && has) native.AccessibilityCustomActions = existing.Where(a => !ReferenceEquals(a, action)).ToArray();
        foreach (var child in native.Subviews) Apply(child, action, enabled);
    }
#endif
}
