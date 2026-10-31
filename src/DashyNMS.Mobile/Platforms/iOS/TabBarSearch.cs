using CoreGraphics;
using Foundation;
using UIKit;

namespace DashyNMS.Mobile;

/// <summary>
/// Search as its own Liquid Glass button beside the tab bar, as Music and
/// other iOS 26 apps have it, for the page showing - so search is in the
/// same place on every page that has it rather than in each page's header.
/// </summary>
/// <remarks>
/// <para>Shell's tab bar is UIKit's, built from view controllers, so it
/// can't take UIKit's own search tab: the button is laid over Shell's tab
/// bar controller instead, placed from where the tab bar's buttons are.
/// Beside a vertical tab bar (iPhone Duo) it sits in the bar's column just
/// above it, in room the bar leaves empty; above a bottom tab bar it floats
/// over the bar's trailing end, and the page keeps its own controls clear
/// of it (<see cref="CoveredBottom"/>).</para>
/// <para>One button for the app; a page takes it over as it appears and
/// gives it up as it goes, so whichever page shows owns the tap.</para>
/// </remarks>
internal static class TabBarSearch
{
    /// <summary>The gap between the button and the tab bar, as between the bar and the screen's edge.</summary>
    private const double Gap = 12;

    private static Host? _host;
    private static object? _owner;

    /// <summary>Liquid Glass needs iOS 26; before that, pages keep search in their header.</summary>
    public static bool IsAvailable => OperatingSystem.IsIOSVersionAtLeast(26);

    /// <summary>Raised when the button moves, so pages can keep their controls clear of it.</summary>
    public static event EventHandler? Moved;

    /// <summary>
    /// How much more of the foot of the screen the button takes, over a bottom
    /// tab bar: nothing beside a vertical one, which it sits in line with.
    /// </summary>
    public static double CoveredBottom => _host is { Button.Hidden: false } host && !host.BesideVerticalBar
        ? host.Button.Frame.Height + Gap
        : 0;

    /// <summary>Shows the button for <paramref name="owner"/>, which <paramref name="tapped"/> answers.</summary>
    public static void Show(object owner, Action tapped, bool isOpen, string description)
    {
        if (!IsAvailable || TabDots.FindTabs() is not { View: { } root })
        {
            return;
        }

        if (_host is null || !ReferenceEquals(_host.Superview, root))
        {
            _host?.RemoveFromSuperview();
            _host = new Host { Frame = root.Bounds };
            root.AddSubview(_host);
        }

        _owner = owner;
        root.BringSubviewToFront(_host);
        _host.Tapped = tapped;
        _host.Description = description;
        _host.Button.Hidden = false;
        SetOpen(owner, isOpen);
        _host.SetNeedsLayout();
    }

    /// <summary>Shows a cross while search is open, and a magnifying glass while it's closed.</summary>
    public static void SetOpen(object owner, bool isOpen)
    {
        if (_host is null || !ReferenceEquals(owner, _owner))
        {
            return;
        }

        _host.SetOpen(isOpen);
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
        Moved?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// A clear layer over the whole tab bar controller, so the button can be
    /// placed again whenever the screen changes - it opens, closes or turns -
    /// passing every touch but the button's through.
    /// </summary>
    private sealed class Host : UIView
    {
        private readonly UIImageView _icon = new() { TintColor = UIColor.Label, ContentMode = UIViewContentMode.Center };
        private CGRect _placed;

        public Host()
        {
            BackgroundColor = UIColor.Clear;
            AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;

            Button = new UIVisualEffectView(Glass()) { ClipsToBounds = true, IsAccessibilityElement = true };
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

        public bool BesideVerticalBar { get; private set; }

        public void SetOpen(bool open)
        {
            var symbol = UIImageSymbolConfiguration.Create(UIFont.PreferredBody.PointSize + 3, UIImageSymbolWeight.Medium);
            _icon.Image = UIImage.GetSystemImage(open ? "xmark" : "magnifyingglass", symbol);
            Button.AccessibilityLabel = open ? "Close search" : Description;
        }

        public override UIView? HitTest(CGPoint point, UIEvent? uievent)
        {
            var hit = base.HitTest(point, uievent);
            return ReferenceEquals(hit, this) ? null : hit;
        }

        public override void SafeAreaInsetsDidChange()
        {
            base.SafeAreaInsetsDidChange();
            SetNeedsLayout();
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            if (Superview is not { } root || TabDots.FindTabs() is not { TabBar: { Hidden: false } tabBar } || tabBar.Window is null)
            {
                return;
            }

            var bar = Pill(tabBar, this);
            if (bar.Width <= 0 || bar.Height <= 0)
            {
                return;
            }

            root.BringSubviewToFront(this);
            BesideVerticalBar = bar.Height > bar.Width;

            // As wide as the vertical bar, or as tall as the bottom one, so it
            // reads as part of the same row.
            var size = BesideVerticalBar ? bar.Width : bar.Height;
            var frame = BesideVerticalBar
                ? new CGRect(bar.GetMidX() - size / 2, bar.Top - Gap - size, size, size)
                : new CGRect(bar.Right - size, bar.Top - Gap - size, size, size);

            Button.Frame = frame;
            Button.Layer.CornerRadius = size / 2;
            _icon.Frame = Button.ContentView.Bounds;

            // Tell pages only when it has really moved, or each layout pass
            // they make would lay this out again.
            if (frame != _placed)
            {
                _placed = frame;
                Moved?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// The visible bar: around the tab buttons. On iOS 26 the bar's own
        /// frame can be the whole edge while its glass is a shorter pill.
        /// </summary>
        private static CGRect Pill(UITabBar tabBar, UIView into)
        {
            var area = CGRect.Null;
            foreach (var control in Controls(tabBar))
            {
                area = area.UnionWith(control.ConvertRectToView(control.Bounds, into));
            }

            return area.IsNull() ? tabBar.ConvertRectToView(tabBar.Bounds, into) : area;
        }

        private static IEnumerable<UIControl> Controls(UIView view)
        {
            foreach (var child in view.Subviews)
            {
                if (child is UIControl { Hidden: false } control && control.Bounds.Width > 0)
                {
                    yield return control;
                }
                else
                {
                    foreach (var inner in Controls(child))
                    {
                        yield return inner;
                    }
                }
            }
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
