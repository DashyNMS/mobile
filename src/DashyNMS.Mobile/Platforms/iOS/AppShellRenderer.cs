using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using UIKit;
using Foundation;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile;

/// <summary>
/// Keeps Shell's tab layout consistent when a phone starts on a wide display.
/// MAUI applies this workaround to iPads only, leaving a wide phone launch
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

    private sealed class AppShellItemRenderer : ShellItemRenderer
    {
        private readonly TabSettlePolicy _settle = new();
        private bool _appeared;
        private bool _initialLayoutCompleted;
        private bool _initialLayoutQueued;
        private bool _settleQueued;
        private TabSettleStage _lastStage;

        public AppShellItemRenderer(IShellContext context) : base(context)
        {
            // Set this before Shell creates the child controllers, so a cold
            // launch and subsequent display changes use the same tab layout.
            if (OperatingSystem.IsIOSVersionAtLeast(18))
            {
                TraitOverrides.HorizontalSizeClass = UIUserInterfaceSizeClass.Compact;
            }
        }

        // The tab bar may have moved - iPhone Duo folded, opened or turned -
        // so the search button by it moves too.
        public override void ViewDidLayoutSubviews()
        {
            base.ViewDidLayoutSubviews();
            TabBarButtons.Relayout(this);
            QueueInitialLayout();
            QueueSettle();
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            _appeared = true;
            QueueInitialLayout();
            QueueSettle();
        }

        public override void ViewDidDisappear(bool animated)
        {
            _appeared = false;
            base.ViewDidDisappear(animated);
        }

        /// <summary>
        /// A wide-screen cold launch can lay out the tab controller before it
        /// has its final window traits. Rotation repairs that layout; perform
        /// one attached pass instead, without changing selection or rebuilding tabs.
        /// </summary>
        private void QueueInitialLayout()
        {
            if (!OperatingSystem.IsIOSVersionAtLeast(18) || !_appeared || _initialLayoutCompleted || _initialLayoutQueued)
                return;

            _initialLayoutQueued = true;
            NSRunLoop.Main.BeginInvokeOnMainThread(() =>
            {
                _initialLayoutQueued = false;
                if (!_appeared || _initialLayoutCompleted || View is not { Window: not null } view || ShellItem is null)
                    return;

                if (ShellItemController.ShowTabs && (ViewControllers is null || ViewControllers.Length == 0))
                    return; // Wait for a subsequent layout after MAUI supplies the tabs.

                // Mark complete before laying out: that calls ViewDidLayoutSubviews again.
                _initialLayoutCompleted = true;
                TraitOverrides.HorizontalSizeClass = UIUserInterfaceSizeClass.Compact;
                SetTabBarHidden(!ShellItemController.ShowTabs, false);
                LayOut(view);
                NoteTabs("Initial tabs", view);
            });
        }

        /// <summary>
        /// On the Duo's inner display a landscape cold launch can leave the tab
        /// bar with all its items but no pill until the phone is turned (#372),
        /// and one refresh on the first run-loop turn was not enough. Checks a
        /// few times over about a second, nudging harder each time, as
        /// <see cref="TabSettlePolicy"/> decides; starts again when the view's
        /// size changes. Portrait and narrow views are never nudged.
        /// </summary>
        /// <remarks>
        /// Only cheap checks run here, as this follows every layout; the window
        /// search for the pill waits for the queued check. Liquid Glass (iOS 26)
        /// is needed: earlier tab bars have no pill to find.
        /// </remarks>
        private void QueueSettle()
        {
            if (!TabBarButtons.IsAvailable || !_appeared || View is not { Window: not null } view)
                return;

            if (_settle.Resize(view.Bounds.Width, view.Bounds.Height))
                _lastStage = TabSettleStage.None;

            if (_settleQueued || _settle.IsFinished || !_initialLayoutCompleted
                || !ShellItemController.ShowTabs || view.Bounds.Width <= view.Bounds.Height)
                return;

            _settleQueued = true;
            NSRunLoop.Main.BeginInvokeOnMainThread(Settle);
        }

        private void Settle()
        {
            _settleQueued = false;
            if (!_appeared || View is not { Window: not null } view || ShellItem is null)
                return;

            var showTabs = ShellItemController.ShowTabs;
            var isWide = view.Bounds.Width > view.Bounds.Height;
            var controllers = ViewControllers is { Length: > 0 };
            var pillFound = showTabs && isWide && controllers && TabBarButtons.HasPill(this);
            var step = _settle.Check(showTabs, isWide, controllers, pillFound);
            switch (step.Action)
            {
                case TabSettleAction.Nudge:
                    // Queued before nudging: the nudge lays out, and that must not
                    // queue a second, immediate check that skips the wait.
                    _settleQueued = true;
                    Nudge(step.Stage, view);
                    _lastStage = step.Stage;
                    NoteTabs($"Initial tabs: nudged ({step.Stage})", view);
                    var weak = new WeakReference<AppShellItemRenderer>(this);
                    NSTimer.CreateScheduledTimer(step.NextCheck.TotalSeconds, false, _ =>
                    {
                        if (weak.TryGetTarget(out var renderer))
                            renderer.Settle();
                    });
                    break;
                case TabSettleAction.GiveUp:
                    NoteTabs($"Initial tabs: no pill after {_lastStage}", view);
                    break;
                case TabSettleAction.None when pillFound && _lastStage != TabSettleStage.None:
                    NoteTabs($"Initial tabs: pill after {_lastStage}", view);
                    break;
            }
        }

        /// <summary>
        /// Pushes UIKit to build the pill a rotation would. Each stage puts back
        /// what it changes, keeping the selection and the compact tab layout.
        /// </summary>
        private void Nudge(TabSettleStage stage, UIView view)
        {
            if (!OperatingSystem.IsIOSVersionAtLeast(26))
                return;

            switch (stage)
            {
                case TabSettleStage.Refresh when ViewControllers is { Length: > 0 } controllers:
                    var selected = SelectedViewController;
                    SetViewControllers(controllers.ToArray(), false);
                    if (selected is not null && !ReferenceEquals(SelectedViewController, selected))
                        SelectedViewController = selected;
                    break;
                case TabSettleStage.ToggleBar:
                    SetTabBarHidden(true, false);
                    LayOut(view);
                    SetTabBarHidden(false, false);
                    break;
                case TabSettleStage.FlipTraits:
                    TraitOverrides.HorizontalSizeClass = UIUserInterfaceSizeClass.Regular;
                    LayOut(view);
                    TraitOverrides.HorizontalSizeClass = UIUserInterfaceSizeClass.Compact;
                    break;
            }

            LayOut(view);
        }

        private void LayOut(UIView view)
        {
            view.SetNeedsLayout();
            view.LayoutIfNeeded();
            TabBar.SetNeedsLayout();
            TabBar.LayoutIfNeeded();
            TabBarButtons.Relayout(this);
        }

        private void NoteTabs(string what, UIView view) =>
            (IPlatformApplication.Current?.Services.GetService(typeof(DiagnosticsLog)) as DiagnosticsLog)?.Note(
                "Layout", $"{what}: horizontal={TraitCollection.HorizontalSizeClass}, vertical={TraitCollection.VerticalSizeClass}, show={ShellItemController.ShowTabs}, hidden={(OperatingSystem.IsIOSVersionAtLeast(18) ? TabBarHidden : TabBar.Hidden)}, bounds={view.Bounds}, bar={TabBar.Frame}, controllers={ViewControllers?.Length ?? 0}, items={TabBar.Items?.Length ?? 0}");
    }
}
