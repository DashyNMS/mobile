using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Read-only embedded notices, pushed relative to the actual Settings caller.</summary>
public partial class LicencesPage : ContentPage, IActivePage
{
    private IDisposable? _edges;
    private bool _visible;

    public LicencesPage(LicencesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container,
            SafeAreaRegions.None, SafeAreaRegions.None);
        Root.SafeAreaEdges = SafeAreaEdges.None;
        Scroller.SafeAreaEdges = SafeAreaEdges.None;
#else
        SafeAreaEdges = SafeAreaEdges.All;
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
    }

    /// <summary>Reclaims glass when More restores this retained stack, including resume.</summary>
    public void Activate()
    {
        _visible = true;
        _edges ??= CoveredEdges.Watch(this, Place);
        Scroller.HandlerChanged -= OnChanged;
        Scroller.HandlerChanged += OnChanged;
        Scroller.Loaded -= OnChanged;
        Scroller.Loaded += OnChanged;
#if IOS
        TabBarButtons.Moved -= OnChanged;
        TabBarButtons.Moved += OnChanged;
        Header.BackInGlass = TabBarButtons.IsAvailable;
        if (TabBarButtons.IsAvailable)
            TabBarButtons.Show(this, null, string.Empty,
            [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => { if (_visible) _ = Shell.Current.GoToAsync(".."); }, IsBack: true)]);
#endif
        Header.Apply(this);
        Place();
    }

    protected override void OnDisappearing()
    {
        _visible = false;
        _edges?.Dispose();
        _edges = null;
        Scroller.HandlerChanged -= OnChanged;
        Scroller.Loaded -= OnChanged;
#if IOS
        TabBarButtons.Moved -= OnChanged;
        TabBarButtons.Hide(this);
#endif
        base.OnDisappearing();
    }

    private void OnChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(Place);

    private void Place()
    {
        if (!_visible) return;
        var edges = CoveredEdges.ForScrolling(this);
        // The header scrolls with the notices. Only hidden header Back needs a fade.
        Fade.IsVisible = Header.BackInGlass;
        var bottom = 24 + edges.Bottom;
#if IOS
        var sideBar = false;
        if (Handler?.PlatformView is UIKit.UIView page && page.Bounds.Height > 0)
        {
            var nativePill = TabBarButtons.PillIn(page);
            sideBar = nativePill is { } p && p.Height > p.Width;
            OccupiedArea? pill = nativePill is { } bar ? new(bar.Left, bar.Top, bar.Right, bar.Bottom) : null;
            var glass = TabBarButtons.Occupied(page)
                .Select(r => new OccupiedArea(r.Left, r.Top, r.Right, r.Bottom)).ToArray();
            var obstruction = SetupActionPlacement.BottomGap(page.Bounds.Height,
                CoveredEdges.Of(this).Bottom + (pill is null ? TabBarButtons.CoveredBottom : 0), pill, glass);
            bottom = SectionListPlacement.Resolve(obstruction).Footer;
        }
        BottomFade.SetCoveredHeight(!sideBar ? edges.Bottom : 0);
        if (Scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
#endif
        Body.Padding = new Thickness(16 + edges.Left, Header.BackInGlass ? 20 : 0,
            16 + edges.Right, bottom);
        Fade.Margin = new Thickness(16 + edges.Left, 0, 16 + edges.Right, 0);
    }
}
