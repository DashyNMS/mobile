using DashyNMS.Mobile.Services;
#if IOS
using Foundation;
using UIKit;
#endif

namespace DashyNMS.Mobile.Controls;

/// <summary>Owns the finite backup viewport and optional fixed action, using measured native glass bounds.</summary>
internal sealed class PushedListLayout
{
    private readonly ContentPage _page;
    private readonly PageHeader _header;
    private readonly Layout _body;
    private readonly CollectionView _rows;
    private readonly View _foot;
    private readonly View? _bottomAction;
    private readonly FootFade? _footFade;
    private IDisposable? _edges;
    private bool _shown;

    /// <summary>
    /// Opt-in iOS full-height mode: viewport runs to the screen base (stopping 12pt
    /// above a docked keyboard), overlaying <c>bottomAction</c> above a <see cref="FootFade"/>.
    /// Default false so existing pages remain unchanged until opted in.
    /// </summary>
    public bool FullHeight { get; set; }
#if IOS
    private IReadOnlyList<GlassAction> _actions = [];
    private Action? _search;
    private string _searchLabel = string.Empty;
    private LiveKeyboardBounds? _keyboard;
#endif

    public PushedListLayout(ContentPage page, PageHeader header, Layout body, CollectionView rows, View foot, View? bottomAction = null, FootFade? footFade = null)
    {
        _page = page;
        _header = header;
        _body = body;
        _rows = rows;
        _foot = foot;
        _bottomAction = bottomAction;
        _footFade = footFade;
        body.Padding = new Thickness(16, 0, 16, 12);
        if (bottomAction is not null) bottomAction.Margin = new Thickness(0, 8, 0, 0);
#if IOS
        page.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        header.BackInGlass = TabBarButtons.IsAvailable;
#else
        page.SafeAreaEdges = SafeAreaEdges.All;
#endif
    }

#if IOS
    /// <summary>Callers cache stateful actions; unchanged state must not rebuild native controls.</summary>
    public void SetActions(IReadOnlyList<GlassAction> actions, Action? search = null, string searchLabel = "")
    {
        if (ReferenceEquals(_actions, actions) && _search == search && _searchLabel == searchLabel) return;
        _actions = actions;
        _search = search;
        _searchLabel = searchLabel;
        if (_shown) ShowGlass();
    }

    private void ShowGlass()
    {
        if (!_shown || !TabBarButtons.IsAvailable) return;
        var actions = new List<GlassAction>
        {
            new("chevron.backward", "Back to " + BackBar.PreviousTitle(_page),
                () => { if (_shown) _ = Shell.Current.GoToAsync(".."); }, IsBack: true),
        };
        actions.AddRange(_actions.Select(action => action with
        {
            Tapped = () => { if (_shown) action.Tapped(); },
        }));
        TabBarButtons.Show(_page, _search is null ? null : () => { if (_shown) _search?.Invoke(); }, _searchLabel, actions);
    }
#endif

    public void Show() => Activate();

    /// <summary>Central active-page dispatch reclaims retained stacks without reloading their data.</summary>
    public void Activate()
    {
        if (!_shown)
        {
            _shown = true;
            _edges = CoveredEdges.Watch(_page, Place);
            _body.SizeChanged += OnChanged;
            _rows.HandlerChanged += OnChanged;
            _rows.Loaded += OnChanged;
            if (_bottomAction is not null)
            {
                _bottomAction.SizeChanged += OnChanged;
                _bottomAction.PropertyChanged += OnActionChanged;
            }
#if IOS
            TabBarButtons.Moved += OnChanged;
            _keyboard = new LiveKeyboardBounds(Place);
#endif
        }
        _header.Apply(_page);
#if IOS
        // A departing sibling may reconnect after our first Appearing. Reclaim on activation.
        DashyNMS.Mobile.Controls.ManualKeyboardScroll.Acquire(this);
        ShowGlass();
#endif
        Place();
    }

    public void Hide()
    {
        if (!_shown) return;
        _shown = false;
        _edges?.Dispose();
        _edges = null;
        _body.SizeChanged -= OnChanged;
        _rows.HandlerChanged -= OnChanged;
        _rows.Loaded -= OnChanged;
        if (_bottomAction is not null)
        {
            _bottomAction.SizeChanged -= OnChanged;
            _bottomAction.PropertyChanged -= OnActionChanged;
        }
#if IOS
        TabBarButtons.Moved -= OnChanged;
        TabBarButtons.Hide(_page);
        DashyNMS.Mobile.Controls.ManualKeyboardScroll.Release(this);
        _keyboard?.Dispose();
        _keyboard = null;
#endif
    }

    private void OnActionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(View.IsVisible)) Place();
    }

    private void OnChanged(object? sender, EventArgs e) => _page.Dispatcher.Dispatch(Place);

    private void Place()
    {
        if (!_shown || _body.Height <= 0) return;
        var clear = CoveredEdges.ForScrolling(_page);
        var bottom = 12 + clear.Bottom;
        var footer = 20d;
        var hasAction = _bottomAction?.IsVisible == true;
#if IOS
        if (_rows.Handler?.PlatformView is UIView rows && FindScroll(rows) is { } scroll)
            scroll.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
        // Body fills the page's top-safe-area host. Query pill and actions in that
        // exact host; no page-to-card offset or guessed per-page conversion is needed.
        if (_body.Handler?.PlatformView is UIView view && view.Bounds.Height > 0)
        {
            var height = view.Bounds.Height;
            var nativePill = TabBarButtons.PillIn(view);
            OccupiedArea? pill = nativePill is { } bar ? new(bar.Left, bar.Top, bar.Right, bar.Bottom) : null;
            var glass = TabBarButtons.Occupied(view)
                .Select(rect => new OccupiedArea(rect.Left, rect.Top, rect.Right, rect.Bottom)).ToArray();
            var keyboardTop = _keyboard?.TopIn(view);
            var actionGap = SetupActionPlacement.BottomGap(height,
                CoveredEdges.Of(_page).Bottom + (pill is null ? TabBarButtons.CoveredBottom : 0), pill, glass);
            if (FullHeight)
            {
                var keyboardMargin = SectionListPlacement.KeyboardViewportMargin(height, keyboardTop);
                var actionHeight = hasAction && _bottomAction!.Height > 0 ? _bottomAction.Height : hasAction ? 48d : 0d;
                var placement = SectionListPlacement.Resolve(actionGap, keyboardMargin > 0 ? keyboardMargin : null,
                    actionHeight: actionHeight > 0 ? actionHeight : null);
                var fullPadding = new Thickness(16 + clear.Left, 0, 16 + clear.Right, placement.Viewport);
                if (_body.Padding != fullPadding) _body.Padding = fullPadding;
                if (_foot.HeightRequest != placement.Footer) _foot.HeightRequest = placement.Footer;
                var overlayMarginBottom = Math.Max(0, placement.Overlay - placement.Viewport);
                if (_bottomAction is not null)
                {
                    var actionMargin = new Thickness(0, 0, 0, overlayMarginBottom);
                    if (_bottomAction.Margin != actionMargin) _bottomAction.Margin = actionMargin;
                }
                var sidePill = nativePill is { } np && np.Height > np.Width;
                var bottomChrome = !sidePill && keyboardMargin <= 0
                    ? CoveredEdges.Of(_page).Bottom + TabBarButtons.CoveredBottom
                    : 0;
                _footFade?.SetCoveredHeight(hasAction ? actionHeight + overlayMarginBottom : bottomChrome);
                return;
            }
            bottom = actionGap;
            if (!hasAction && nativePill is { } measured && (measured.Height > measured.Width || measured.Top > height / 2))
            {
                // Match Devices/Alerts: the finite card reaches the pill base.
                // Bottom glass is cleared inside the list, not by shrinking its card.
                bottom = Math.Max(0, height - measured.Bottom);
                footer += Math.Max(0, actionGap - bottom);
            }
            if (keyboardTop is { } top) bottom = Math.Max(bottom, height - Math.Max(0, top) + 12);
        }
#endif
        var padding = new Thickness(16 + clear.Left, 0, 16 + clear.Right, bottom);
        if (_body.Padding != padding) _body.Padding = padding;
        if (_foot.HeightRequest != footer) _foot.HeightRequest = footer;
        // The Auto row measures the real wrapped action. Body owns the only base gap.
        if (_bottomAction is not null && _bottomAction.Margin != new Thickness(0, 8, 0, 0))
            _bottomAction.Margin = new Thickness(0, 8, 0, 0);
    }

#if IOS
    private static UIScrollView? FindScroll(UIView view) =>
        view as UIScrollView ?? view.Subviews.Select(FindScroll).FirstOrDefault(found => found is not null);
#endif
}
