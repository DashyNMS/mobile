using DashyNMS.Mobile.Rules;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.AlertTemplate"/>: one alert template, read-only (#22).</summary>
public partial class AlertTemplatePage : ContentPage, IQueryAttributable
{
	private readonly AlertTemplateViewModel _viewModel;

	public AlertTemplatePage(AlertTemplateViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		Controls.PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading the template…", "Refreshing the template…");
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.TemplateIdParameter, out var id) && id is int templateId)
		{
			_viewModel.TemplateId = templateId;
			_viewModel.RefreshCommand.Execute(null);
		}
	}
}
