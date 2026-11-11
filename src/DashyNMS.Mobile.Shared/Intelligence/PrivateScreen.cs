namespace DashyNMS.Mobile.Intelligence;

/// <summary>What one visible page or sheet is, as far as single-chat privacy (#230) goes.</summary>
public enum ScreenSurface
{
    /// <summary>A page the app knows and that shows none of the conversation.</summary>
    NotPrivate,

    /// <summary>The chat or one of its sheets.</summary>
    Private,

    /// <summary>Missing, or a page or container the head doesn't recognise.</summary>
    Unknown,
}

/// <summary>
/// Whether what is on screen may show the single-chat conversation (#230), for
/// <see cref="IntelligenceSnapshotShield"/>. The head lists a surface for every
/// window's visible page and sheets; this decides, so the fail-closed rule is
/// tested here rather than in MAUI.
/// </summary>
/// <remarks>
/// Only pages known not to show the chat leave it unhidden. Nothing to list (no
/// windows yet), a window without a page, or a page the head can't place counts
/// as possibly the chat: hiding a network page from the app switcher by mistake
/// costs little, showing the private chat can't be taken back.
/// </remarks>
public static class PrivateScreen
{
    /// <summary>True unless every listed surface is known not to be private, and there is at least one.</summary>
    public static bool MayShowChat(IEnumerable<ScreenSurface>? surfaces)
    {
        if (surfaces is null) return true;
        var any = false;
        foreach (var surface in surfaces)
        {
            if (surface != ScreenSurface.NotPrivate) return true;
            any = true;
        }

        return !any;
    }
}
