using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Topology;
using DashyNMS.Mobile.Views;

namespace DashyNMS.Mobile.Pages;

/// <summary>Owns navigation and top safe area for the pushed link snapshot.</summary>
public sealed class NeighbourLinkPage : ContentPage, IQueryAttributable
{
    private readonly NeighbourLinkView _view;
    private Window? _window;
    private Shell? _shell;

    public NeighbourLinkPage(NeighbourLinkViewModel viewModel)
    {
        BindingContext = viewModel;
        Title = "Link";
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container,
            SafeAreaRegions.None, SafeAreaRegions.None);
#else
        SafeAreaEdges = SafeAreaEdges.All;
#endif
        Content = _view = new NeighbourLinkView(viewModel);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        OnUnloaded(sender, e);
        _window = Window;
        _shell = Shell.Current;
        if (_window is not null) _window.Resumed += OnResumed;
        if (_shell is not null) _shell.Navigated += OnNavigated;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_window is not null) _window.Resumed -= OnResumed;
        if (_shell is not null) _shell.Navigated -= OnNavigated;
        _window = null;
        _shell = null;
    }

    private void OnResumed(object? sender, EventArgs e) => Restore();
    private void OnNavigated(object? sender, ShellNavigatedEventArgs e) => Restore();
    private void Restore() => Dispatcher.Dispatch(() =>
    {
        if (Shell.Current?.CurrentPage == this) Activate();
    });

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
    }

    private void Activate()
    {
        _view.Bar.Apply(this);
#if IOS
        TabBarButtons.Moved -= OnMoved;
        TabBarButtons.Moved += OnMoved;
        _view.Bar.ShowsBack = !TabBarButtons.IsAvailable;
        if (TabBarButtons.IsAvailable)
            TabBarButtons.Show(this, null, string.Empty,
                [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                    () => _ = Shell.Current.GoToAsync(".."), IsBack: true)]);
#endif
        _view.Place();
    }

#if IOS
    private void OnMoved(object? sender, EventArgs e) => _view.Place();
#endif
    protected override void OnDisappearing()
    {
#if IOS
        TabBarButtons.Moved -= OnMoved;
        TabBarButtons.Hide(this);
#endif
        base.OnDisappearing();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);
}
