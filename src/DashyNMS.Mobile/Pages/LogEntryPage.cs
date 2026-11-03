using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;

namespace DashyNMS.Mobile.Pages;

/// <summary>Pushed log detail; the embedded host retains its own glass ownership.</summary>
public sealed class LogEntryPage : ContentPage, IQueryAttributable
{
    private readonly LogEntryView _view;
    private IDisposable? _edges;
    private Window? _resumeWindow;
    private bool _showing;

    public LogEntryPage(LogEntryViewModel viewModel)
    {
        BindingContext = viewModel;
        Title = "Entry";
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
#endif
        Content = _view = new LogEntryView(viewModel);
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
