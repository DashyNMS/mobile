using CoreGraphics;
using DashyNMS.Mobile.Services;
using Foundation;
using UIKit;

namespace DashyNMS.Mobile;

/// <summary>
/// Search as its own Liquid Glass button in the tab bar's row, as Music and
/// other iOS 26 apps have it, for the page showing - so search is in the
/// same place on every page that has it rather than in each page's header.
/// </summary>
/// <remarks>
/// <para>Shell's tab bar is UIKit's, built from view controllers, so it
/// can't take UIKit's own search tab: the button is laid over Shell's tab
/// bar controller instead, placed from where the tab bar's glass is. Beside
/// a bottom tab bar it sits at the bar's right, a gap apart; with a vertical
/// tab bar (iPhone Duo) it sits above it in the same column. Where a bottom
/// bar leaves no room at its right, it floats above the bar's right end, and
/// the page keeps its own controls clear of it (<see cref="CoveredBottom"/>).</para>
/// <para>It's placed again each time the tab bar controller lays out
/// (<see cref="Relayout"/>), and moves to whichever controller that is:
/// placing it only as a page appeared left it where the tab bar had been,
/// over the tabs or off the screen, once iPhone Duo was folded or turned.</para>
/// <para>One button for the app; a page takes it over as it appears and
/// gives it up as it goes, so whichever page shows owns the tap.</para>
/// </remarks>
internal static class TabBarSearch
{
    /// <summary>The gap between the button and the tab bar.</summary>
    private const double Gap = 12;

    private static Host? _host;

    /// <summary>Where the tab bar's pill was last found, in the button's layer's points; empty if it wasn't.</summary>
    private static CGRect _pill = CGRect.Empty;
    private static object? _owner;
    private static string? _reported;

    /// <summary>Liquid Glass needs iOS 26; before that, pages keep search in their header.</summary>
    public static bool IsAvailable => OperatingSystem.IsIOSVersionAtLeast(26);

    /// <summary>
    /// Whether the button is on the screen by the tab bar. Pages keep search
    /// in their header while it isn't.
    /// </summary>
    public static bool IsPlaced { get; private set; }

    /// <summary>Raised when the button moves, so pages can keep their controls clear of it.</summary>
    public static event EventHandler? Moved;

    /// <summary>
    /// How much more of the foot of the screen the button takes: only when
    /// it has had to float above a bottom tab bar rather than sit beside it.
    /// </summary>
    public static double CoveredBottom => IsPlaced && _host is { Button.Hidden: false, AboveBottomBar: true } host
        ? host.Button.Frame.Height + Gap
        : 0;

    /// <summary>Shows the button for <paramref name="owner"/>, which <paramref name="tapped"/> answers.</summary>
    public static void Show(object owner, Action tapped, bool isOpen, string description)
    {
        if (!IsAvailable)
        {
            return;
        }

        _host ??= new Host();
        _owner = owner;
        _host.Tapped = tapped;
        _host.Description = description;
        _host.SetOpen(isOpen);
        _host.Button.Hidden = false;
        if (TabDots.FindTabs() is { } tabs)
        {
            Relayout(tabs);
        }
    }

    /// <summary>Shows a cross while search is open, and a magnifying glass while it's closed.</summary>
    public static void SetOpen(object owner, bool isOpen)
    {
        if (_host is not null && ReferenceEquals(owner, _owner))
        {
            _host.SetOpen(isOpen);
        }
    }

    /// <summary>Hides the button, unless another page has taken it over since.</summary>
    public static void Hide(object owner)
    {
        if (_host is null || !ReferenceEquals(owner, _owner))
        {
            return;
        }

        _owner = null;
        _host.Tapped = null;
        _host.Button.Hidden = true;
        IsPlaced = false;
        Moved?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Places the button by <paramref name="tabs"/>' tab bar - from its
    /// ViewDidLayoutSubviews, and once more straight after, as the tab bar
    /// lays out its own glass after the controller does.
    /// </summary>
    public static void Relayout(UITabBarController tabs)
    {
        if (_host is null || _owner is null || tabs.View is not { Window: not null } root)
        {
            return;
        }

        Place(tabs, root);
        NSRunLoop.Main.BeginInvokeOnMainThread(() =>
        {
            if (_owner is not null && tabs.View is { Window: not null } later)
            {
                Place(tabs, later);
            }
        });
    }

    private static void Place(UITabBarController tabs, UIView root)
    {
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

        if (tabs.TabBar is not { Hidden: false, Window: not null } tabBar)
        {
            Unplace(host);
            return;
        }

        var (bar, from) = Pill(tabs, tabBar, host);
        _pill = from.StartsWith("bar", StringComparison.Ordinal) ? CGRect.Empty : bar;
        if (bar.Width <= 0 || bar.Height <= 0)
        {
            Unplace(host);
            return;
        }

        CGRect frame;
        var vertical = bar.Height > bar.Width;
        host.AboveBottomBar = false;
        if (vertical)
        {
            // Above it, in its column, as wide as it is.
            var size = bar.Width;
            frame = new CGRect(bar.GetMidX() - size / 2, bar.Top - Gap - size, size, size);
        }
        else
        {
            // At its right, a gap apart, as tall as it is - or above its
            // right end if the screen has no room there.
            var size = bar.Height;
            frame = new CGRect(bar.Right + Gap, bar.GetMidY() - size / 2, size, size);
            if (frame.Right > host.Bounds.Width - host.SafeAreaInsets.Right)
            {
                frame = new CGRect(bar.Right - size, bar.Top - Gap - size, size, size);
                host.AboveBottomBar = true;
            }
        }

        // Never off the screen, nor under the status bar: if that's where it
        // would go, it isn't shown, and the page keeps search in its header.
        var placed = frame.Top >= host.SafeAreaInsets.Top && frame.Left >= 0
            && frame.Bottom <= host.Bounds.Height && frame.Right <= host.Bounds.Width;
        host.Button.Alpha = placed ? 1 : 0;
        var moved = frame != host.Button.Frame || placed != IsPlaced;
        IsPlaced = placed;
        host.Button.Frame = frame;
        host.Button.Layer.CornerRadius = frame.Height / 2;
        host.Layout();
        Report(tabBar, host, bar, from, frame);
        if (moved)
        {
            Moved?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// The tab bar's pill and the button, in <paramref name="view"/>'s points,
    /// for what keeps clear of them across its own row (<c>CoveredEdges.Beside</c>);
    /// nothing where the pill wasn't found.
    /// </summary>
    public static IReadOnlyList<CGRect> Occupied(UIView view)
    {
        if (_host is not { Window: not null } host || _pill.IsEmpty)
        {
            return [];
        }

        var areas = new List<CGRect> { host.ConvertRectToView(_pill, view) };
        if (IsPlaced)
        {
            areas.Add(host.ConvertRectToView(host.Button.Frame, view));
        }

        return areas;
    }

    // Not shown: no tab bar, or nothing to place it by.
    private static void Unplace(Host host)
    {
        host.Button.Alpha = 0;
        if (IsPlaced)
        {
            IsPlaced = false;
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
    /// leaving out the page showing and the button itself.
    /// </remarks>
    private static (CGRect Bar, string From) Pill(UITabBarController tabs, UITabBar tabBar, UIView into)
    {
        var window = into.Window ?? tabBar.Window;
        var page = tabs.SelectedViewController?.View;
        // Not into the page's own views - a list can hold thousands - nor the button's.
        var candidates = window is null
            ? []
            : Descendants(window, view => view.Hidden || ReferenceEquals(view, into) || ReferenceEquals(view, page))
                .Where(view => view.Alpha > 0 && view.Bounds.Width > 0 && view.Bounds.Height > 0)
                .ToList();

        // The pill itself, the biggest if there are several (one per state).
        var platter = candidates
            .Where(view => Name(view).Contains("TabBarItemPlatterView", StringComparison.Ordinal))
            .Select(view => (View: view, Frame: view.ConvertRectToView(view.Bounds, into)))
            .OrderByDescending(found => found.Frame.Width * found.Frame.Height)
            .FirstOrDefault();
        if (platter.View is not null)
        {
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

    // Where the button went and what it was placed from, in the diagnostics
    // log Settings shares - only when it changes. The tab bar's glass on
    // iPhone Duo can't be seen any other way off the device.
    private static void Report(UITabBar tabBar, UIView host, CGRect bar, string from, CGRect frame)
    {
        var bounds = tabBar.ConvertRectToView(tabBar.Bounds, host);
        var text = $"screen {host.Bounds.Width:0.#}×{host.Bounds.Height:0.#}, tab bar {Text(bounds)}, glass {Text(bar)} from {from} → button {Text(frame)}";
        if (text == _reported)
        {
            return;
        }

        _reported = text;
        (IPlatformApplication.Current?.Services.GetService(typeof(DiagnosticsLog)) as DiagnosticsLog)?.Note("Layout", "Search button: " + text);

        static string Text(CGRect r) => $"{r.Left:0.#},{r.Top:0.#} {r.Width:0.#}×{r.Height:0.#}";
    }

    /// <summary>
    /// A clear layer over the whole tab bar controller holding the button,
    /// passing every touch but the button's through.
    /// </summary>
    private sealed class Host : UIView
    {
        private readonly UIImageView _icon = new() { TintColor = UIColor.Label, ContentMode = UIViewContentMode.Center };

        public Host()
        {
            BackgroundColor = UIColor.Clear;
            AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;

            Button = new UIVisualEffectView(Glass()) { ClipsToBounds = true, IsAccessibilityElement = true, Alpha = 0 };
            Button.AccessibilityTraits = UIAccessibilityTrait.Button;
            Button.ContentView.AddSubview(_icon);

            // On the glass itself: interactive glass keeps a button inside it
            // from hearing the tap.
            Button.AddGestureRecognizer(new UITapGestureRecognizer(() => Tapped?.Invoke()));
            AddSubview(Button);
            SetOpen(false);
        }

        public UIVisualEffectView Button { get; }

        public Action? Tapped { get; set; }

        public string Description { get; set; } = "Search";

        public bool AboveBottomBar { get; set; }

        public void SetOpen(bool open)
        {
            var symbol = UIImageSymbolConfiguration.Create(UIFont.PreferredBody.PointSize + 3, UIImageSymbolWeight.Medium);
            _icon.Image = UIImage.GetSystemImage(open ? "xmark" : "magnifyingglass", symbol);
            Button.AccessibilityLabel = open ? "Close search" : Description;
        }

        public void Layout() => _icon.Frame = Button.ContentView.Bounds;

        public override UIView? HitTest(CGPoint point, UIEvent? uievent)
        {
            var hit = base.HitTest(point, uievent);
            return ReferenceEquals(hit, this) ? null : hit;
        }

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
    }
}
