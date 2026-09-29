using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>The Alerts tab; the dashboard's counts come here with <see cref="Routes.AlertFilterParameter"/>.</summary>
public partial class AlertsPage : ContentPage, IQueryAttributable
{
	private readonly AlertsViewModel _viewModel;

	public AlertsPage(AlertsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.AlertFilterParameter, out var value) && value is string kind)
		{
			_viewModel.ShowOnly(kind);
		}

		// Applied once: coming back to the tab later shouldn't reapply it.
		query.Clear();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
		_viewModel.RefreshCommand.Execute(null);
	}
}
