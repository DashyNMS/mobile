using System.Runtime.Versioning;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using UIKit;

namespace DashyNMS.Mobile;

/// <summary>
/// Keeps Shell's tab layout consistent when a phone starts on a wide display,
/// and gives Shell's tab bar UIKit's own search tab (<see cref="TabBarSearch"/>).
/// MAUI applies the layout workaround to iPads only, leaving a wide phone launch
/// to initialise a different native tab layout from a narrow phone launch.
/// </summary>
internal sealed class AppShellRenderer : ShellRenderer
{
    public AppShellRenderer()
    {
    }

    // Assign ShellItem as the base method does: without it the tab controller
    // has no sections to show and the window stays black.
    protected override IShellItemRenderer CreateShellItemRenderer(ShellItem item)
        => new AppShellItemRenderer(this) { ShellItem = item };

    /// <summary>
    /// Shell's tab bar controller, using UIKit's tabs (iOS 18) on iOS 26 and
    /// later so it can show a search tab.
    /// </summary>
    /// <remarks>
    /// <para>Shell hands UIKit view controllers, and keeps doing so as tabs
    /// are pinned; a search tab needs UIKit's newer tabs instead, and the two
    /// don't mix. So the view controllers Shell sets are turned into a tab
    /// each, the same tab for the same controller every time, with the search
    /// tab after them while a page wants it. Shell still selects by view
    /// controller, and still hears of each selection there, so the rest of
    /// Shell works as before.</para>
    /// <para>Before iOS 26 nothing changes: view controllers as Shell sets them.</para>
    /// </remarks>
    private sealed class AppShellItemRenderer : ShellItemRenderer, TabBarSearch.IHost
    {
        private readonly Dictionary<UIViewController, UITab> _tabs = [];
        private UIViewController[] _controllers = [];
        private UISearchTab? _search;
        private UIViewController? _searchPlaceholder;
        private bool _applying;
        private int _nextId;

        public AppShellItemRenderer(IShellContext context) : base(context)
        {
            // Set this before Shell creates the child controllers, so a cold
            // launch and subsequent display changes use the same tab layout.
            if (OperatingSystem.IsIOSVersionAtLeast(18))
            {
                TraitOverrides.HorizontalSizeClass = UIUserInterfaceSizeClass.Compact;
            }

            TabBarSearch.Register(this);
        }

        [SupportedOSPlatformGuard("ios26.0")]
        private static bool UsesTabs => TabBarSearch.IsAvailable;

        public override UIViewController[]? ViewControllers
        {
            get => base.ViewControllers;
            set
            {
                // UIKit sets its own as it applies the tabs: let those through.
                if (!UsesTabs || _applying)
                {
                    base.ViewControllers = value;
                    return;
                }

                _controllers = value?.Where(controller => !ReferenceEquals(controller, _searchPlaceholder)).ToArray() ?? [];
                ApplyTabs();
            }
        }

        public override UIViewController SelectedViewController
        {
            get => base.SelectedViewController;
            set
            {
                // Shell selects by view controller: select its tab to match.
                if (UsesTabs && value is not null && _tabs.TryGetValue(value, out var tab) && !ReferenceEquals(SelectedTab, tab))
                {
                    SelectedTab = tab;
                }

                base.SelectedViewController = value!;
            }
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            if (!UsesTabs)
            {
                return;
            }

            // Shell decides whether a tab may be selected by its view
            // controller; UIKit now asks by tab, so ask Shell for the tab's.
            var shell = ShouldSelectViewController;
            ShouldSelectTab = (controller, tab) =>
            {
                if (ReferenceEquals(tab, _search))
                {
                    TabBarSearch.Tap();
                    return false;
                }

                if (ControllerFor(tab) is not { } viewController)
                {
                    return true;
                }

                var allowed = shell?.Invoke(controller, viewController) ?? true;
                if (allowed)
                {
                    // Shell hears of a new tab through SelectedViewController;
                    // with tabs, UIKit may not set it, so once it's selected,
                    // pass it on.
                    BeginInvokeOnMainThread(() =>
                    {
                        if (SelectedTab is { } selected && ControllerFor(selected) is { } now && !ReferenceEquals(now, base.SelectedViewController))
                        {
                            SelectedViewController = now;
                        }
                    });
                }

                return allowed;
            };
        }

        public override void ViewWillLayoutSubviews()
        {
            base.ViewWillLayoutSubviews();

            // Shell loads tab icons after it sets the view controllers, and a
            // tab's image is fixed once made: make again any that had none.
            if (UsesTabs && _tabs.Any(pair => pair.Value.Image is null && pair.Key.TabBarItem?.Image is not null))
            {
                foreach (var stale in _tabs.Where(pair => pair.Value.Image is null).Select(pair => pair.Key).ToList())
                {
                    _tabs.Remove(stale);
                }

                ApplyTabs();
            }
        }

        public void ShowSearch(bool shown)
        {
            if (UsesTabs && _controllers.Length > 0)
            {
                ApplyTabs();
            }
        }

        [SupportedOSPlatform("ios18.0")]
        private void ApplyTabs()
        {
            foreach (var gone in _tabs.Keys.Where(controller => !_controllers.Contains(controller)).ToList())
            {
                _tabs.Remove(gone);
            }

            var tabs = _controllers.Select(TabFor).ToList();
            if (TabBarSearch.IsShown)
            {
                _search ??= new UISearchTab(_ => _searchPlaceholder ??= new UIViewController()) { AutomaticallyActivatesSearch = false };
                tabs.Add(_search);
            }

            var selected = base.SelectedViewController;
            _applying = true;
            try
            {
                SetTabs([.. tabs], false);
            }
            finally
            {
                _applying = false;
            }

            // Setting the tabs can select the first: keep Shell's.
            if (selected is not null && _tabs.TryGetValue(selected, out var tab) && !ReferenceEquals(SelectedTab, tab))
            {
                SelectedTab = tab;
            }
        }

        // One tab per view controller, kept, so UIKit sees the same tabs
        // each time and doesn't rebuild them as search comes and goes.
        [SupportedOSPlatform("ios18.0")]
        private UITab TabFor(UIViewController controller)
        {
            if (!_tabs.TryGetValue(controller, out var tab))
            {
                var item = controller.TabBarItem;
                tab = new UITab(item?.Title ?? controller.Title ?? string.Empty, item?.Image, "shell-" + _nextId++, _ => controller);
                _tabs[controller] = tab;
            }

            return tab;
        }

        [SupportedOSPlatform("ios18.0")]
        private UIViewController? ControllerFor(UITab tab) =>
            _tabs.FirstOrDefault(pair => ReferenceEquals(pair.Value, tab)).Key;
    }
}
