using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Release notes pushed after an update or from Settings; only Got it marks them seen.</summary>
public partial class WhatsNewPage : ContentPage, IActivePage
{
    private IDisposable? _edges;
    private Window? _resumeWindow;
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
        if (!ReferenceEquals(_resumeWindow, Window))
        {
            if (_resumeWindow is not null) _resumeWindow.Resumed -= OnResumed;
            _resumeWindow = Window;
            if (_resumeWindow is not null) _resumeWindow.Resumed += OnResumed;
        }
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
        if (_resumeWindow is not null) _resumeWindow.Resumed -= OnResumed;
        _resumeWindow = null;
        Scroller.HandlerChanged -= OnChanged;
        Scroller.Loaded -= OnChanged;
#if IOS
        TabBarButtons.Moved -= OnChanged;
        TabBarButtons.Hide(this);
#endif
        base.OnDisappearing();
    }

    private void OnResumed(object? sender, EventArgs e) => Dispatcher.Dispatch(() =>
    {
        if (_visible) Activate();
    });
    private void OnChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(Place);

    private void Place()
    {
        if (!_visible) return;
        var edges = CoveredEdges.ForScrolling(this);
        Header.Margin = new Thickness(16 + edges.Left, 0, 16 + edges.Right, 0);
        TopBar.Fit(Header);
        Body.Padding = new Thickness(16 + edges.Left, 20, 16 + edges.Right, 24 + edges.Bottom);
        Fade.Margin = new Thickness(16 + edges.Left, 0, 16 + edges.Right, 0);
#if IOS
        if (Scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
#endif
    }
}
