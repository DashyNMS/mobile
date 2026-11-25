using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.TopCardSetUp"/>, with the card it sets up: a Top card's rows, ranking and title (#103).</summary>
public partial class TopCardSetUpPage : ContentPage, IQueryAttributable, IActivePage
{
	private readonly TopCardSetUpViewModel _viewModel;
	private readonly SetupPageLayout _placement;

	public TopCardSetUpPage(TopCardSetUpViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		// The shared helper owns the header, insets, glass Back and keyboard while visible (#397).
		_placement = new SetupPageLayout(this, Setup, Header, FormScroll);
	}

	public void Activate() => _placement.Activate();

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.WidgetIdParameter, out var id) && id is string widgetId && widgetId.Length > 0)
		{
			_viewModel.WidgetId = widgetId;
		}

		_viewModel.Load();
	}
}
