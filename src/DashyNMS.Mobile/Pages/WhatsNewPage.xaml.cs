using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Release notes pushed after an update or from Settings; only Got it marks them seen.</summary>
public partial class WhatsNewPage : ContentPage, IActivePage
{
    private IDisposable? _edges;
    private bool _visible;

    public WhatsNewPage(WhatsNewViewModel viewModel)
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
        Header.SetBinding(IsEnabledProperty, nameof(WhatsNewViewModel.CanDismiss));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
    }

    /// <summary>Shell also calls this when selecting a retained More stack.</summary>
    public void Activate()
    {
        _visible = true;
        _edges ??= CoveredEdges.Watch(this, Place);
        Scroller.HandlerChanged -= OnChanged;
        Scroller.HandlerChanged += OnChanged;
        Scroller.Loaded -= OnChanged;
        Scroller.Loaded += OnChanged;
        Root.SizeChanged -= OnChanged;
        Root.SizeChanged += OnChanged;
        GotIt.SizeChanged -= OnChanged;
        GotIt.SizeChanged += OnChanged;
#if IOS
        TabBarButtons.Moved -= OnChanged;
        TabBarButtons.Moved += OnChanged;
        Header.BackInGlass = TabBarButtons.IsAvailable;
        if (TabBarButtons.IsAvailable)
            TabBarButtons.Show(this, null, string.Empty,
            [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => { if (_visible && BindingContext is WhatsNewViewModel { CanDismiss: true }) _ = Shell.Current.GoToAsync(".."); }, IsBack: true)]);
#endif
        Header.Apply(this);
        Place();
    }

    protected override bool OnBackButtonPressed() =>
        BindingContext is WhatsNewViewModel { CanDismiss: false } || base.OnBackButtonPressed();

    protected override void OnDisappearing()
    {
        _visible = false;
        _edges?.Dispose();
        _edges = null;
        Scroller.HandlerChanged -= OnChanged;
        Scroller.Loaded -= OnChanged;
        Root.SizeChanged -= OnChanged;
        GotIt.SizeChanged -= OnChanged;
#if IOS
        TabBarButtons.Moved -= OnChanged;
        TabBarButtons.Hide(this);
#endif
        base.OnDisappearing();
    }

    /// <summary>Ignores a queued tap after Back has begun removing the page.</summary>
    private async void OnGotItClicked(object? sender, EventArgs e)
    {
        if (!_visible || BindingContext is not WhatsNewViewModel { CanDismiss: true } viewModel) return;
        await viewModel.GotItCommand.ExecuteAsync(null);
    }

    private void OnChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(Place);

    private void Place()
    {
        if (!_visible) return;
        var edges = CoveredEdges.ForScrolling(this);
        var bottom = SetupActionPlacement.BottomGap(Root.Height, edges.Bottom, null, []);
#if IOS
        if (Root.Handler?.PlatformView is UIKit.UIView view)
        {
            OccupiedArea? pill = TabBarButtons.PillIn(view) is { } bar
                ? new(bar.Left, bar.Top, bar.Right, bar.Bottom) : null;
            var glass = TabBarButtons.Occupied(view)
                .Select(rect => new OccupiedArea(rect.Left, rect.Top, rect.Right, rect.Bottom)).ToArray();
            bottom = SetupActionPlacement.BottomGap(view.Bounds.Height,
                CoveredEdges.Of(this).Bottom + (pill is null ? TabBarButtons.CoveredBottom : 0), pill, glass);
        }
#endif
        Header.Margin = new Thickness(16 + edges.Left, 0, 16 + edges.Right, 0);
        Body.Padding = new Thickness(16 + edges.Left, 20, 16 + edges.Right, 24);
        // The Auto row reserves the wrapped action; the measured gap is its only bottom inset.
        var actionMargin = new Thickness(16 + edges.Left, 12, 16 + edges.Right, bottom);
        if (GotIt.Margin != actionMargin) GotIt.Margin = actionMargin;
        Fade.Margin = new Thickness(16 + edges.Left, 0, 16 + edges.Right, 0);
#if IOS
        if (Scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
#endif
    }
}
