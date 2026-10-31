using System.Runtime.Versioning;
using CoreGraphics;
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
    /// Shell's tab bar controller, changed over to UIKit's tabs (iOS 18) on
    /// iOS 26 and later so it can show a search tab.
    /// </summary>
    /// <remarks>
    /// <para>Shell hands UIKit view controllers, and keeps doing so as tabs
    /// are pinned; a search tab needs UIKit's newer tabs instead, and the two
    /// don't mix. So once the tab bar has first appeared - never while Shell
    /// is still building it, which is where #308's black screen came from -
    /// the view controllers become a tab each, the same tab for the same
    /// controller every time, with the search tab after them while a page
    /// wants it. Shell still selects by view controller and hears of each
    /// selection there, and still reads back the view controllers it set.</para>
    /// <para>Anything that goes wrong along the way puts the view controllers
    /// back as Shell set them (<see cref="TabBarSearch.Failed"/>).</para>
    /// </remarks>
    private sealed class AppShellItemRenderer : ShellItemRenderer, TabBarSearch.IHost
    {
        private readonly Dictionary<UIViewController, UITab> _tabs = [];
        private UIViewController[] _controllers = [];
        private UISearchTab? _search;
        private UIViewController? _searchPlaceholder;
        private bool _active;
        private bool _tried;
        private bool _applying;
        private int _nextId;

        /// <summary>The view controller Shell last selected, as Shell sees it.</summary>
        private UIViewController? _shellSelected;

        /// <summary>Telling Shell of a tab now, so UIKit's echo of it isn't told again.</summary>
        private bool _tellingShell;

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

        public override UIViewController[]? ViewControllers
        {
            // Shell's own, as it set them: UIKit's, with tabs, may only hold
            // the ones it has shown so far, and has the search tab's too.
            get => _active ? _controllers : base.ViewControllers;
            set
            {
                // UIKit sets its own as it applies the tabs: let those through.
                if (!_active || _applying)
                {
                    base.ViewControllers = value;
                    return;
                }

                _controllers = value?.Where(controller => !ReferenceEquals(controller, _searchPlaceholder)).ToArray() ?? [];
                Guard(nameof(ViewControllers), ApplyTabs);
            }
        }

        public override UIViewController SelectedViewController
        {
            get => base.SelectedViewController;
            set
            {
                // Before UIKit hears of it: selecting it there says a tab was
                // selected, and that mustn't look like a new one (build 188
                // went round that until the stack ran out).
                _shellSelected = value;

                // Shell selects by view controller: select its tab to match.
                if (_active && value is not null && _tabs.TryGetValue(value, out var tab))
                {
                    Guard(nameof(SelectedTab), () =>
                    {
                        if (OperatingSystem.IsIOSVersionAtLeast(18) && !ReferenceEquals(SelectedTab, tab))
                        {
                            SelectedTab = tab;
                        }
                    });
                }

                try
                {
                    base.SelectedViewController = value!;
                }
                catch (Exception error) when (_active)
                {
                    // UIKit may not take a view controller once it has tabs:
                    // back to Shell's view controllers, and select again.
                    Fail(nameof(SelectedViewController), error);
                    base.SelectedViewController = value!;
                }
            }
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);

            // Once Shell has built the tab bar and shown it, and only once.
            if (!_tried && TabBarSearch.CanTry && OperatingSystem.IsIOSVersionAtLeast(26))
            {
                _tried = true;
                BeginInvokeOnMainThread(Start);
            }
        }

        // Opening, folding or turning iPhone Duo with the tabs in use: if the
        // app dies on it, the next launch leaves them alone.
        public override void ViewWillTransitionToSize(CGSize toSize, IUIViewControllerTransitionCoordinator coordinator)
        {
            if (_active)
            {
                TabBarSearch.Risky();
            }

            base.ViewWillTransitionToSize(toSize, coordinator);
        }

        public override void ViewWillLayoutSubviews()
        {
            base.ViewWillLayoutSubviews();

            // Shell loads tab icons after it sets the view controllers, and a
            // tab's image is fixed once made: make again any that had none.
            if (_active && OperatingSystem.IsIOSVersionAtLeast(18)
                && _tabs.Any(pair => pair.Value.Image is null && pair.Key.TabBarItem?.Image is not null))
            {
                foreach (var stale in _tabs.Where(pair => pair.Value.Image is null).Select(pair => pair.Key).ToList())
                {
                    _tabs.Remove(stale);
                }

                Guard("icons", ApplyTabs);
            }
        }

        public void ShowSearch(bool shown)
        {
            if (_active)
            {
                Guard(nameof(ShowSearch), ApplyTabs);
            }
        }

        [SupportedOSPlatform("ios26.0")]
        private void Start()
        {
            // Not Sign in's, which shows no tab bar, nor one already closed.
            if (_active || Handle == ObjCRuntime.NativeHandle.Zero || TabBar is not { Hidden: false }
                || base.ViewControllers is not { Length: > 1 } controllers || View?.Window is null)
            {
                return;
            }

            TabBarSearch.Starting();
            try
            {
                _controllers = controllers;
                _active = true;

                // Shell decides whether a tab may be selected by its view
                // controller; UIKit now asks by tab, so ask Shell for the tab's.
                var shell = ShouldSelectViewController;
                ShouldSelectTab = (controller, tab) =>
                {
                    try
                    {
                        return ShouldSelect(controller, tab, shell);
                    }
                    catch (Exception error)
                    {
                        Fail(nameof(ShouldSelectTab), error);
                        return true;
                    }
                };

                // UIKit selects a tapped tab itself, past Shell, which shows
                // and hides pages - and pushes onto - the tab it last chose.
                DidSelectTab += (_, _) => TellShell();

                _shellSelected = base.SelectedViewController;
                ApplyTabs();
                TabBarSearch.Started();
            }
            catch (Exception error)
            {
                Fail(nameof(Start), error);
            }
        }

        [SupportedOSPlatform("ios18.0")]
        private bool ShouldSelect(UITabBarController controller, UITab tab, UITabBarSelection? shell)
        {
            if (ReferenceEquals(tab, _search))
            {
                TabBarSearch.Tap();
                return false;
            }

            if (!_active || ControllerFor(tab) is not { } viewController)
            {
                return true;
            }

            var allowed = shell?.Invoke(controller, viewController) ?? true;
            if (allowed)
            {
                BeginInvokeOnMainThread(TellShell);
            }

            return allowed;
        }

        /// <summary>
        /// Tells Shell of the tab UIKit has selected. Shell hears of a new tab
        /// only through SelectedViewController, which UIKit doesn't set when a
        /// tab is tapped: Shell went on thinking the first tab was showing, so
        /// it never showed the new one's page (the network map didn't load),
        /// and More's Settings was pushed onto the hidden Dashboard tab.
        /// Judged against what Shell last chose - UIKit's own selection has
        /// already moved.
        /// </summary>
        private void TellShell()
        {
            if (_tellingShell || !_active || !OperatingSystem.IsIOSVersionAtLeast(18) || SelectedTab is not { } selected
                || ControllerFor(selected) is not { } now || ReferenceEquals(now, _shellSelected))
            {
                return;
            }

            _tellingShell = true;
            try
            {
                Guard(nameof(TellShell), () => SelectedViewController = now);
            }
            finally
            {
                _tellingShell = false;
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

        private void Guard(string where, Action action)
        {
            if (!_active || Handle == ObjCRuntime.NativeHandle.Zero || !OperatingSystem.IsIOSVersionAtLeast(18))
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception error)
            {
                Fail(where, error);
            }
        }

        // Back to the view controllers as Shell set them, the one it had
        // selected still selected, and search back in the page's header.
        private void Fail(string where, Exception error)
        {
            if (!_active)
            {
                return;
            }

            var selected = base.SelectedViewController;
            _active = false;
            TabBarSearch.Failed(where, error);
            try
            {
                _applying = true;
                base.ViewControllers = _controllers;
                if (selected is not null && _controllers.Contains(selected))
                {
                    base.SelectedViewController = selected;
                }
            }
            catch (Exception again)
            {
                TabBarSearch.Failed("putting the tab bar back", again);
            }
            finally
            {
                _applying = false;
            }
        }
    }
}
