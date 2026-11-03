using DashyNMS.Mobile.Rules;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;
using DashyNMS.Mobile.Controls;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.AlertRule"/>: one alert rule, read-only (#22).
/// The rule itself is <see cref="AlertRuleView"/>, which the Alert rules list
/// also shows beside itself on a larger screen (#88, #122).
/// </summary>
public sealed class AlertRulePage : ContentPage, IQueryAttributable
{
	private readonly AlertRuleView _view;
    private IDisposable? _edges;

	public AlertRulePage(AlertRuleViewModel viewModel)
	{
		BindingContext = viewModel;
		SetBinding(TitleProperty, new Binding(nameof(AlertRuleViewModel.Title)));
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
#endif
		Content = _view = new AlertRuleView(viewModel);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
        _view.Bar.Apply(this);
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
#if IOS
        _view.Bar.IsVisible = !TabBarButtons.IsAvailable;
        TabBarButtons.Show(this, null, string.Empty,
            [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => _ = Shell.Current.GoToAsync(".."), IsBack: true)]);
        TabBarButtons.Moved += OnGlassMoved;
#endif
        PlaceControls();
	}

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _edges?.Dispose();
        _edges = null;
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
    }

    private void PlaceControls() => _view.PlaceContent(CoveredEdges.ForScrolling(this));
#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);
#endif

	public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);
}
