using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
using DashyNMS.Mobile.Views;

namespace DashyNMS.Mobile.Pages;

/// <summary>Owns navigation and top safe area for the pushed link snapshot.</summary>
public sealed class NeighbourLinkPage : ContentPage, IQueryAttributable, IActivePage
{
    private readonly NeighbourLinkView _view;
    private IDisposable? _edges;
    private bool _active;

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
        SizeChanged += (_, _) => PlaceControls();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
    }

    /// <summary>Uses the shell activation owner to restore retained pushed Back.</summary>
    public void Activate()
    {
        _active = true;
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
        _view.Bar.Apply(this);
#if IOS
        _view.Bar.ShowsBack = !TabBarButtons.IsAvailable;
        if (TabBarButtons.IsAvailable)
            TabBarButtons.Show(this, null, string.Empty,
                [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                    () => _ = Shell.Current.GoToAsync(".."), IsBack: true)]);
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
#endif
        PlaceControls();
    }

    private void PlaceControls()
    {
        if (!_active || Shell.Current?.CurrentPage != this) return;
        _view.PlaceContent(CoveredEdges.ForScrolling(this));
    }

#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);
#endif

    protected override void OnDisappearing()
    {
        _active = false;
        _edges?.Dispose();
        _edges = null;
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
        base.OnDisappearing();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);
}
