using DashyNMS.Mobile.Services;
#if IOS
using UIKit;
#endif

namespace DashyNMS.Mobile.Adapters;

/// <summary>
/// Watches for the blank page of #107 - a tab (Alerts, Dashboard) with no UI
/// after the phone is unlocked, until the app is restarted, on iOS 26 and 27 -
/// and puts it right. Also notes pages appearing and the app going to the
/// background and back in the diagnostics log, so a report says what led up
/// to it.
/// </summary>
/// <remarks>
/// The cause is likely inside Shell on iOS 26+, where the tab's page keeps
/// its view but isn't laid out again. So shortly after the app comes back,
/// the page showing is checked: no handler, no size, content without either,
/// or (iOS) a view that isn't in the window or is hidden. If it looks blank
/// it's laid out again; if it still does, its tab is rebuilt with a fresh
/// page (<see cref="AppShell.RebuildCurrentTab"/>). Each step is logged, so
/// diagnostics show which it was and whether it helped.
/// </remarks>
internal static class PageHealth
{
    /// <summary>Left for the platform to settle after coming back, before checking.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(500);

    private static DateTime _lastCheck;

    public static void Watch(Application app, Window window, DiagnosticsLog log)
    {
        app.PageAppearing += (_, page) => log.Note("Page", $"{Name(page)} appeared");
        app.PageDisappearing += (_, page) => log.Note("Page", $"{Name(page)} disappeared");
        window.Stopped += (_, _) => log.Note("App", "Went to the background");
        window.Resumed += (_, _) =>
        {
            log.Note("App", "Came back");
            Check(window, log);
        };

        // Unlocking can bring only Activated, so both check - once.
        window.Activated += (_, _) => Check(window, log);
    }

    private static void Check(Window window, DiagnosticsLog log)
    {
        if (DateTime.UtcNow - _lastCheck < TimeSpan.FromSeconds(2))
        {
            return;
        }

        _lastCheck = DateTime.UtcNow;
        window.Dispatcher.DispatchDelayed(Settle, () => _ = CheckAsync(window, log));
    }

    private static async Task CheckAsync(Window window, DiagnosticsLog log)
    {
        try
        {
            if (Shell.Current?.CurrentPage is not { } page)
            {
                return;
            }

            if (Problem(page) is not { } problem)
            {
                log.Note("Page", $"{Name(page)} fine after coming back ({page.Width:0}x{page.Height:0})");
                return;
            }

            log.Note("Page", $"{Name(page)} looks blank after coming back: {problem}. Laying it out again");
            LayOutAgain(page);
            await Task.Delay(Settle);

            if (Problem(page) is not { } still)
            {
                log.Note("Page", $"{Name(page)} fine after laying out again");
                return;
            }

            log.Note("Page", $"{Name(page)} still blank: {still}. Rebuilding its tab");
            var rebuilt = window.Page is AppShell shell && shell.RebuildCurrentTab();
            log.Note("Page", rebuilt ? "Tab rebuilt" : "Not a tab that can be rebuilt");
        }
        catch (Exception ex)
        {
            // Diagnosis must never take the app down with it.
            log.Note("Page", $"Check after coming back failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Why the page looks blank, or null if it doesn't.</summary>
    private static string? Problem(Page page)
    {
        if (page.Handler is null)
        {
            return "no handler";
        }

        if (page.Width <= 0 || page.Height <= 0)
        {
            return $"no size ({page.Width:0}x{page.Height:0})";
        }

        if (page is ContentPage { Content: { } content })
        {
            if (content.Handler is null)
            {
                return "content has no handler";
            }

            if (content.Width <= 0 || content.Height <= 0)
            {
                return $"content has no size ({content.Width:0}x{content.Height:0})";
            }
        }

#if IOS
        if (page.Handler.PlatformView is UIView view)
        {
            if (view.Window is null)
            {
                return "its view isn't in the window";
            }

            if (view.Hidden || view.Alpha < 0.01)
            {
                return "its view is hidden";
            }

            if (view.Bounds.Width <= 0 || view.Bounds.Height <= 0)
            {
                return "its view has no size";
            }
        }
#endif

        return null;
    }

    private static void LayOutAgain(Page page)
    {
        ((IView)page).InvalidateMeasure();
        if (page is ContentPage { Content: IView content })
        {
            content.InvalidateMeasure();
        }

#if IOS
        if (page.Handler?.PlatformView is UIView view)
        {
            view.Hidden = false;
            view.Alpha = 1;
            view.SetNeedsLayout();
            view.Superview?.SetNeedsLayout();
            view.Superview?.LayoutIfNeeded();
        }
#endif
    }

    private static string Name(Page page) => string.IsNullOrWhiteSpace(page.Title) ? page.GetType().Name : $"{page.Title} ({page.GetType().Name})";
}
