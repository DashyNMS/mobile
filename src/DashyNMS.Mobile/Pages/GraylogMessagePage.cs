using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;

namespace DashyNMS.Mobile.Pages;

/// <summary>Pushed message host; embedded messages retain their list's glass owner.</summary>
public sealed class GraylogMessagePage : ContentPage, IQueryAttributable, IActivePage
{
    private readonly GraylogMessageView _view;
    private IDisposable? _edges;
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
        Activate();
    }

    /// <summary>Restores retained message controls through AppShell's single activation owner.</summary>
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
