using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Controls;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.DiagnosticsSharing"/>: once after sign-in, until the user chooses (#216).</summary>
public partial class DiagnosticsSharingPage : ContentPage, IActivePage
{
    private IDisposable? _edges;
    private bool _showing;

	public DiagnosticsSharingPage(DiagnosticsSharingViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container,
            SafeAreaRegions.None, SafeAreaRegions.None);
        Scroller.SafeAreaEdges = SafeAreaEdges.None;
        Root.SafeAreaEdges = SafeAreaEdges.None;
#endif
	}

	/// <summary>The app's own header, as every page (#142).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
        Activate();
	}

    protected override void OnDisappearing()
    {
        _showing = false;
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

    /// <summary>Reclaims caller Back after returning to a retained Main stack.</summary>
    public void Activate()
    {
        if (!_showing)
        {
            _showing = true;
            _edges = CoveredEdges.Watch(this, Place);
            Scroller.HandlerChanged += OnChanged;
            Scroller.Loaded += OnChanged;
#if IOS
            TabBarButtons.Moved += OnChanged;
#endif
        }
#if IOS
        Header.BackInGlass = TabBarButtons.IsAvailable;
        if (TabBarButtons.IsAvailable)
            TabBarButtons.Show(this, null, string.Empty,
            [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => { if (BindingContext is DiagnosticsSharingViewModel { CanChoose: true }) _ = Shell.Current.GoToAsync(".."); }, IsBack: true)]);
#endif
        Header.SetBinding(IsEnabledProperty, nameof(DiagnosticsSharingViewModel.CanChoose));
        Header.Apply(this);
        Place();
    }

    protected override bool OnBackButtonPressed() =>
        BindingContext is DiagnosticsSharingViewModel { CanChoose: false } || base.OnBackButtonPressed();

    private void OnChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(Place);

    private void Place()
    {
        if (!_showing) return;
        var clear = CoveredEdges.ForScrolling(this);
        Header.Margin = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
        Body.Padding = new Thickness(16 + clear.Left, 16, 16 + clear.Right, 24 + clear.Bottom);
#if IOS
        if (Scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
#endif
    }
}
