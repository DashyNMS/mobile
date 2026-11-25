using CoreGraphics;
using Foundation;
using UIKit;

namespace DashyNMS.Mobile;

/// <summary>
/// Tracks live docked-keyboard geometry on iOS using UIKit's
/// <see cref="UIView.KeyboardLayoutGuide"/> so folding, unfolding or rotating
/// with a docked keyboard never leaves composers, Save buttons or search fields
/// behind a stale pre-fold frame.
/// </summary>
internal sealed class LiveKeyboardBounds : IDisposable
{
    private readonly List<NSObject> _observers = [];
    private CGRect? _pendingScreenFrame;
    private CGSize _pendingWindowSize;
    private bool _visible;
    private bool _disposed;

    public LiveKeyboardBounds(Action onChanged, Action? onShownOrChanged = null)
    {
        _observers.Add(UIKeyboard.Notifications.ObserveWillChangeFrame((_, args) =>
        {
            if (_disposed) return;
            UpdateFromNotification(args.FrameEnd);
            onChanged();
            if (_visible) onShownOrChanged?.Invoke();
        }));
        _observers.Add(UIKeyboard.Notifications.ObserveDidChangeFrame((_, args) =>
        {
            if (_disposed) return;
            UpdateFromNotification(args.FrameEnd);
            onChanged();
        }));
        _observers.Add(UIKeyboard.Notifications.ObserveWillHide((_, _) =>
        {
            if (_disposed) return;
            _visible = false;
            _pendingScreenFrame = null;
            onChanged();
        }));
        _observers.Add(UIKeyboard.Notifications.ObserveDidHide((_, _) =>
        {
            if (_disposed) return;
            _visible = false;
            _pendingScreenFrame = null;
            onChanged();
        }));
    }

    private void UpdateFromNotification(CGRect screenFrame)
    {
        var window = ActiveWindow();
        if (window is null)
        {
            _visible = false;
            _pendingScreenFrame = null;
            return;
        }

        var inWindow = window.ConvertRectFromCoordinateSpace(screenFrame, window.Screen.CoordinateSpace);
        if (IsDockedSoftKeyboard(inWindow, window.Bounds, window.SafeAreaInsets.Bottom))
        {
            _visible = true;
            _pendingScreenFrame = screenFrame;
            _pendingWindowSize = window.Bounds.Size;
        }
        else
        {
            _visible = false;
            _pendingScreenFrame = null;
        }
    }

    /// <summary>
    /// Returns the docked keyboard's top Y coordinate in <paramref name="view"/>'s
    /// bounds, or null when no docked keyboard overlaps <paramref name="view"/>.
    /// </summary>
    public double? TopIn(UIView view)
    {
        if (!_visible || view.Window is not { } window || view.Bounds.Height <= 0 || view.Bounds.Width <= 0)
        {
            return null;
        }

        // Prefer UIKit's live KeyboardLayoutGuide frame: it tracks folds and rotations
        // in the view's own coordinates without relying on a pre-fold screen rect.
        // Require height meaningfully above the bottom safe-area inset, since a hidden
        // or hardware-only keyboard collapses the guide to SafeAreaInsets.Bottom.
        var guide = view.KeyboardLayoutGuide.LayoutFrame;
        if (!guide.IsEmpty && IsDockedSoftKeyboard(guide, view.Bounds, view.SafeAreaInsets.Bottom))
        {
            return (double)guide.Top;
        }

        // During the initial WillChangeFrame before KeyboardLayoutGuide settles, fall
        // back to the notification frame only while the window size still matches.
        if (_pendingScreenFrame is { } screen && window.Bounds.Size == _pendingWindowSize)
        {
            var converted = view.ConvertRectFromView(
                window.ConvertRectFromCoordinateSpace(screen, window.Screen.CoordinateSpace), window);
            if (IsDockedSoftKeyboard(converted, view.Bounds, view.SafeAreaInsets.Bottom))
            {
                return (double)converted.Top;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns how far a docked keyboard overlaps the bottom of <paramref name="view"/>,
    /// or 0 when no docked keyboard overlaps it.
    /// </summary>
    public double OverlapIn(UIView view) =>
        TopIn(view) is { } top ? Math.Max(0, view.Bounds.Height - top) : 0;

    private static bool IsDockedSoftKeyboard(CGRect keyboard, CGRect bounds, nfloat safeBottom) =>
        keyboard.Width >= bounds.Width * 0.8
        && keyboard.Bottom >= bounds.Height - 1
        && keyboard.Top < bounds.Height - safeBottom - 40
        && keyboard.Height > safeBottom + 40;

    private static UIWindow? ActiveWindow()
    {
        var scene = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .FirstOrDefault(s => s.ActivationState == UISceneActivationState.ForegroundActive)
            ?? UIApplication.SharedApplication.ConnectedScenes.OfType<UIWindowScene>().FirstOrDefault();
        return scene?.Windows.FirstOrDefault(w => w.IsKeyWindow) ?? scene?.Windows.FirstOrDefault();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _visible = false;
        _pendingScreenFrame = null;
        foreach (var observer in _observers) observer.Dispose();
        _observers.Clear();
    }
}
