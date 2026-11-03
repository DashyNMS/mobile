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

	public AlertDetailPage(AlertDetailViewModel viewModel)
	{
		BindingContext = viewModel;
		SetBinding(TitleProperty, new Binding(nameof(AlertDetailViewModel.Title)));
		On<iOS>().SetUseSafeArea(true);
		Content = _view = new AlertDetailView(viewModel);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
        Activate();
	}

    public void Activate()
    {
		_view.Bar.Apply(this);
#if IOS
        _view.Bar.IsVisible = !TabBarButtons.IsAvailable;
        TabBarButtons.Show(this, null, string.Empty,
            [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => _ = Shell.Current.GoToAsync(".."), IsBack: true)]);
#endif
	}

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if IOS
        TabBarButtons.Hide(this);
#endif
    }

	public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);
}
