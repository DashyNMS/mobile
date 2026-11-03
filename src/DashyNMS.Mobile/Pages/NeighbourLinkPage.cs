using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
using DashyNMS.Mobile.Views;

namespace DashyNMS.Mobile.Pages;

/// <summary>Owns navigation and top safe area for the pushed link snapshot.</summary>
public sealed class NeighbourLinkPage : ContentPage, IQueryAttributable, IActivePage
{
    private readonly NeighbourLinkView _view;

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
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
    }

    /// <summary>Uses the shell activation owner to restore retained pushed Back.</summary>
    public void Activate()
    {
        _view.Bar.Apply(this);
#if IOS
        _view.Bar.ShowsBack = !TabBarButtons.IsAvailable;
        if (TabBarButtons.IsAvailable)
            TabBarButtons.Show(this, null, string.Empty,
                [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                    () => _ = Shell.Current.GoToAsync(".."), IsBack: true)]);
#endif
        _view.Place();
    }

    protected override void OnDisappearing()
    {
#if IOS
        TabBarButtons.Hide(this);
#endif
        base.OnDisappearing();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);
}
