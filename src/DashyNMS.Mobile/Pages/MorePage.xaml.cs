using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>The fixed page catalogue and immediate tab preferences.</summary>
public partial class MorePage : ContentPage, IActivePage
{
    private readonly MoreViewModel _viewModel;
    private IDisposable? _edges;
    private bool _visible;

    public MorePage(MoreViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        Scroller.HandlerChanged += (_, _) => StopInsets();
        Scroller.Loaded += (_, _) => StopInsets();
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
    }

    /// <summary>Reclaim an action-free glass owner after retained navigation or resume.</summary>
    public void Activate()
    {
        _visible = true;
        Header.Apply(this);
        _viewModel.Update();
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
#if IOS
        TabBarButtons.Show(this, null, string.Empty, []);
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
        StopInsets();
#endif
        PlaceControls();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _visible = false;
        _edges?.Dispose();
        _edges = null;
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
    }

    private void PlaceControls()
    {
        if (!_visible || !ActivePage.IsCurrent(this)) return;
        var clear = CoveredEdges.ForScrolling(this);
        var obstruction = 12 + clear.Bottom;
#if IOS
        StopInsets();
        if (Handler?.PlatformView is UIKit.UIView page && page.Bounds.Height > 0)
        {
            var nativePill = TabBarButtons.PillIn(page);
            OccupiedArea? pill = nativePill is { } bar ? new(bar.Left, bar.Top, bar.Right, bar.Bottom) : null;
            var glass = TabBarButtons.Occupied(page)
                .Select(r => new OccupiedArea(r.Left, r.Top, r.Right, r.Bottom)).ToArray();
            obstruction = SetupActionPlacement.BottomGap(page.Bounds.Height,
                CoveredEdges.Of(this).Bottom + (pill is null ? TabBarButtons.CoveredBottom : 0), pill, glass);
        }
#endif
        Body.Padding = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
        var foot = SectionListPlacement.FooterGap + obstruction;
        if (Foot.HeightRequest != foot) Foot.HeightRequest = foot;
    }

#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

    private void StopInsets()
    {
        if (Scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }
#endif
}
