using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>The Alerts tab; the dashboard's counts come here with <see cref="Routes.AlertFilterParameter"/>.</summary>
public partial class AlertsPage : ContentPage, IQueryAttributable
{
	private readonly AlertsViewModel _viewModel;
	private readonly ShortcutReturn _shortcut = new();

	public AlertsPage(AlertsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		SearchReveal.Attach(List, SearchSlot, Search);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.AlertFilterParameter, out var value) && value is string kind)
		{
			_shortcut.Arrived();
			_viewModel.ShowOnly(kind);
		}

		// Applied once: coming back to the tab later shouldn't reapply it.
		query.Clear();
	}

	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		_shortcut.Left(this, args);
	}

	protected override void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);
		if (_shortcut.OpenedAfresh())
		{
			_viewModel.ShowSavedFilter();
		}
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
		_viewModel.RefreshCommand.Execute(null);
	}
}
