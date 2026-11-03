using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;

namespace DashyNMS.Mobile.Pages;

/// <summary>Pushed log detail; the embedded host retains its own glass ownership.</summary>
public sealed class LogEntryPage : ContentPage, IQueryAttributable, IActivePage
{
    private readonly LogEntryView _view;
    private IDisposable? _edges;
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
        Activate();
    }

    /// <summary>Restores retained page ownership through AppShell's activation hook.</summary>
    public void Activate()
    {
        _showing = true;
        _view.Bar.Apply(this);
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
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
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
    }

    private void RestoreGlass()
    {
#if IOS
        _view.Bar.IsVisible = !TabBarButtons.IsAvailable;
        if (TabBarButtons.IsAvailable)
            TabBarButtons.Show(this, null, string.Empty,
            [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => { if (_showing) _ = Shell.Current.GoToAsync(".."); }, IsBack: true)]);
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
