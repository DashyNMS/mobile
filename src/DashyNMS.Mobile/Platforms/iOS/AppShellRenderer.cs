using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using UIKit;

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
        public AppShellItemRenderer(IShellContext context) : base(context)
        {
            // Set this before Shell creates the child controllers, so a cold
            // launch and subsequent display changes use the same tab layout.
            if (OperatingSystem.IsIOSVersionAtLeast(18))
            {
                TraitOverrides.HorizontalSizeClass = UIUserInterfaceSizeClass.Compact;
            }
        }
    }
}
