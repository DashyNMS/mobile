using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.GraylogMessage"/>, with the entry and its list
/// (#117). The entry itself is <see cref="GraylogMessageView"/>, which the Graylog
/// list also shows beside itself on a larger screen (#88).
/// </summary>
public sealed class GraylogMessagePage : ContentPage, IQueryAttributable
{
	private readonly GraylogMessageView _view;

	public GraylogMessagePage(GraylogMessageViewModel viewModel)
	{
		BindingContext = viewModel;
		Title = "Message";
		On<iOS>().SetUseSafeArea(true);
		Content = _view = new GraylogMessageView(viewModel);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_view.Bar.Apply(this);
#if IOS
		_view.Bar.IsVisible = !TabBarButtons.IsAvailable;
		if (TabBarButtons.IsAvailable)
		{
			TabBarButtons.Show(this, null, string.Empty,
			[
				new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => _ = Shell.Current.GoToAsync("..")),
			]);
		}
		TabBarButtons.Moved += OnGlassMoved;
		PlaceFooter();
#endif
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
#if IOS
		TabBarButtons.Moved -= OnGlassMoved;
		TabBarButtons.Hide(this);
#endif
	}

#if IOS
	private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceFooter);
	private void PlaceFooter() => _view.SetGlassClearance(TabBarButtons.CoveredBottom);
#endif

	public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);
}
