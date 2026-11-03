using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Views;
using DashyNMS.Mobile.Controls;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.AlertDetail"/>, with the alert id (and device
/// id, from a notification). The alert itself is <see cref="AlertDetailView"/>,
/// which the Alerts list also shows beside itself on a larger screen (#88).
/// </summary>
public sealed class AlertDetailPage : ContentPage, IQueryAttributable, IActivePage
{
	private readonly AlertDetailView _view;
    private IDisposable? _edges;
    private Window? _resumeWindow;

	public AlertDetailPage(AlertDetailViewModel viewModel)
	{
		BindingContext = viewModel;
		SetBinding(TitleProperty, new Binding(nameof(AlertDetailViewModel.Title)));
#if IOS
        On<iOS>().SetUseSafeArea(false);
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
#endif
		Content = _view = new AlertDetailView(viewModel);
	}

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
        if (!ReferenceEquals(_resumeWindow, Window))
        {
            if (_resumeWindow is not null) _resumeWindow.Resumed -= OnResumed;
            _resumeWindow = Window;
            if (_resumeWindow is not null) _resumeWindow.Resumed += OnResumed;
        }
#if IOS
        TabBarButtons.Moved -= OnMoved;
        TabBarButtons.Moved += OnMoved;
#endif
        Activate();
    }

    /// <summary>Reclaims pushed Back when Shell reactivates a retained stack.</summary>
    public void Activate()
    {
        _view.Bar.Apply(this);
#if IOS
        _view.Bar.IsVisible = !TabBarButtons.IsAvailable;
        ShowBack();
#endif
        PlaceControls();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _edges?.Dispose();
        _edges = null;
        if (_resumeWindow is not null) _resumeWindow.Resumed -= OnResumed;
        _resumeWindow = null;
#if IOS
        TabBarButtons.Moved -= OnMoved;
        TabBarButtons.Hide(this);
#endif
    }

    private void PlaceControls() => _view.PlaceContent(CoveredEdges.ForScrolling(this));
    private void OnResumed(object? sender, EventArgs e) => Dispatcher.Dispatch(Activate);
#if IOS
    private void ShowBack() => TabBarButtons.Show(this, null, string.Empty,
        [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
            () => _ = Shell.Current.GoToAsync(".."), IsBack: true)]);
    private void OnMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);
#endif

	public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);
}
