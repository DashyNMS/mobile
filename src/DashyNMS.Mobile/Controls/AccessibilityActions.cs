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
        private static readonly int[] CustomActionIds =
        [
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_000,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_001,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_002,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_003,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_004,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_005,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_006,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_007,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_008,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_009,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_010,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_011,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_012,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_013,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_014,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_015,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_016,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_017,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_018,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_019,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_020,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_021,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_022,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_023,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_024,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_025,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_026,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_027,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_028,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_029,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_030,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_031,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_032,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_033,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_034,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_035,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_036,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_037,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_038,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_039,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_040,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_041,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_042,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_043,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_044,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_045,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_046,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_047,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_048,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_049,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_050,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_051,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_052,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_053,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_054,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_055,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_056,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_057,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_058,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_059,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_060,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_061,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_062,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_063,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_064,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_065,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_066,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_067,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_068,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_069,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_070,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_071,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_072,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_073,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_074,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_075,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_076,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_077,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_078,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_079,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_080,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_081,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_082,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_083,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_084,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_085,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_086,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_087,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_088,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_089,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_090,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_091,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_092,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_093,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_094,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_095,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_096,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_097,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_098,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_099,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_100,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_101,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_102,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_103,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_104,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_105,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_106,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_107,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_108,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_109,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_110,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_111,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_112,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_113,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_114,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_115,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_116,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_117,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_118,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_119,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_120,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_121,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_122,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_123,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_124,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_125,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_126,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_127,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_128,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_129,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_130,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_131,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_132,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_133,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_134,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_135,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_136,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_137,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_138,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_139,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_140,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_141,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_142,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_143,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_144,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_145,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_146,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_147,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_148,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_149,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_150,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_151,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_152,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_153,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_154,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_155,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_156,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_157,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_158,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_159,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_160,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_161,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_162,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_163,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_164,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_165,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_166,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_167,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_168,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_169,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_170,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_171,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_172,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_173,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_174,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_175,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_176,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_177,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_178,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_179,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_180,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_181,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_182,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_183,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_184,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_185,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_186,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_187,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_188,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_189,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_190,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_191,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_192,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_193,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_194,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_195,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_196,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_197,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_198,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_199,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_200,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_201,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_202,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_203,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_204,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_205,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_206,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_207,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_208,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_209,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_210,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_211,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_212,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_213,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_214,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_215,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_216,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_217,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_218,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_219,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_220,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_221,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_222,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_223,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_224,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_225,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_226,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_227,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_228,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_229,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_230,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_231,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_232,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_233,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_234,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_235,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_236,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_237,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_238,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_239,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_240,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_241,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_242,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_243,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_244,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_245,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_246,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_247,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_248,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_249,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_250,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_251,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_252,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_253,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_254,
            global::DashyNMS.Mobile.Resource.Id.dashynms_accessibility_action_255,
        ];
        private readonly Dictionary<AccessibilityActions, int> _actions = new();
        public void Set(AccessibilityActions action, bool enabled)
        {
            if (enabled && !_actions.ContainsKey(action))
            {
                var id = CustomActionIds.FirstOrDefault(id => !_actions.ContainsValue(id));
                if (id != 0) _actions[action] = id;
            }
            else if (!enabled) _actions.Remove(action);
        }
        public override void OnInitializeAccessibilityNodeInfo(Android.Views.View? host, Android.Views.Accessibility.AccessibilityNodeInfo? info)
        {
            base.OnInitializeAccessibilityNodeInfo(host, info);
            if (info is null) return;
            foreach (var action in _actions)
                info.AddAction(new Android.Views.Accessibility.AccessibilityNodeInfo.AccessibilityAction(action.Value, new Java.Lang.String(action.Key.Name)));
        }
        public override bool PerformAccessibilityAction(Android.Views.View host, Android.Views.Accessibility.Action action, Android.OS.Bundle? args)
        {
            var found = _actions.FirstOrDefault(a => a.Value == (int)action).Key;
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
