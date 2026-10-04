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
/// <para>Diagnostics from a real case (2 October) showed the Alerts tab's page
/// had lost its handler - its view - on an ordinary tab switch, a minute after
/// a detail page pushed on that tab was closed: inside Shell on iOS 26+, not
/// a layout glitch. A page with no view can't be laid out again, so its tab
/// is rebuilt with a fresh page (<see cref="AppShell.RebuildCurrentTab"/>);
/// in that case it came back straight away.</para>
/// <para>So the page showing is checked half a second after any page
/// appears, and after the app comes back: no handler, no size, content
/// without either, or (iOS) a view out of the window or hidden. One that
/// still has a view is laid out again first. Each step is logged, and so is
/// the moment any page loses its handler, so diagnostics show what led to it.</para>
/// </remarks>
internal static class PageHealth
{
    /// <summary>Left for the platform to settle after coming back, before checking.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(500);

    private static DateTime _lastCheck;

    /// <summary>Pages whose handler is watched, so losing it is logged once each.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Page, object> Watched = new();

    public static void Watch(Application app, Window window, DiagnosticsLog log)
    {
        app.PageAppearing += (_, page) =>
        {
            // One trail of pages rather than a line each (#126): "Pages: Alerts → Devices".
            log.NotePage(Name(page));
            WatchHandler(page, log);

            // A tab went blank on an ordinary tab switch, not only after
            // unlocking (diagnostics, 2 Oct): check every page as it appears.
            window.Dispatcher.DispatchDelayed(Settle, () => _ = CheckAsync(window, log, "after appearing", logFine: false));
        };
        window.Stopped += (_, _) => log.Note("App", "Went to the background");
        window.Resumed += (_, _) =>
        {
            log.Note("App", "Came back");
            Check(window, log);
        };

        // Unlocking can bring only Activated, so both check - once.
        window.Activated += (_, _) => Check(window, log);
    }

    /// <summary>
    /// The blank page had lost its handler - its view - while still in a tab.
    /// Logged the moment it happens, so diagnostics show what came just before.
    /// </summary>
    private static void WatchHandler(Page page, DiagnosticsLog log)
    {
        if (Watched.TryGetValue(page, out _))
        {
            return;
        }

        Watched.Add(page, new object());
        page.HandlerChanged += (_, _) => log.NoteImportant("Page", page.Handler is null
            ? $"{Name(page)} lost its view (handler disconnected)"
            : $"{Name(page)} has a view again");
    }

    private static void Check(Window window, DiagnosticsLog log)
    {
        if (DateTime.UtcNow - _lastCheck < TimeSpan.FromSeconds(2))
        {
            return;
        }

        _lastCheck = DateTime.UtcNow;
        window.Dispatcher.DispatchDelayed(Settle, () => _ = CheckAsync(window, log, "after coming back", logFine: true));
    }

    private static async Task CheckAsync(Window window, DiagnosticsLog log, string when, bool logFine)
    {
        try
        {
            if (Shell.Current?.CurrentPage is not { } page)
            {
                return;
            }

            if (Problem(page) is not { } problem)
            {
                if (logFine)
                {
                    log.Note("Page", $"{Name(page)} fine {when} ({page.Width:0}x{page.Height:0})");
                }

                return;
            }

            // With no view at all there's nothing to lay out: straight to a fresh page.
            if (page.Handler is not null)
            {
                log.NoteImportant("Page", $"{Name(page)} looks blank {when}: {problem}. Laying it out again");
                LayOutAgain(page);
                await Task.Delay(Settle);

                if (Problem(page) is not { } still)
                {
                    log.Note("Page", $"{Name(page)} fine after laying out again");
                    return;
                }

                problem = still;
            }

            log.NoteImportant("Page", $"{Name(page)} blank {when}: {problem}. Rebuilding its tab");
            var rebuilt = window.Page is AppShell shell && shell.RebuildCurrentTab();
            log.Note("Page", rebuilt ? "Tab rebuilt" : "Not a tab that can be rebuilt");
        }
        catch (Exception ex)
        {
            // Diagnosis must never take the app down with it.
            log.NoteImportant("Page", $"Check {when} failed: {ex.GetType().Name}: {ex.Message}");
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
