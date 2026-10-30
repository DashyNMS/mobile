using System.Runtime.CompilerServices;
using DashyNMS.Mobile.Services;
using IosPage = Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page;
#if IOS
using UIKit;
#endif

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Moves a page's top line up beside the status bar where the screen leaves
/// room for it - iPhone Duo's inner display (#278) - and back below it
/// everywhere else, as the phone opens, closes or turns.
/// </summary>
/// <remarks>
/// <para>One per page, for the lines on it (<see cref="TopBar.Fit"/>). Where
/// a line moves up, the page and every layout between it and the line stop
/// keeping clear of the top safe area, and so do the line's own layouts: in
/// MAUI 10 each layout pads itself out of the safe area, so freeing the page
/// alone would leave the line where it was. The line then takes its top and
/// its distance from the status bar and camera itself. Each is put back
/// exactly as it was set once no line needs it.</para>
/// <para>Scrolled content may pass under the status bar on such a page, as
/// it does under UIKit's own bars.</para>
/// </remarks>
internal sealed class TopLineSpace
{
    private static readonly ConditionalWeakTable<Page, TopLineSpace> Spaces = new();

    private readonly Page _page;
    private readonly List<(WeakReference<Layout> Line, Thickness Padding)> _lines = [];
    private readonly Dictionary<BindableObject, (bool WasSet, object? Value)> _freed = [];
    private readonly List<string> _seen = [];
    private string? _reported;
    private bool _queued;

    private TopLineSpace(Page page)
    {
        _page = page;
        page.SizeChanged += (_, _) => Queue();
        page.Appearing += (_, _) => Queue();
    }

    /// <summary>
    /// Fits <paramref name="line"/> beside the status bar when its page has
    /// room. Its padding now is what it keeps elsewhere.
    /// </summary>
    public static void Track(Layout line)
    {
        var padding = line.Padding;
        line.Loaded += (_, _) =>
        {
            if (PageOf(line) is { } page)
            {
                Spaces.GetValue(page, p => new TopLineSpace(p)).Add(line, padding);
            }
        };
    }

    private static Page? PageOf(Element element)
    {
        for (var parent = element.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is Page page)
            {
                return page;
            }
        }

        return null;
    }

    private void Add(Layout line, Thickness padding)
    {
        if (!_lines.Any(l => l.Line.TryGetTarget(out var known) && ReferenceEquals(known, line)))
        {
            _lines.Add((new WeakReference<Layout>(line), padding));
        }

        Queue();
    }

    // After the layout pass, when the line's position across the page is known.
    private void Queue()
    {
        if (_queued)
        {
            return;
        }

        _queued = true;
        _page.Dispatcher.Dispatch(() =>
        {
            _queued = false;
            Update();
        });
    }

    private void Update()
    {
        _seen.Clear();
        var keep = new HashSet<BindableObject>();
        _lines.RemoveAll(l => !l.Line.TryGetTarget(out _));
        foreach (var (reference, padding) in _lines)
        {
            if (!reference.TryGetTarget(out var line))
            {
                continue;
            }

            if (Place(line) is { } placement)
            {
                line.Padding = new Thickness(
                    padding.Left + placement.InsetStart, placement.Top, padding.Right + placement.InsetEnd, padding.Bottom);
                Collect(line, keep);
            }
            else
            {
                line.Padding = padding;
            }
        }

        foreach (var element in _freed.Keys.Where(e => !keep.Contains(e)).ToList())
        {
            var (wasSet, value) = _freed[element];
            if (wasSet)
            {
                element.SetValue(Layout.SafeAreaEdgesProperty, value);
            }
            else
            {
                element.ClearValue(Layout.SafeAreaEdgesProperty);
            }

            _freed.Remove(element);
            Remeasure(element);
        }

        foreach (var element in keep.Where(e => !_freed.ContainsKey(e)))
        {
            _freed[element] = (element.IsSet(Layout.SafeAreaEdgesProperty), element.GetValue(Layout.SafeAreaEdgesProperty));
            var edges = EdgesOf(element);
            element.SetValue(
                Layout.SafeAreaEdgesProperty,
                new SafeAreaEdges(edges.Left, SafeAreaRegions.None, edges.Right, edges.Bottom));
            Remeasure(element);
        }

        Report();
    }

    // MAUI reads a layout's SafeAreaEdges again only when the layout is next
    // measured, and changing the property doesn't measure it. Without this,
    // a layout no padding change reached went on keeping clear of the status
    // bar - the Dashboard's line moved up only by its padding (#278).
    private static void Remeasure(BindableObject element) => (element as IView)?.InvalidateMeasure();

    // What each line was given, and where it then ended up, in the
    // diagnostics log Settings shares - only when it changes. iPhone Duo's
    // regions can't be seen any other way off the device (#278).
    private void Report()
    {
        if (_seen.Count == 0)
        {
            return;
        }

        var seen = string.Join(" | ", _seen);
        if (seen == _reported)
        {
            return;
        }

        _reported = seen;
        _page.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), () =>
        {
            var landed = string.Join(", ", _lines
                .Select(l => l.Line.TryGetTarget(out var line) ? Landed(line) : null)
                .Where(text => text is not null));
            (IPlatformApplication.Current?.Services.GetService(typeof(DiagnosticsLog)) as DiagnosticsLog)
                ?.Note("Layout", $"Top line on {_page.GetType().Name}: {seen}; laid out at {landed}");
        });
    }

    // The line's top and its first row's top, from the top of the page.
    private string? Landed(Layout line)
    {
#if IOS
        if (_page.Handler?.PlatformView is UIView page && line.Handler?.PlatformView is UIView view && view.Window is not null)
        {
            var frame = view.ConvertRectToView(view.Bounds, page);
            return $"{frame.Top:0.#} + padding {line.Padding.Top:0.#} (own strip {view.SafeAreaInsets.Top:0.#})";
        }
#endif
        return null;
    }

    private TopLinePlacement? Place(Layout line)
    {
#if IOS
        if (!OperatingSystem.IsIOSVersionAtLeast(27, 1)
            || _page.Handler?.PlatformView is not UIView page || page.Window is null
            || line.Handler?.PlatformView is not UIView view || view.Window is null || view.Bounds.Width <= 0)
        {
            return null;
        }

        var frame = view.ConvertRectToView(view.Bounds, page);
        var occupied = ReservedRegions.Occupied(page);
        var placement = BesideStatusBar.Place(page.SafeAreaInsets.Top, frame.Left, frame.Right, TopBar.Height, occupied);
        _seen.Add(
            $"strip {page.SafeAreaInsets.Top:0.#}, page {page.Bounds.Width:0.#}×{page.Bounds.Height:0.#}, "
            + $"line {frame.Left:0.#}–{frame.Right:0.#}, occupied "
            + (occupied.Count == 0 ? "none" : string.Join("; ", occupied.Select(a => $"{a.Left:0.#}–{a.Right:0.#} × {a.Top:0.#}–{a.Bottom:0.#}")))
            + (placement is { } p ? $" → top {p.Top:0.#}, insets {p.InsetStart:0.#}/{p.InsetEnd:0.#}" : " → stays below"));
        return placement;
#else
        return null;
#endif
    }

    // The line, its own layouts, and everything from it up to the page.
    private void Collect(Element line, HashSet<BindableObject> keep)
    {
        for (Element? element = line; element is not null; element = element.Parent)
        {
            if (KeepsClear(element))
            {
                keep.Add(element);
            }

            if (ReferenceEquals(element, _page))
            {
                break;
            }
        }

        CollectWithin(line, keep);
    }

    private static void CollectWithin(IVisualTreeElement element, HashSet<BindableObject> keep)
    {
        foreach (var child in element.GetVisualChildren())
        {
            if (child is BindableObject bindable && KeepsClear(bindable))
            {
                keep.Add(bindable);
            }

            CollectWithin(child, keep);
        }
    }

    private static bool KeepsClear(BindableObject element) =>
        element is ContentPage or Layout or ScrollView or ContentView or Border;

    // What each edge does now. A page that leaves SafeAreaEdges unset keeps
    // clear by the older UseSafeArea instead, so that's read in its place.
    private static SafeAreaEdges EdgesOf(BindableObject element) =>
        element is ContentPage page && !page.IsSet(ContentPage.SafeAreaEdgesProperty)
            ? new SafeAreaEdges(IosPage.GetUseSafeArea(page) ? SafeAreaRegions.Container : SafeAreaRegions.None)
            : (SafeAreaEdges)element.GetValue(Layout.SafeAreaEdgesProperty);
}
