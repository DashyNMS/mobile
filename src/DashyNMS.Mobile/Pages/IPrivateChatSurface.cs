namespace DashyNMS.Mobile.Pages;

/// <summary>
/// A page that shows the Intelligence conversation or part of it - the chat,
/// the saved-chat list, a report preview, an enlarged answer graph - and so is
/// hidden from the app switcher in single-chat mode (#230). See
/// <see cref="Intelligence.IntelligenceSnapshotShield"/>.
/// </summary>
public interface IPrivateChatSurface
{
}

/// <summary>Finds a <see cref="IPrivateChatSurface"/> on screen, for the snapshot shield.</summary>
/// <remarks>
/// Lists what every window shows - its page and any sheets over it - and lets
/// <see cref="Intelligence.PrivateScreen"/> decide, which hides unless all of it
/// is known not to be the chat. Content pages are known; a missing window or
/// page, and a container or page type this doesn't place, are unknown. Sheets are
/// read from the window's root and the current Shell section, as a page's own
/// navigation pushes them there; a navigation wrapper counts by the page it shows.
/// </remarks>
internal static class PrivateChatSurfaces
{
    public static bool OnScreen() => Intelligence.PrivateScreen.MayShowChat(Visible());

    private static List<Intelligence.ScreenSurface>? Visible()
    {
        if (Application.Current?.Windows is not { } windows) return null;
        var surfaces = new List<Intelligence.ScreenSurface>();
        foreach (var window in windows)
        {
            if (window.Page is not { } root)
            {
                surfaces.Add(Intelligence.ScreenSurface.Unknown);
                continue;
            }

            foreach (var modal in root.Navigation.ModalStack) surfaces.Add(Classify(modal));
            if (root is Shell shell)
            {
                if (shell.CurrentItem?.CurrentItem is { } section)
                    foreach (var modal in section.Navigation.ModalStack) surfaces.Add(Classify(modal));
                surfaces.Add(Classify(shell.CurrentPage));
            }
            else
            {
                surfaces.Add(Classify(root));
            }
        }

        return surfaces;
    }

    private static Intelligence.ScreenSurface Classify(Page? page) => page switch
    {
        null => Intelligence.ScreenSurface.Unknown,
        IPrivateChatSurface => Intelligence.ScreenSurface.Private,
        NavigationPage navigation => Classify(navigation.CurrentPage),
        TabbedPage tabs => Classify(tabs.CurrentPage),
        FlyoutPage flyout => Classify(flyout.Detail),
        ContentPage => Intelligence.ScreenSurface.NotPrivate,
        _ => Intelligence.ScreenSurface.Unknown,
    };
}
