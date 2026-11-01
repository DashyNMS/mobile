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
        private bool _appeared;
        private bool _initialLayoutCompleted;
        private bool _initialLayoutQueued;

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
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            _appeared = true;
            QueueInitialLayout();
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

                // Mark complete before laying out: that calls ViewDidLayoutSubviews again.
                _initialLayoutCompleted = true;
                TraitOverrides.HorizontalSizeClass = UIUserInterfaceSizeClass.Compact;
                SetTabBarHidden(!ShellItemController.ShowTabs, false);
                view.SetNeedsLayout();
                view.LayoutIfNeeded();
                TabBar.SetNeedsLayout();
                TabBar.LayoutIfNeeded();
                TabBarButtons.Relayout(this);

                (IPlatformApplication.Current?.Services.GetService(typeof(DiagnosticsLog)) as DiagnosticsLog)?.Note(
                    "Layout", $"Initial tabs: show={ShellItemController.ShowTabs}, hidden={TabBarHidden}, bounds={view.Bounds}, bar={TabBar.Frame}");
            });
        }
    }
}
