using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Read-only embedded notices, pushed relative to the actual Settings caller.</summary>
public partial class LicencesPage : ContentPage, IActivePage
{
    private IDisposable? _edges;
    private Window? _resumeWindow;
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
        // The header scrolls with the notices. Only hidden header Back needs a fade.
        Fade.IsVisible = Header.BackInGlass;
        Body.Padding = new Thickness(16 + edges.Left, Header.BackInGlass ? 20 : 0,
            16 + edges.Right, 24 + edges.Bottom);
        Fade.Margin = new Thickness(16 + edges.Left, 0, 16 + edges.Right, 0);
        TopBar.Fit(Header);
#if IOS
        if (Scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
#endif
    }
}
