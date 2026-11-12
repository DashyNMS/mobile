using DashyNMS.Mobile.Services;
#if IOS
using CoreGraphics;
using Foundation;
using UIKit;
#endif

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Owns setup-page clearance while visible. Native safe areas own the top on
/// iOS; this helper owns sides and bottom. Android retains native safe areas
/// and keyboard resizing. Existing pages remain on their previous policy until opted in.
/// </summary>
internal sealed class SetupPageLayout
{
    private readonly ContentPage _page;
    private readonly SetupLayout _layout;
    private readonly PageHeader _header;
    private readonly View[] _scrollers;
    private IDisposable? _edges;
    private bool _showing;
    private readonly List<VisualElement> _focusTargets = [];
#if IOS
    private readonly List<NSObject> _observers = [];
    private CGRect? _keyboardScreen;
#endif

    public SetupPageLayout(ContentPage page, SetupLayout layout, PageHeader header, params View[] scrollers)
    {
        _page = page;
        _layout = layout;
        _header = header;
        _scrollers = scrollers;
#if IOS
        page.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container,
            SafeAreaRegions.None, SafeAreaRegions.None);
        header.BackInGlass = TabBarButtons.IsAvailable;
#else
        page.SafeAreaEdges = SafeAreaEdges.All;
#endif
        page.Appearing += OnAppearing;
        page.Disappearing += OnDisappearing;
    }

    /// <summary>Pages used as pinned roots supply their own Back and page actions.</summary>
    internal Action? ShowActions { get; set; }

    /// <summary>
    /// A page whose body runs to the bottom of the screen behind an overlay (the
    /// chat, #594) takes the measured foot here instead of as layout padding.
    /// This helper still measures the keyboard, pill and glass once.
    /// </summary>
    internal Action<SetupFoot>? PlaceFoot { get; set; }

    private void OnAppearing(object? sender, EventArgs e)
    {
        if (_showing) return;
        _showing = true;
        _header.Apply(_page);
        _edges = CoveredEdges.Watch(_page, Place);
        _layout.SizeChanged += OnChanged;
        WatchFocus(_layout);
        foreach (var scroller in _scrollers)
        {
            scroller.HandlerChanged += OnChanged;
            scroller.Loaded += OnChanged;
            if (scroller is ScrollView { Content: { } content }) content.SizeChanged += OnChanged;
            StopInsets(scroller);
        }
#if IOS
        DashyNMS.Mobile.Controls.ManualKeyboardScroll.Acquire(this);
        TabBarButtons.Moved += OnChanged;
        ShowBack();
        _observers.Add(UIKeyboard.Notifications.ObserveWillChangeFrame((_, args) =>
        {
            _keyboardScreen = args.FrameEnd;
            Place();
            _page.Dispatcher.Dispatch(ScrollFocusedField);
        }));
        _observers.Add(UIKeyboard.Notifications.ObserveWillHide((_, _) =>
        {
            _keyboardScreen = null;
            Place();
        }));
        _observers.Add(UIApplication.Notifications.ObserveDidBecomeActive((_, _) =>
        {
            ShowBack();
            Place();
        }));
#endif
        Place();
    }

    /// <summary>Reclaims controls when Shell selects a retained setup stack.</summary>
    internal void Activate()
    {
        if (!_showing) OnAppearing(_page, EventArgs.Empty);
        _header.Apply(_page);
#if IOS
        // A departing manual-layout page may reconnect after our Appearing.
        DashyNMS.Mobile.Controls.ManualKeyboardScroll.Acquire(this);
        ShowBack();
#endif
        Place();
    }

    private void OnDisappearing(object? sender, EventArgs e)
    {
        _showing = false;
        _edges?.Dispose();
        _edges = null;
        _layout.SizeChanged -= OnChanged;
        foreach (var target in _focusTargets) target.Focused -= OnFocused;
        _focusTargets.Clear();
        foreach (var scroller in _scrollers)
        {
            scroller.HandlerChanged -= OnChanged;
            scroller.Loaded -= OnChanged;
            if (scroller is ScrollView { Content: { } content }) content.SizeChanged -= OnChanged;
        }
#if IOS
        TabBarButtons.Moved -= OnChanged;
        TabBarButtons.Hide(_page);
        DashyNMS.Mobile.Controls.ManualKeyboardScroll.Release(this);
        foreach (var observer in _observers) observer.Dispose();
        _observers.Clear();
        _keyboardScreen = null;
#endif
    }

    private void WatchFocus(IVisualTreeElement element)
    {
        if (element is VisualElement target)
        {
            target.Focused += OnFocused;
            _focusTargets.Add(target);
        }
        foreach (var child in element.GetVisualChildren()) WatchFocus(child);
    }

    private void OnFocused(object? sender, FocusEventArgs e)
    {
#if IOS
        // Moving between fields need not change the keyboard frame.
        _page.Dispatcher.Dispatch(ScrollFocusedField);
#endif
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        if (sender is View scroller) StopInsets(scroller);
        // Draft forms can add fields after Appearing. Refresh without duplicate handlers.
        foreach (var target in _focusTargets) target.Focused -= OnFocused;
        _focusTargets.Clear();
        if (_showing) WatchFocus(_layout);
        _page.Dispatcher.Dispatch(Place);
    }

    private void Place()
    {
        if (!_showing || _layout.Height <= 0 || Shell.Current?.CurrentPage != _page) return;
        var edges = CoveredEdges.ForScrolling(_page);
        var bottom = SetupActionPlacement.BottomGap(_layout.Height, edges.Bottom, null, []);
        var foot = new SetupFoot(_layout.Width, _layout.Height, edges.Bottom, null, [], null);
#if IOS
        if (_layout.Handler?.PlatformView is UIView view)
        {
            OccupiedArea? pill = TabBarButtons.PillIn(view) is { } bar ? Area(bar) : null;
            var glass = TabBarButtons.Occupied(view).Select(Area).ToArray();
            double? keyboardTop = null;
            if (_keyboardScreen is { } screen && view.Window is { } window)
            {
                var keyboard = view.ConvertRectFromView(window.ConvertRectFromCoordinateSpace(screen, window.Screen.CoordinateSpace), window);
                // Do not lift the entire form above an undocked iPad keyboard.
                if (keyboard.Width >= view.Bounds.Width * .8 && keyboard.Bottom >= view.Bounds.Height)
                    keyboardTop = keyboard.Top;
            }
            var safeBottom = CoveredEdges.Of(_page).Bottom + (pill is null ? TabBarButtons.CoveredBottom : 0);
            bottom = SetupActionPlacement.BottomGap(view.Bounds.Height, safeBottom, pill, glass, keyboardTop);
            foot = new SetupFoot(view.Bounds.Width, view.Bounds.Height, safeBottom, pill, glass, keyboardTop);
        }
#endif
        // The action's Auto row reserves its actual (including large-text) height.
        // With no action this same padding keeps the last result clear of glass.
        var padding = new Thickness(16 + edges.Left, 0, 16 + edges.Right, PlaceFoot is null ? bottom : 0);
        PlaceFoot?.Invoke(foot with { ColumnLeft = padding.Left, ColumnRight = foot.Width - padding.Right });
        if (_layout.Padding != padding) _layout.Padding = padding;
    }

    private static void StopInsets(View scroller)
    {
#if IOS
        if (scroller.Handler?.PlatformView is UIView view && FindScroll(view) is { } scroll)
            scroll.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
#endif
    }

#if IOS
    private void ShowBack()
    {
        if (ShowActions is { } show) { show(); return; }
        if (TabBarButtons.IsAvailable)
            TabBarButtons.Show(_page, null, string.Empty,
            [new("chevron.backward", "Back to " + BackBar.PreviousTitle(_page),
                () => _ = Shell.Current.GoToAsync(".."), IsBack: true)]);
    }

    private static OccupiedArea Area(CGRect rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);
    private static UIScrollView? FindScroll(UIView view) =>
        view as UIScrollView ?? view.Subviews.Select(FindScroll).FirstOrDefault(found => found is not null);
    private static UIView? FindFocus(UIView view) =>
        view.IsFirstResponder ? view : view.Subviews.Select(FindFocus).FirstOrDefault(found => found is not null);

    private void ScrollFocusedField()
    {
        if (!_showing || _layout.Handler?.PlatformView is not UIView view || FindFocus(view) is not { } field) return;
        view.LayoutIfNeeded();
        for (var parent = field.Superview; parent is not null; parent = parent.Superview)
        {
            if (parent is not UIScrollView scroll) continue;
            var rect = field.ConvertRectToView(field.Bounds, scroll);
            scroll.ScrollRectToVisible(rect.Inset(-8, -8), true);
            break;
        }
    }
#endif
}

/// <summary>
/// The measured foot of a setup page, in the layout's coordinates: its size,
/// the native bottom inset, the tab bar's pill and glass, and a docked keyboard.
/// </summary>
internal readonly record struct SetupFoot(double Width, double Height, double SafeBottom,
    OccupiedArea? Pill, IReadOnlyList<OccupiedArea> Glass, double? KeyboardTop)
{
    /// <summary>The column's left edge after the layout's side gutters.</summary>
    public double ColumnLeft { get; init; }

    /// <summary>The column's right edge after the layout's side gutters.</summary>
    public double ColumnRight { get; init; }
}
