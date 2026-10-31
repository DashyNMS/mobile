using CoreGraphics;
using DashyNMS.Mobile.Services;
using Foundation;
using UIKit;

namespace DashyNMS.Mobile;

/// <summary>A page's action in the tab bar's glass: an SF Symbol, what VoiceOver says, and what it does.</summary>
internal sealed record GlassAction(string Symbol, string Label, Action Tapped);

/// <summary>
/// The page's search and actions as Liquid Glass buttons by the tab bar, as
/// iOS 26 apps have them, for the page showing - so they're in the same place
/// on every page rather than in each page's header.
/// </summary>
/// <remarks>
/// <para>Search is a button of its own beside the tab bar: above it, in its
/// column, with a vertical tab bar (iPhone Duo), and beside or above its right
/// end with a bottom one. The page's actions (the network map's Refresh,
/// Fit and Reset layout) are a second glass group: at the top of a vertical tab bar's column,
/// below the status bar and camera; with a bottom tab bar, in the row above
/// it to the left of search. The page keeps its own controls clear of them
/// (<see cref="CoveredBottom"/>).</para>
/// <para>Shell's tab bar is UIKit's, built from view controllers, so it can't
/// take UIKit's own search tab - which in any case is an ordinary tab inside
/// a vertical tab bar (#318). The buttons are laid over Shell's tab bar
/// controller instead, placed from the tab bar's glass pill, and placed again
/// each time the controller lays out (<see cref="Relayout"/>), so they follow
/// iPhone Duo opening, folding and turning.</para>
/// <para>A page with nothing to search (the Dashboard) passes no search,
/// and shows its actions alone: no magnifier that does nothing.</para>
/// <para>One set for the app; a page takes it over as it appears and gives it
/// up as it goes, so whichever page shows owns the taps.</para>
/// </remarks>
internal static class TabBarButtons
{
    /// <summary>The gap between the buttons and the tab bar, and between the groups.</summary>
    private const double Gap = 12;

    private static Host? _host;

    /// <summary>Where the tab bar's pill was last found, in the buttons' layer's points; empty if it wasn't.</summary>
    private static CGRect _pill = CGRect.Empty;
    /// <summary>Most placements allowed in a second before the buttons pause following the tab bar.</summary>
    private const int MostPlacesPerSecond = 120;

    /// <summary>How long the buttons stay put once placed too often, before trying again.</summary>
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);

    private static object? _owner;
    private static string? _reported;

    /// <summary>The pill last found, looked at again first rather than searching the window.</summary>
    private static WeakReference<UIView>? _platter;

    private static bool _placeQueued;
    private static int _placesThisSecond;
    private static DateTime _secondStarted;
    private static DateTime _pausedUntil;
    private static bool _pauseNoted;

    /// <summary>Liquid Glass needs iOS 26; before that, pages keep search and their actions on the page.</summary>
    public static bool IsAvailable => OperatingSystem.IsIOSVersionAtLeast(26);

    /// <summary>Raised when the buttons move, so pages can keep their controls clear of them.</summary>
    public static event EventHandler? Moved;

    /// <summary>
    /// How much more of the foot of the screen the buttons take: the row above
    /// a bottom tab bar, when they sit there; nothing with a vertical one.
    /// </summary>
    public static double CoveredBottom => _host is { Hidden: false, AboveBottomBar: true } host
        ? host.RowHeight + Gap
        : 0;

    /// <summary>
    /// Shows search (<paramref name="search"/>, said as <paramref name="searchLabel"/>)
    /// and <paramref name="actions"/> for <paramref name="owner"/>; no search
    /// button where <paramref name="search"/> is null.
    /// </summary>
    public static void Show(object owner, Action? search, string searchLabel, IReadOnlyList<GlassAction> actions)
    {
        if (!IsAvailable)
        {
            return;
        }

        _host ??= new Host();
        _owner = owner;
        _host.Set(search, searchLabel, actions);
        _host.Hidden = false;
        if (TabDots.FindTabs() is { } tabs)
        {
            Relayout(tabs);
        }
    }

    /// <summary>Hides the buttons, unless another page has taken them over since.</summary>
    public static void Hide(object owner)
    {
        if (_host is null || !ReferenceEquals(owner, _owner))
        {
            return;
        }

        _owner = null;
        _host.Set(null, string.Empty, []);
        _host.Hidden = true;
        Moved?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Places the buttons by <paramref name="tabs"/>' tab bar - from its
    /// ViewDidLayoutSubviews, and once more straight after, as the tab bar
    /// lays out its own glass after the controller does.
    /// </summary>
    /// <remarks>
    /// <para>Only a controller showing its tab bar places them. Signing in,
    /// the sign-in screen's controller (its tab bar hidden) is still laying
    /// out beside the main one for a moment; when both took the buttons in
    /// turn, each move made the other lay out again, without end, and the
    /// app never finished launching (#332).</para>
    /// <para>Only one placement waits for the next turn at a time, however
    /// often the controller lays out. And should placing them ever keep the
    /// tab bar laying out - more than <see cref="MostPlacesPerSecond"/> a
    /// second - they stay where they are for a moment, noted in the
    /// diagnostics log, then try again.</para>
    /// </remarks>
    public static void Relayout(UITabBarController tabs)
    {
        if (_host is null || _owner is null || tabs.View is not { Window: not null } root)
        {
            return;
        }

        Place(tabs, root);
        if (_placeQueued || tabs.TabBar is not { Hidden: false })
        {
            return;
        }

        _placeQueued = true;
        NSRunLoop.Main.BeginInvokeOnMainThread(() =>
        {
            _placeQueued = false;
            if (_owner is not null && tabs.View is { Window: not null } later)
            {
                Place(tabs, later);
            }
        });
    }

    // Counts placements; true while paused after too many in a second, and
    // places once more when the pause is over.
    private static bool TooOften(UITabBarController tabs)
    {
        var now = DateTime.UtcNow;
        if (now < _pausedUntil)
        {
            return true;
        }

        if (now - _secondStarted > TimeSpan.FromSeconds(1))
        {
            _secondStarted = now;
            _placesThisSecond = 0;
        }

        if (++_placesThisSecond <= MostPlacesPerSecond)
        {
            return false;
        }

        _pausedUntil = now + Pause;
        if (!_pauseNoted)
        {
            _pauseNoted = true;
            (IPlatformApplication.Current?.Services.GetService(typeof(DiagnosticsLog)) as DiagnosticsLog)?.Note(
                "Layout", $"Tab bar buttons: paused, placed over {MostPlacesPerSecond} times in a second");
        }

        var weak = new WeakReference<UITabBarController>(tabs);
        NSTimer.CreateScheduledTimer((Pause + TimeSpan.FromMilliseconds(50)).TotalSeconds, false, _ =>
        {
            if (weak.TryGetTarget(out var again))
            {
                Relayout(again);
            }
        });
        return true;
    }

    /// <summary>
    /// The tab bar's pill in <paramref name="view"/>'s points, for lining
    /// things up with it; null where it wasn't found or nothing's shown.
    /// </summary>
    public static CGRect? PillIn(UIView view) =>
        _host is { Window: not null, Hidden: false } host && !_pill.IsEmpty ? host.ConvertRectToView(_pill, view) : null;

    /// <summary>
    /// The tab bar's pill and the buttons, in <paramref name="view"/>'s points,
    /// for what keeps clear of them across its own row (<c>CoveredEdges.Beside</c>);
    /// nothing where the pill wasn't found.
    /// </summary>
    public static IReadOnlyList<CGRect> Occupied(UIView view)
    {
        if (_host is not { Window: not null, Hidden: false } host || _pill.IsEmpty)
        {
            return [];
        }

        var areas = new List<CGRect> { host.ConvertRectToView(_pill, view) };
        foreach (var glass in new[] { host.Search, host.Actions })
        {
            if (glass.Alpha > 0)
            {
                areas.Add(host.ConvertRectToView(glass.Frame, view));
            }
        }

        return areas;
    }

    private static void Place(UITabBarController tabs, UIView root)
    {
        // A controller with no tab bar showing leaves the buttons where they are.
        if (tabs.TabBar is not { Hidden: false, Window: not null } tabBar)
        {
            if (_host is { } shown && ReferenceEquals(shown.Superview, root))
            {
                Unplace(shown);
            }

            return;
        }

        if (TooOften(tabs))
        {
            return;
        }

        var host = _host!;
        if (!ReferenceEquals(host.Superview, root))
        {
            host.RemoveFromSuperview();
            host.Frame = root.Bounds;
            root.AddSubview(host);
        }

        if (!ReferenceEquals(root.Subviews.LastOrDefault(), host))
        {
            root.BringSubviewToFront(host);
        }

        var (bar, from) = Pill(tabs, tabBar, host);
        _pill = from.StartsWith("bar", StringComparison.Ordinal) ? CGRect.Empty : bar;
        if (bar.Width <= 0 || bar.Height <= 0)
        {
            Unplace(host);
            return;
        }

        var count = host.ActionCount;
        CGRect search, actions;
        host.AboveBottomBar = false;
        if (bar.Height > bar.Width)
        {
            // Search above the pill, in its column, as wide as it is; the
            // actions at the column's top, below whatever the system has there,
            // or straight above search (or the pill) if that leaves them no room.
            var size = bar.Width;
            search = new CGRect(bar.GetMidX() - size / 2, bar.Top - Gap - size, size, size);
            var below = host.HasSearch ? search.Top : bar.Top;
            var top = ColumnTop(host, bar) + Gap;
            actions = new CGRect(search.Left, top, size, size * count);
            if (actions.Bottom + Gap > below)
            {
                actions = new CGRect(search.Left, below - Gap - (size * count), size, size * count);
            }

            host.RowHeight = size;
            host.Vertical = true;
        }
        else
        {
            // Search at the pill's right, a gap apart, as tall as it is - or
            // above its right end if the screen has no room there. The actions
            // in the row above, to search's left (or at the pill's right end).
            // With no search, the actions take its place: beside the pill if
            // they fit, else above its right end.
            var size = bar.Height;
            var row = bar.Top - Gap - size;
            var room = host.Bounds.Width - host.SafeAreaInsets.Right;
            if (!host.HasSearch)
            {
                search = CGRect.Empty;
                actions = bar.Right + Gap + (size * count) <= room
                    ? new CGRect(bar.Right + Gap, bar.GetMidY() - size / 2, size * count, size)
                    : new CGRect(bar.Right - (size * count), row, size * count, size);
                host.AboveBottomBar = count > 0 && actions.Top < bar.Top;
            }
            else
            {
                search = new CGRect(bar.Right + Gap, bar.GetMidY() - size / 2, size, size);
                if (search.Right > room)
                {
                    search = new CGRect(bar.Right - size, row, size, size);
                    actions = new CGRect(search.Left - Gap - (size * count), row, size * count, size);
                }
                else
                {
                    actions = new CGRect(bar.Right - (size * count), row, size * count, size);
                }

                host.AboveBottomBar = count > 0 || search.Top < bar.Top;
            }

            host.RowHeight = size;
            host.Vertical = false;
        }

        if (!host.HasSearch)
        {
            search = CGRect.Empty;
        }

        var moved = host.Arrange(search, actions);
        Report(tabBar, host, bar, from, search, actions);
        if (moved)
        {
            Moved?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// The top of a vertical tab bar's column that's free: below the status
    /// bar, camera or anything else the system draws there (iOS 27.1 says
    /// where), else below the safe area's top.
    /// </summary>
    private static double ColumnTop(UIView host, CGRect bar)
    {
        double top = host.SafeAreaInsets.Top;
        if (OperatingSystem.IsIOSVersionAtLeast(27, 1))
        {
            foreach (var area in ReservedRegions.Occupied(host).Where(a => a.Left < bar.Right && a.Right > bar.Left && a.Top < bar.Top))
            {
                top = Math.Max(top, area.Bottom);
            }
        }

        return top;
    }

    // Not shown: no tab bar, or nothing to place them by.
    private static void Unplace(Host host)
    {
        host.AboveBottomBar = false;
        if (host.Arrange(CGRect.Empty, CGRect.Empty))
        {
            Moved?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// The tab bar's glass pill, in <paramref name="into"/>'s points.
    /// </summary>
    /// <remarks>
    /// iOS 26 draws the pill as a <c>_UITabBarItemPlatterView</c>, round the
    /// tab buttons (<c>_UITabButton</c>). With a vertical tab bar (iPhone Duo)
    /// it isn't inside the UITabBar, which is then an empty column the screen's
    /// full height: measured from that, the button went off the top of the
    /// screen (#306's diagnostics). So it's looked for across the whole window,
    /// leaving out the page showing and the buttons themselves.
    /// <para>This runs every time the tab bar controller lays out, so the pill
    /// once found is looked at again first, and the search never goes into
    /// a page's views (the Dashboard's cards run to thousands) or the app's
    /// own. The pill is UIKit's.</para>
    /// </remarks>
    private static (CGRect Bar, string From) Pill(UITabBarController tabs, UITabBar tabBar, UIView into)
    {
        var window = into.Window ?? tabBar.Window;
        if (_platter is not null && _platter.TryGetTarget(out var known)
            && known.Window is not null && ReferenceEquals(known.Window, window) && Showing(known))
        {
            return (known.ConvertRectToView(known.Bounds, into), Name(known));
        }

        _platter = null;
        var page = tabs.SelectedViewController?.View;

        // Not into the page's own views - a list can hold thousands - nor the buttons'.
        var candidates = window is null
            ? []
            : Descendants(window, view => view.Hidden || ReferenceEquals(view, into) || ReferenceEquals(view, page) || IsOurs(view))
                .Where(Showing)
                .ToList();

        // The pill itself, the biggest if there are several (one per state).
        var platter = candidates
            .Where(view => Name(view).Contains("TabBarItemPlatterView", StringComparison.Ordinal))
            .Select(view => (View: view, Frame: view.ConvertRectToView(view.Bounds, into)))
            .OrderByDescending(found => found.Frame.Width * found.Frame.Height)
            .FirstOrDefault();
        if (platter.View is not null)
        {
            _platter = new WeakReference<UIView>(platter.View);
            return (platter.Frame, Name(platter.View));
        }

        // Else round the tab buttons.
        var buttons = candidates.Where(view => Name(view).Contains("TabButton", StringComparison.Ordinal)).ToList();
        if (buttons.Count > 0)
        {
            var area = CGRect.Null;
            foreach (var button in buttons)
            {
                area = area.UnionWith(button.ConvertRectToView(button.Bounds, into));
            }

            return (area, $"{buttons.Count} {Name(buttons[0])}");
        }

        // Else the bar, and what there was, for the diagnostics log.
        var seen = string.Join(", ", candidates.Select(Name).Where(name => name.Contains("Tab", StringComparison.Ordinal)).Distinct().Take(8));
        return (tabBar.ConvertRectToView(tabBar.Bounds, into), "bar; saw " + (seen.Length == 0 ? "no tab views" : seen));
    }

    private static string Name(UIView view) => view.Class.Name ?? string.Empty;

    private static bool Showing(UIView view) =>
        !view.Hidden && view.Alpha > 0 && view.Bounds.Width > 0 && view.Bounds.Height > 0;

    // A page's views - each page and layout is a MauiView, holding everything
    // on it - and the app's own (the buttons, the safe-area watchers). Shell's
    // containers are UIKit's own views, so the tab bar is still found.
    private static bool IsOurs(UIView view) =>
        view is Microsoft.Maui.Platform.MauiView
        || (view.GetType().Namespace?.StartsWith("DashyNMS", StringComparison.Ordinal) ?? false);

    private static IEnumerable<UIView> Descendants(UIView view, Func<UIView, bool> skip)
    {
        foreach (var child in view.Subviews)
        {
            if (skip(child))
            {
                continue;
            }

            yield return child;
            foreach (var inner in Descendants(child, skip))
            {
                yield return inner;
            }
        }
    }

    // Where the buttons went and what they were placed from, in the
    // diagnostics log Settings shares - only when it changes. The tab bar's
    // glass on iPhone Duo can't be seen any other way off the device.
    private static void Report(UITabBar tabBar, UIView host, CGRect bar, string from, CGRect search, CGRect actions)
    {
        var bounds = tabBar.ConvertRectToView(tabBar.Bounds, host);
        var text = $"screen {host.Bounds.Width:0.#}×{host.Bounds.Height:0.#}, tab bar {Text(bounds)}, glass {Text(bar)} from {from}"
            + $" → search {(search.IsEmpty ? "none" : Text(search))}, actions {Text(actions)}";
        if (text == _reported)
        {
            return;
        }

        _reported = text;
        (IPlatformApplication.Current?.Services.GetService(typeof(DiagnosticsLog)) as DiagnosticsLog)?.Note("Layout", "Tab bar buttons: " + text);

        static string Text(CGRect r) => $"{r.Left:0.#},{r.Top:0.#} {r.Width:0.#}×{r.Height:0.#}";
    }

    /// <summary>
    /// A clear layer over the whole tab bar controller holding the buttons,
    /// passing every touch but theirs through.
    /// </summary>
    private sealed class Host : UIView
    {
        private readonly UIImageView _searchIcon = Icon("magnifyingglass");
        private readonly List<Slot> _slots = [];
        private Action? _search;
        private IReadOnlyList<GlassAction> _actions = [];

        public Host()
        {
            BackgroundColor = UIColor.Clear;
            AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;

            // Taps on the glass itself: interactive glass keeps a button
            // inside it from hearing the tap.
            Search = new UIVisualEffectView(Glass()) { ClipsToBounds = true, IsAccessibilityElement = true, Alpha = 0 };
            Search.AccessibilityTraits = UIAccessibilityTrait.Button;
            Search.ContentView.AddSubview(_searchIcon);
            Search.AddGestureRecognizer(new UITapGestureRecognizer(() => _search?.Invoke()));
            AddSubview(Search);

            Actions = new UIVisualEffectView(Glass()) { ClipsToBounds = true, Alpha = 0 };
            Actions.AddGestureRecognizer(new UITapGestureRecognizer(tap =>
            {
                var point = tap.LocationInView(Actions.ContentView);
                _slots.FirstOrDefault(slot => slot.Frame.Contains(point))?.Activate();
            }));
            AddSubview(Actions);
        }

        public UIVisualEffectView Search { get; }

        public UIVisualEffectView Actions { get; }

        public int ActionCount => _actions.Count;

        public bool AboveBottomBar { get; set; }

        /// <summary>Whether the page showing has search; without it the magnifier stays hidden.</summary>
        public bool HasSearch => _search is not null;

        /// <summary>How tall a row of glass is: the pill's height, or its width with a vertical one.</summary>
        public double RowHeight { get; set; }

        /// <summary>The actions run down the group with a vertical tab bar, across it with a bottom one.</summary>
        public bool Vertical { get; set; }

        public void Set(Action? search, string searchLabel, IReadOnlyList<GlassAction> actions)
        {
            _search = search;
            Search.AccessibilityLabel = searchLabel;
            _actions = actions;

            foreach (var slot in _slots)
            {
                slot.RemoveFromSuperview();
            }

            _slots.Clear();
            foreach (var action in actions)
            {
                var slot = new Slot(action);
                _slots.Add(slot);
                Actions.ContentView.AddSubview(slot);
            }

            // In the group as it stands, so they show even before it's next placed.
            LayoutSlots();
        }

        /// <summary>Puts the glass where it goes; empty frames hide it. True if anything moved.</summary>
        public bool Arrange(CGRect search, CGRect actions)
        {
            var moved = false;
            moved |= Put(Search, search);
            moved |= Put(Actions, _actions.Count > 0 ? actions : CGRect.Empty);
            _searchIcon.Frame = Search.ContentView.Bounds;
            LayoutSlots();
            return moved;
        }

        // One square per action, down the group with a vertical tab bar, across it with a bottom one.
        private void LayoutSlots()
        {
            var bounds = Actions.Bounds;
            var step = Vertical ? bounds.Width : bounds.Height;
            for (var i = 0; i < _slots.Count; i++)
            {
                _slots[i].Frame = Vertical
                    ? new CGRect(0, i * step, step, step)
                    : new CGRect(i * step, 0, step, step);
            }
        }

        public override UIView? HitTest(CGPoint point, UIEvent? uievent)
        {
            var hit = base.HitTest(point, uievent);
            return ReferenceEquals(hit, this) ? null : hit;
        }

        // Never off the screen, nor under the status bar: not shown there.
        private bool Put(UIVisualEffectView glass, CGRect frame)
        {
            var shown = !frame.IsEmpty && frame.Top >= SafeAreaInsets.Top && frame.Left >= 0
                && frame.Bottom <= Bounds.Height && frame.Right <= Bounds.Width;
            var target = shown ? frame : glass.Frame;
            var moved = target != glass.Frame || (glass.Alpha > 0) != shown;
            glass.Frame = target;
            glass.Layer.CornerRadius = (nfloat)(Math.Min(target.Width, target.Height) / 2);
            glass.Alpha = shown ? 1 : 0;
            return moved;
        }

        private static UIImageView Icon(string symbol) => new()
        {
            Image = UIImage.GetSystemImage(symbol, UIImageSymbolConfiguration.Create(UIFont.PreferredBody.PointSize + 3, UIImageSymbolWeight.Medium)),
            TintColor = UIColor.Label,
            ContentMode = UIViewContentMode.Center,
        };

        private static UIVisualEffect Glass()
        {
            if (!OperatingSystem.IsIOSVersionAtLeast(26))
            {
                return UIBlurEffect.FromStyle(UIBlurEffectStyle.SystemMaterial);
            }

            // Reacts to the finger as the tab bar's own glass does. By key, as
            // UIKit names it, whatever the binding calls the property.
            var glass = new UIGlassEffect();
            glass.SetValueForKey(NSNumber.FromBoolean(true), new NSString("interactive"));
            return glass;
        }

        /// <summary>One action in the group: its icon, and a button to VoiceOver.</summary>
        private sealed class Slot : UIView
        {
            private readonly GlassAction _action;
            private readonly UIImageView _icon;

            public Slot(GlassAction action)
            {
                _action = action;
                _icon = Icon(action.Symbol);
                _icon.AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;
                AddSubview(_icon);
                IsAccessibilityElement = true;
                AccessibilityLabel = action.Label;
                AccessibilityTraits = UIAccessibilityTrait.Button;
            }

            public void Activate() => _action.Tapped();

            public override void LayoutSubviews()
            {
                base.LayoutSubviews();
                _icon.Frame = Bounds;
            }

            public override bool AccessibilityActivate()
            {
                Activate();
                return true;
            }
        }
    }
}
