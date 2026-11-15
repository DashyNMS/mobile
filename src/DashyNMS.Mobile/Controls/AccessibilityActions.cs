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
        _action ??= new UIKit.UIAccessibilityCustomAction(Name, new Func<UIKit.UIAccessibilityCustomAction, bool>(_ => Perform()));
        Apply(root, _action, enabled);
#elif ANDROID
        if (View.Handler?.PlatformView is Android.Views.View native)
        {
            if (!enabled && !AndroidActions.TryGetValue(native, out _)) return;
            var actions = AndroidActions.GetValue(native, v =>
            {
                var manager = new AndroidActionDelegate();
                v.SetAccessibilityDelegate(manager);
                return manager;
            });
            actions.Set(this, enabled);
        }
#endif
    }

#if ANDROID
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Android.Views.View, AndroidActionDelegate> AndroidActions = new();
    private sealed class AndroidActionDelegate : Android.Views.View.AccessibilityDelegate
    {
        private readonly Dictionary<AccessibilityActions, int> _actions = new();
        public void Set(AccessibilityActions action, bool enabled)
        {
            if (enabled && !_actions.ContainsKey(action)) _actions[action] = Android.Views.View.GenerateViewId();
            else if (!enabled) _actions.Remove(action);
        }
        public override void OnInitializeAccessibilityNodeInfo(Android.Views.View? host, Android.Views.Accessibility.AccessibilityNodeInfo? info)
        {
            base.OnInitializeAccessibilityNodeInfo(host, info);
            if (info is null) return;
            foreach (var action in _actions)
                info.AddAction(new Android.Views.Accessibility.AccessibilityNodeInfo.AccessibilityAction(action.Value, new Java.Lang.String(action.Key.Name)));
        }
        public override bool PerformAccessibilityAction(Android.Views.View? host, int action, Android.OS.Bundle? args)
        {
            var found = _actions.FirstOrDefault(a => a.Value == action).Key;
            return found is not null ? found.Perform() : base.PerformAccessibilityAction(host, action, args);
        }
    }
#endif

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
