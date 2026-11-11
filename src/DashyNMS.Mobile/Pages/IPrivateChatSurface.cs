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
internal static class PrivateChatSurfaces
{
    /// <summary>
    /// The current Shell page, or any modal over it, shows the conversation.
    /// Modals are checked on the window's root and the current section, as a
    /// page's own navigation pushes them; either way a sheet over the chat counts.
    /// </summary>
    public static bool OnScreen()
    {
        if (Application.Current?.Windows is not { Count: > 0 } windows) return false;
        foreach (var window in windows)
        {
            if (window.Page is not { } root) continue;
            if (root.Navigation.ModalStack.Any(page => page is IPrivateChatSurface)) return true;
            if (root is not Shell shell) continue;
            if (shell.CurrentPage is IPrivateChatSurface) return true;
            if (shell.CurrentItem?.CurrentItem?.Navigation.ModalStack.Any(page => page is IPrivateChatSurface) == true) return true;
        }

        return false;
    }
}
