using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.TopCardSetUp"/>, with the card it sets up: a Top card's rows, ranking and title (#103).</summary>
public partial class TopCardSetUpPage : ContentPage, IQueryAttributable
{
	private readonly TopCardSetUpViewModel _viewModel;

	public TopCardSetUpPage(TopCardSetUpViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.WidgetIdParameter, out var id) && id is string widgetId && widgetId.Length > 0)
		{
			_viewModel.WidgetId = widgetId;
		}

		_viewModel.Load();
	}

	/// <summary>The app's own top bar, as the other pushed pages (#140).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		TopBar.Apply(this);
	}
}
