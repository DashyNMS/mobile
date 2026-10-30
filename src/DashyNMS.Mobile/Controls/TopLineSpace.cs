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
        }

        foreach (var element in keep.Where(e => !_freed.ContainsKey(e)))
        {
            _freed[element] = (element.IsSet(Layout.SafeAreaEdgesProperty), element.GetValue(Layout.SafeAreaEdgesProperty));
            var edges = EdgesOf(element);
            element.SetValue(
                Layout.SafeAreaEdgesProperty,
                new SafeAreaEdges(edges.Left, SafeAreaRegions.None, edges.Right, edges.Bottom));
        }
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
        return BesideStatusBar.Place(
            page.SafeAreaInsets.Top, frame.Left, frame.Right, TopBar.Height, ReservedRegions.Occupied(page));
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
