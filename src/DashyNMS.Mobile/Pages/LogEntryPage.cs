using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.LogEntry"/>, with the entry and its list
/// (#118). The entry itself is <see cref="LogEntryView"/>, which the Logs
/// list also shows beside itself on a larger screen (#88).
/// </summary>
public sealed class LogEntryPage : ContentPage, IQueryAttributable
{
	private readonly LogEntryView _view;

	public LogEntryPage(LogEntryViewModel viewModel)
	{
		BindingContext = viewModel;
		Title = "Entry";
		On<iOS>().SetUseSafeArea(true);
		Content = _view = new LogEntryView(viewModel);
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
				new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => _ = Shell.Current.GoToAsync(".."), IsBack: true),
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
