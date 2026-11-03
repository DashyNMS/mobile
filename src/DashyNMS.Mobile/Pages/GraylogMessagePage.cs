using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;

namespace DashyNMS.Mobile.Pages;

/// <summary>Pushed message host; embedded messages retain their list's glass owner.</summary>
public sealed class GraylogMessagePage : ContentPage, IQueryAttributable
{
    private readonly GraylogMessageView _view;
    private IDisposable? _edges;
    private Window? _resumeWindow;
    private bool _showing;

    public GraylogMessagePage(GraylogMessageViewModel viewModel)
    {
        BindingContext = viewModel;
        Title = "Message";
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
#endif
        Content = _view = new GraylogMessageView(viewModel);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_showing) return;
        _showing = true;
        _view.Bar.Apply(this);
        _edges = CoveredEdges.Watch(this, PlaceControls);
        _resumeWindow = Window;
        if (_resumeWindow is not null) _resumeWindow.Resumed += OnResumed;
#if IOS
        TabBarButtons.Moved += OnGlassMoved;
#endif
        RestoreGlass();
        PlaceControls();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _showing = false;
        _edges?.Dispose();
        _edges = null;
        if (_resumeWindow is not null) _resumeWindow.Resumed -= OnResumed;
        _resumeWindow = null;
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
    }

    private void OnResumed(object? sender, EventArgs e)
    {
        if (!_showing) return;
        RestoreGlass();
        PlaceControls();
    }

    private void RestoreGlass()
    {
#if IOS
        _view.Bar.IsVisible = !TabBarButtons.IsAvailable;
        if (TabBarButtons.IsAvailable)
            TabBarButtons.Show(this, null, string.Empty,
            [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => _ = Shell.Current.GoToAsync(".."), IsBack: true)]);
#endif
    }

    private void PlaceControls()
    {
        if (_showing) _view.PlaceContent(CoveredEdges.ForScrolling(this));
    }
#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);
#endif
    public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);
}
