using System.Runtime.Versioning;

namespace DashyNMS.Mobile;

/// <summary>
/// Search as UIKit's own search tab, beside the tab bar's other tabs, as in
/// Music and other iOS 26 apps - for the page showing, so search is in the
/// same place on every page that has it rather than in each page's header.
/// </summary>
/// <remarks>
/// <para>UIKit places the search tab itself, on every display and the right
/// way round: beside a bottom tab bar, in line with a vertical one (iPhone
/// Duo). A button laid over the tab bar by hand (#304, #306) never sat right
/// on every display, and couldn't sit in the bar's row at all: only UIKit
/// can shorten the bar to make room. Shell's tab bar controller is taught to
/// use tabs in <see cref="AppShellRenderer"/>.</para>
/// <para>A tap on it doesn't switch tabs: it opens search on the page
/// showing. The tab only shows while a page with search does - a page takes
/// it over as it appears and gives it up as it goes.</para>
/// </remarks>
internal static class TabBarSearch
{
    private static readonly List<WeakReference<IHost>> Hosts = [];
    private static object? _owner;
    private static Action? _tapped;

    /// <summary>The tab bar that can show a search tab: Shell's, made to use UIKit's tabs.</summary>
    internal interface IHost
    {
        void ShowSearch(bool shown);
    }

    /// <summary>Liquid Glass and its search tab need iOS 26; before that, pages keep search in their header.</summary>
    [SupportedOSPlatformGuard("ios26.0")]
    public static bool IsAvailable => OperatingSystem.IsIOSVersionAtLeast(26);

    /// <summary>Whether a page wants the search tab now.</summary>
    internal static bool IsShown => _owner is not null;

    /// <summary>Shows the search tab for <paramref name="owner"/>, which <paramref name="tapped"/> answers.</summary>
    public static void Show(object owner, Action tapped)
    {
        if (!IsAvailable)
        {
            return;
        }

        // Only when it comes: changing the tabs can make UIKit show the page
        // again, which would ask again.
        var wasShown = IsShown;
        _owner = owner;
        _tapped = tapped;
        if (!wasShown)
        {
            Tell(true);
        }
    }

    /// <summary>Hides the search tab, unless another page has taken it over since.</summary>
    public static void Hide(object owner)
    {
        if (!ReferenceEquals(owner, _owner))
        {
            return;
        }

        _owner = null;
        _tapped = null;
        Tell(false);
    }

    /// <summary>For a tab bar controller to tell of itself, and be told when search comes and goes.</summary>
    internal static void Register(IHost host)
    {
        Hosts.RemoveAll(h => !h.TryGetTarget(out _));
        Hosts.Add(new WeakReference<IHost>(host));
    }

    /// <summary>The search tab was tapped.</summary>
    internal static void Tap() => _tapped?.Invoke();

    private static void Tell(bool shown)
    {
        Hosts.RemoveAll(h => !h.TryGetTarget(out _));
        foreach (var reference in Hosts)
        {
            if (reference.TryGetTarget(out var host))
            {
                host.ShowSearch(shown);
            }
        }
    }
}
