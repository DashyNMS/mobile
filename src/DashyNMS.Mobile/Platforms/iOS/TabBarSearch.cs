using DashyNMS.Mobile.Services;

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
/// <para>The first try at it (#308) left a black screen at launch. So the
/// tab bar only changes over once it has appeared, anything that goes wrong
/// puts it back as Shell made it, with search in the page's header again,
/// and a launch that never got past the change turns it off for that build
/// (<see cref="CanTry"/>): one bad launch at worst, never every launch.</para>
/// <para>A tap on the search tab doesn't switch tabs: it opens search on the
/// page showing. The tab only shows while a page with search does - a page
/// takes it over as it appears and gives it up as it goes.</para>
/// </remarks>
internal static class TabBarSearch
{
    /// <summary>The build that is changing the tab bar over, until it has run a while since.</summary>
    private const string PendingKey = "ios.searchtab.pending";

    /// <summary>The build the search tab is turned off for, having failed on it.</summary>
    private const string DisabledKey = "ios.searchtab.disabled";

    /// <summary>How long the app runs after the change before it counts as having worked.</summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(10);

    private static readonly List<WeakReference<IHost>> Hosts = [];
    private static object? _owner;
    private static Action? _tapped;
    private static bool? _canTry;

    /// <summary>The tab bar that can show a search tab: Shell's, made to use UIKit's tabs.</summary>
    internal interface IHost
    {
        void ShowSearch(bool shown);
    }

    /// <summary>Raised when the search tab starts or stops being there, so pages show search in their header or not.</summary>
    public static event EventHandler? ActiveChanged;

    /// <summary>Whether the tab bar has a search tab to offer: pages put search in their header otherwise.</summary>
    public static bool IsActive { get; private set; }

    /// <summary>Whether a page wants the search tab now.</summary>
    internal static bool IsShown => _owner is not null;

    /// <summary>
    /// Whether to try the search tab this launch: iOS 26 and later, and not a
    /// build whose last launch never got past changing the tab bar over.
    /// </summary>
    internal static bool CanTry
    {
        get
        {
            if (_canTry is { } known)
            {
                return known;
            }

            if (!OperatingSystem.IsIOSVersionAtLeast(26))
            {
                return (_canTry = false).Value;
            }

            var build = AppInfo.Current.BuildString;
            if (Preferences.Default.Get<string?>(PendingKey, null) == build)
            {
                Preferences.Default.Set(DisabledKey, build);
                Note($"the last launch of build {build} ended while the search tab was being added or the screen was changing; off for this build");
            }

            Preferences.Default.Remove(PendingKey);
            return (_canTry = Preferences.Default.Get<string?>(DisabledKey, null) != build).Value;
        }
    }

    /// <summary>Shows the search tab for <paramref name="owner"/>, which <paramref name="tapped"/> answers.</summary>
    public static void Show(object owner, Action tapped)
    {
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

    /// <summary>The tab bar is about to change over: if this launch goes no further, the next one won't try.</summary>
    internal static void Starting() => Preferences.Default.Set(PendingKey, AppInfo.Current.BuildString);

    /// <summary>The tab bar has changed over: once the app has run a while on it, it counts as having worked.</summary>
    internal static void Started()
    {
        SetActive(true);
        Note("added the search tab");
        Application.Current?.Dispatcher.DispatchDelayed(SettleTime, () =>
        {
            if (IsActive)
            {
                Preferences.Default.Remove(PendingKey);
            }
        });
    }

    /// <summary>
    /// Something the tabs might not survive is starting - iPhone Duo opening,
    /// folding or turning: if the app dies before it settles, the next
    /// launch leaves the tab bar as Shell makes it.
    /// </summary>
    internal static void Risky()
    {
        Starting();
        Application.Current?.Dispatcher.DispatchDelayed(SettleTime, () =>
        {
            if (IsActive)
            {
                Preferences.Default.Remove(PendingKey);
            }
        });
    }

    /// <summary>Something went wrong with the tabs: the tab bar is back as Shell made it, for the rest of this build.</summary>
    internal static void Failed(string where, Exception error)
    {
        _canTry = false;
        Preferences.Default.Set(DisabledKey, AppInfo.Current.BuildString);
        Preferences.Default.Remove(PendingKey);
        SetActive(false);
        Note($"search tab off after {error.GetType().Name} in {where}: {error.Message}");
    }

    private static void SetActive(bool active)
    {
        if (IsActive != active)
        {
            IsActive = active;
            ActiveChanged?.Invoke(null, EventArgs.Empty);
        }
    }

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

    // In the diagnostics log Settings shares: the tab bar's change can't be
    // seen any other way off the device.
    private static void Note(string text) =>
        (IPlatformApplication.Current?.Services.GetService(typeof(DiagnosticsLog)) as DiagnosticsLog)?.NoteImportant("Layout", "Search tab: " + text);
}
