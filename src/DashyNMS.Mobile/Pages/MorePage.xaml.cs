using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>The fixed page catalogue and immediate tab preferences.</summary>
public partial class MorePage : ContentPage, IActivePage
{
    private readonly MoreViewModel _viewModel;
    private IDisposable? _edges;

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
        _edges?.Dispose();
        _edges = null;
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
    }

    private void PlaceControls()
    {
        var clear = CoveredEdges.ForScrolling(this);
        Body.Padding = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
        Foot.HeightRequest = 16 + clear.Bottom;
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
